import type { Skill } from '@prisma/client';
import { analyticsRepository } from '../repositories/analytics.repository.js';
import { gameplayRepository } from '../repositories/gameplay.repository.js';
import { accountRepository } from '../repositories/account.repository.js';
import { dashboardService } from './dashboard.service.js';
import { progressService } from './progress.service.js';
import { groqService, type GroqMessage } from './groq.service.js';
import { ALL_SKILLS, SKILL_LABELS } from '../domain/skills.js';
import { addDays, startOfLocalDay } from '../lib/dates.js';
import { HttpError } from '../lib/http-error.js';

/** How much prior conversation is replayed to the model. */
const HISTORY_LIMIT = 12;

/**
 * The parent-facing assistant.
 *
 * Every reply is grounded in a factual summary built from this child's own database
 * rows, assembled here rather than sent from the browser - a client-supplied summary
 * would let anyone ask the model to comment on numbers that were never recorded.
 */
export const chatService = {
  async history(parentId: string, childId: string) {
    await dashboardService.assertOwnsChild(parentId, childId);

    const messages = await analyticsRepository.listChatMessages(parentId, childId);

    return messages.map((message) => ({
      id: message.id,
      from: message.role === 'AI' ? ('ai' as const) : ('parent' as const),
      text: message.content,
      createdAt: message.createdAt.toISOString(),
    }));
  },

  async send(parentId: string, childId: string, text: string) {
    await dashboardService.assertOwnsChild(parentId, childId);

    if (!groqService.isConfigured) {
      throw HttpError.serviceUnavailable(
        'The assistant is not configured yet. Add GROQ_API_KEY to backend/.env and restart the server.',
      );
    }

    // Nothing is written until there is a reply to write with it. Saving the question
    // first meant a failed or unconfigured assistant left a message in the transcript
    // that nothing ever answered - and which reappeared on the next page load, after
    // the browser had already removed it.
    const [context, history] = await Promise.all([
      this.buildContext(childId),
      analyticsRepository.listChatMessages(parentId, childId, HISTORY_LIMIT),
    ]);

    const messages: GroqMessage[] = [
      { role: 'system', content: context },
      ...history.map((message) => ({
        role: message.role === 'AI' ? ('assistant' as const) : ('user' as const),
        content: message.content,
      })),
      { role: 'user', content: text },
    ];

    const reply = await groqService.complete(messages);

    const { parentMessage, aiMessage } = await analyticsRepository.createExchange({
      parentId,
      childId,
      parentText: text,
      aiText: reply,
    });

    return {
      parentMessage: {
        id: parentMessage.id,
        from: 'parent' as const,
        text: parentMessage.content,
        createdAt: parentMessage.createdAt.toISOString(),
      },
      aiMessage: {
        id: aiMessage.id,
        from: 'ai' as const,
        text: aiMessage.content,
        createdAt: aiMessage.createdAt.toISOString(),
      },
    };
  },

  /** Builds the system prompt: role, ground rules, and this child's real figures. */
  async buildContext(childId: string): Promise<string> {
    const weekStart = addDays(startOfLocalDay(new Date()), -6);

    const child = await accountRepository.findChildById(childId);

    const [summary, attempts, playtimeSeconds] = await Promise.all([
      progressService.summary(childId),
      gameplayRepository.listAttempts(childId, { since: weekStart }),
      gameplayRepository.totalPlaytimeSeconds(childId, weekStart),
    ]);

    const { scores, tallies } = summary;

    const name = child?.displayName ?? 'the child';
    const age = child?.age ? `${child.age}` : 'unknown';

    const completed = attempts.filter((attempt) => attempt.status === 'COMPLETED');

    const skillLines = ALL_SKILLS.map((skill: Skill) => {
      const tally = tallies[skill];

      return `- ${SKILL_LABELS[skill]}: score ${scores[skill]}/100 (${tally.correct} of ${tally.total} choices correct)`;
    }).join('\n');

    const missionLines =
      completed.length === 0
        ? '- none completed in the last 7 days'
        : completed
            .map(
              (attempt) =>
                `- "${attempt.mission.title}" (${SKILL_LABELS[attempt.mission.skill]}), scored ${attempt.score ?? 0}/${attempt.maxScore}`,
            )
            .join('\n');

    // A child with no recorded decisions scores a neutral 50 everywhere. Left unsaid,
    // the model would discuss those placeholders as though they were measurements.
    const dataNote = summary.hasData
      ? `These scores are based on ${summary.decisionCount} recorded choice(s).`
      : 'IMPORTANT: this child has not made any recorded choices yet. The scores below are ' +
        'neutral placeholders, NOT measurements. Do not describe them as results, strengths ' +
        'or weaknesses. Encourage the parent to have them play a first mission.';

    return [
      'You are the ShinyMinds parent assistant. ShinyMinds is an educational game that teaches',
      'children aged 8-14 about personal safety, communication, empathy and confidence.',
      'You are speaking to the parent about their own child.',
      '',
      'GROUND RULES:',
      '- Only use the figures below. If you are asked something the data does not cover, say so plainly.',
      '- Never invent scores, missions, dates or events.',
      '- Be warm, brief and practical. Two short paragraphs at most, or a short list.',
      '- Suggest concrete things the parent can do at home.',
      '- You are not a clinician. Do not diagnose. For serious concerns, suggest speaking to a teacher or doctor.',
      '',
      `CHILD: ${name}, age ${age}`,
      `PLAYTIME (last 7 days): ${Math.round(playtimeSeconds / 60)} minutes`,
      '',
      dataNote,
      '',
      'CURRENT SKILL SCORES (0-100, higher is better):',
      skillLines,
      '',
      'MISSIONS COMPLETED IN THE LAST 7 DAYS:',
      missionLines,
    ].join('\n');
  },
};

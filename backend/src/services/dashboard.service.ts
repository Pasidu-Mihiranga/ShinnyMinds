import type { Skill } from '@prisma/client';
import { HttpError } from '../lib/http-error.js';
import { accountRepository } from '../repositories/account.repository.js';
import { analyticsRepository } from '../repositories/analytics.repository.js';
import { gameplayRepository } from '../repositories/gameplay.repository.js';
import { progressService } from './progress.service.js';
import { toChildView } from './auth.service.js';
import {
  ALL_SKILLS,
  SKILL_COLORS,
  SKILL_ICONS,
  SKILL_LABELS,
  needsAttention,
  overallScore,
  skillNote,
  wellbeingLabel,
} from '../domain/skills.js';
import {
  addDays,
  formatDayLabel,
  formatRange,
  lastNLocalDays,
  localDayKey,
  startOfLocalDay,
  toDayKey,
} from '../lib/dates.js';

const WEEK_DAYS = 7;

/**
 * Read models for the parent dashboard. Each method returns exactly what one screen
 * renders, so the frontend never has to aggregate or interpret raw rows itself.
 */
export const dashboardService = {
  /** A parent may only ever read their own children. Every method starts here. */
  async assertOwnsChild(parentId: string, childId: string) {
    const child = await accountRepository.findChildById(childId);

    if (!child || child.parentId !== parentId) {
      throw HttpError.notFound('Child not found.');
    }

    return child;
  },

  async children(parentId: string) {
    const children = await accountRepository.listChildrenForParent(parentId);

    return Promise.all(
      children.map(async (child) => {
        const summary = await progressService.summary(child.id);

        return {
          ...toChildView(child),
          overallScore: overallScore(summary.scores),
          hasPlayed: summary.hasData,
        };
      }),
    );
  },

  async overview(parentId: string, childId: string) {
    const child = await this.assertOwnsChild(parentId, childId);

    const now = new Date();
    const todayStart = startOfLocalDay(now);
    const weekStart = addDays(todayStart, -(WEEK_DAYS - 1));

    const [summary, weekAttempts, weekPlaytime, todayPlaytime] = await Promise.all([
      progressService.summary(childId),
      gameplayRepository.listAttempts(childId, { since: weekStart }),
      gameplayRepository.totalPlaytimeSeconds(childId, weekStart),
      gameplayRepository.totalPlaytimeSeconds(childId, todayStart),
    ]);

    const completed = weekAttempts.filter((attempt) => attempt.status === 'COMPLETED');
    const skillsPracticed = new Set(weekAttempts.map((attempt) => attempt.mission.skill)).size;

    const overall = overallScore(summary.scores);

    return {
      child: toChildView(child),
      // Without any recorded decisions the scores are a neutral placeholder, not a
      // measurement. Saying so lets the dashboard avoid presenting 50/100 as a result.
      hasPlayed: summary.hasData,
      overallWellbeing: {
        score: overall,
        label: summary.hasData ? wellbeingLabel(overall) : 'Not enough data yet',
      },
      skills: ALL_SKILLS.map((skill) => toSkillCard(skill, summary.scores[skill], summary.hasData)),
      weekSummary: {
        range: formatRange(localDayKey(weekStart), localDayKey(now)),
        scenariosCompleted: completed.length,
        skillsPracticed,
        screenTimeTodayMin: Math.round(todayPlaytime / 60),
        screenTimePerDayMin: Math.round(weekPlaytime / 60 / WEEK_DAYS),
      },
      aiTip: buildTip(child.displayName, summary.scores, summary.hasData),
    };
  },

  /** Daily skill history for the trend chart. */
  async skillsProgress(parentId: string, childId: string, days = WEEK_DAYS) {
    await this.assertOwnsChild(parentId, childId);

    const window = lastNLocalDays(days);
    const windowStart = window[0] ?? localDayKey(new Date());

    const [snapshots, summary] = await Promise.all([
      analyticsRepository.listSnapshots(childId, windowStart),
      progressService.summary(childId),
    ]);

    const byDay = new Map<string, Map<Skill, number>>();

    for (const snapshot of snapshots) {
      const key = toDayKey(snapshot.day);
      const bucket = byDay.get(key) ?? new Map<Skill, number>();

      bucket.set(snapshot.skill, snapshot.score);
      byDay.set(key, bucket);
    }

    return {
      days: window.map(formatDayLabel),
      hasPlayed: summary.hasData,
      series: ALL_SKILLS.map((skill) => {
        // Days before the child ever played have no snapshot. Carrying the previous
        // value forward draws a flat line rather than a misleading drop to zero.
        let carried: number | null = null;

        const values = window.map((day) => {
          const recorded = byDay.get(toDayKey(day))?.get(skill);

          if (recorded !== undefined) {
            carried = recorded;
          }

          return carried;
        });

        return {
          key: skill,
          label: SKILL_LABELS[skill],
          color: SKILL_COLORS[skill],
          values,
          current: summary.scores[skill],
        };
      }),
    };
  },

  /** Recently played missions, newest first. */
  async activity(parentId: string, childId: string, take = 10) {
    await this.assertOwnsChild(parentId, childId);

    const attempts = await gameplayRepository.listAttempts(childId, { take });

    return attempts.map((attempt) => ({
      id: attempt.id,
      title: attempt.mission.title,
      missionCode: attempt.mission.code,
      focus: SKILL_LABELS[attempt.mission.skill],
      skill: attempt.mission.skill,
      color: SKILL_COLORS[attempt.mission.skill],
      status: attempt.status === 'COMPLETED' ? 'Completed' : 'In Progress',
      score: attempt.score,
      maxScore: attempt.maxScore,
      when: (attempt.completedAt ?? attempt.startedAt).toISOString(),
    }));
  },

  /** Where the week's play went, and which skills the parent should focus on. */
  async insights(parentId: string, childId: string) {
    await this.assertOwnsChild(parentId, childId);

    const weekStart = addDays(startOfLocalDay(new Date()), -(WEEK_DAYS - 1));

    const [attempts, summary] = await Promise.all([
      gameplayRepository.listAttempts(childId, { since: weekStart }),
      progressService.summary(childId),
    ]);

    const completed = attempts.filter((attempt) => attempt.status === 'COMPLETED');

    const counts = new Map<Skill, number>();

    for (const attempt of completed) {
      counts.set(attempt.mission.skill, (counts.get(attempt.mission.skill) ?? 0) + 1);
    }

    const total = completed.length;

    return {
      hasPlayed: summary.hasData,
      weeklyCompletions: {
        total,
        breakdown: ALL_SKILLS.filter((skill) => (counts.get(skill) ?? 0) > 0).map((skill) => {
          const count = counts.get(skill) ?? 0;

          return {
            key: skill,
            label: SKILL_LABELS[skill],
            count,
            pct: total === 0 ? 0 : Math.round((count / total) * 100),
            color: SKILL_COLORS[skill],
          };
        }),
      },
      // A child who has never played scores a neutral 50 in every skill, which is below
      // the attention threshold. Flagging all four would tell a parent to worry about
      // results that do not exist.
      needsAttention: summary.hasData
        ? needsAttention(summary.scores)
            .slice(0, 2)
            .map((skill) => toSkillCard(skill, summary.scores[skill], true))
        : [],
    };
  },
};

function toSkillCard(skill: Skill, score: number, hasData: boolean) {
  return {
    key: skill,
    label: SKILL_LABELS[skill],
    score,
    color: SKILL_COLORS[skill],
    icon: SKILL_ICONS[skill],
    note: hasData ? skillNote(score) : 'Not practised yet',
  };
}

/** A deterministic one-line tip. The conversational assistant lives in chat.service. */
function buildTip(childName: string, scores: Record<Skill, number>, hasData: boolean): string {
  if (!hasData) {
    return `${childName} hasn't played a mission yet. Once they do, their strengths and the areas to work on will appear here.`;
  }

  const byScore = [...ALL_SKILLS].sort((a, b) => scores[a] - scores[b]);

  const weakest = byScore[0];
  const strongest = byScore[byScore.length - 1];

  if (!weakest || !strongest || scores[strongest] === scores[weakest]) {
    return `${childName} is scoring evenly across all four skills. Keep practising to see where their strengths emerge.`;
  }

  return (
    `${childName} is strongest in ${SKILL_LABELS[strongest]} (${scores[strongest]}). ` +
    `${SKILL_LABELS[weakest]} (${scores[weakest]}) has the most room to grow - try a mission focused on it this week.`
  );
}

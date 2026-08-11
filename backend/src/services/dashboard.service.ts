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
import { addDays, formatDayLabel, formatRange, lastNDays, startOfUtcDay, toDayKey } from '../lib/dates.js';

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
      children.map(async (child) => ({
        ...toChildView(child),
        overallScore: await progressService.overall(child.id),
      })),
    );
  },

  async overview(parentId: string, childId: string) {
    const child = await this.assertOwnsChild(parentId, childId);

    const weekStart = addDays(startOfUtcDay(new Date()), -(WEEK_DAYS - 1));

    const [scores, weekAttempts, weekPlaytime, todayPlaytime] = await Promise.all([
      progressService.currentScores(childId),
      gameplayRepository.listAttempts(childId, { since: weekStart }),
      gameplayRepository.totalPlaytimeSeconds(childId, weekStart),
      gameplayRepository.totalPlaytimeSeconds(childId, startOfUtcDay(new Date())),
    ]);

    const completed = weekAttempts.filter((attempt) => attempt.status === 'COMPLETED');
    const skillsPracticed = new Set(weekAttempts.map((attempt) => attempt.mission.skill)).size;

    const overall = overallScore(scores);

    return {
      child: toChildView(child),
      overallWellbeing: { score: overall, label: wellbeingLabel(overall) },
      skills: ALL_SKILLS.map((skill) => toSkillCard(skill, scores[skill])),
      weekSummary: {
        range: formatRange(weekStart, new Date()),
        scenariosCompleted: completed.length,
        skillsPracticed,
        screenTimeTodayMin: Math.round(todayPlaytime / 60),
        screenTimePerDayMin: Math.round(weekPlaytime / 60 / WEEK_DAYS),
      },
      aiTip: buildTip(child.displayName, scores),
    };
  },

  /** Daily skill history for the trend chart. */
  async skillsProgress(parentId: string, childId: string, days = WEEK_DAYS) {
    await this.assertOwnsChild(parentId, childId);

    const window = lastNDays(days);
    const windowStart = window[0] ?? startOfUtcDay(new Date());

    const [snapshots, currentScores] = await Promise.all([
      analyticsRepository.listSnapshots(childId, windowStart),
      progressService.currentScores(childId),
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
          current: currentScores[skill],
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

    const weekStart = addDays(startOfUtcDay(new Date()), -(WEEK_DAYS - 1));

    const [attempts, scores] = await Promise.all([
      gameplayRepository.listAttempts(childId, { since: weekStart }),
      progressService.currentScores(childId),
    ]);

    const completed = attempts.filter((attempt) => attempt.status === 'COMPLETED');

    const counts = new Map<Skill, number>();

    for (const attempt of completed) {
      counts.set(attempt.mission.skill, (counts.get(attempt.mission.skill) ?? 0) + 1);
    }

    const total = completed.length;

    return {
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
      needsAttention: needsAttention(scores)
        .slice(0, 2)
        .map((skill) => toSkillCard(skill, scores[skill])),
    };
  },
};

function toSkillCard(skill: Skill, score: number) {
  return {
    key: skill,
    label: SKILL_LABELS[skill],
    score,
    color: SKILL_COLORS[skill],
    icon: SKILL_ICONS[skill],
    note: skillNote(score),
  };
}

/** A deterministic one-line tip. The conversational assistant lives in chat.service. */
function buildTip(childName: string, scores: Record<Skill, number>): string {
  const weakest = [...ALL_SKILLS].sort((a, b) => scores[a] - scores[b])[0];
  const strongest = [...ALL_SKILLS].sort((a, b) => scores[b] - scores[a])[0];

  if (!weakest || !strongest) {
    return `${childName} has not played yet. Start a mission together to see progress here.`;
  }

  if (scores[strongest] === scores[weakest]) {
    return `${childName} is off to an even start across all four skills. Keep practising to see where their strengths emerge.`;
  }

  return (
    `${childName} is strongest in ${SKILL_LABELS[strongest]} (${scores[strongest]}). ` +
    `${SKILL_LABELS[weakest]} (${scores[weakest]}) has the most room to grow - try a mission focused on it this week.`
  );
}

import type { Decision, Skill } from '@prisma/client';
import { gameplayRepository } from '../repositories/gameplay.repository.js';
import { analyticsRepository } from '../repositories/analytics.repository.js';
import {
  ALL_SKILLS,
  emptyTallies,
  overallScore,
  scoresFromTallies,
  type SkillTally,
} from '../domain/skills.js';

/**
 * Turns raw decisions into skill scores, and keeps the daily snapshot table current.
 *
 * Current scores are always recomputed from decisions rather than incremented in place:
 * an incremental counter drifts the moment a write is retried or a row is deleted, and
 * a child's history is small enough that recomputing is cheap.
 */
export const progressService = {
  async tallies(childId: string, since?: Date): Promise<Record<Skill, SkillTally>> {
    return tally(await gameplayRepository.listDecisions(childId, since));
  },

  async currentScores(childId: string): Promise<Record<Skill, number>> {
    return scoresFromTallies(await this.tallies(childId));
  },

  async overall(childId: string): Promise<number> {
    return overallScore(await this.currentScores(childId));
  },

  /**
   * Recomputes every skill and records today's value. Called after each decision and
   * each completed mission so the parent dashboard's trend line has real daily points.
   */
  async recordSnapshot(childId: string): Promise<Record<Skill, number>> {
    const scores = await this.currentScores(childId);

    await Promise.all(
      ALL_SKILLS.map((skill) => analyticsRepository.upsertSnapshot(childId, skill, scores[skill])),
    );

    return scores;
  },
};

export function tally(decisions: Decision[]): Record<Skill, SkillTally> {
  const result = emptyTallies();

  for (const decision of decisions) {
    const bucket = result[decision.skill];

    bucket.total += 1;

    if (decision.isCorrect) {
      bucket.correct += 1;
    }
  }

  return result;
}

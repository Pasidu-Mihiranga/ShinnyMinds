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
export interface ProgressSummary {
  tallies: Record<Skill, SkillTally>;
  scores: Record<Skill, number>;
  /** Total choices ever recorded. Zero means this child has never played. */
  decisionCount: number;
  hasData: boolean;
}

export const progressService = {
  async tallies(childId: string, since?: Date): Promise<Record<Skill, SkillTally>> {
    return tally(await gameplayRepository.listDecisions(childId, since));
  },

  /**
   * Scores plus the evidence behind them, from a single query.
   *
   * `hasData` matters as much as the scores do: with no decisions every skill sits at
   * the neutral 50, which is indistinguishable from a genuine mid-range result. Callers
   * need to be able to say "not played yet" rather than reporting a made-up 50.
   */
  async summary(childId: string): Promise<ProgressSummary> {
    const tallies = await this.tallies(childId);

    const decisionCount = ALL_SKILLS.reduce((sum, skill) => sum + tallies[skill].total, 0);

    return {
      tallies,
      scores: scoresFromTallies(tallies),
      decisionCount,
      hasData: decisionCount > 0,
    };
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

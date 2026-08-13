import { Skill } from '@prisma/client';

/**
 * Pure scoring rules. No database, no Express - so the numbers the parent sees can be
 * reasoned about and unit-tested on their own, and the game and the dashboard can
 * never disagree about what a score means.
 */

export const ALL_SKILLS: Skill[] = [
  Skill.SAFETY,
  Skill.COMMUNICATION,
  Skill.EMPATHY,
  Skill.CONFIDENCE,
];

export const SKILL_LABELS: Record<Skill, string> = {
  SAFETY: 'Safety Awareness',
  COMMUNICATION: 'Communication',
  EMPATHY: 'Empathy',
  CONFIDENCE: 'Confidence',
};

export const SKILL_COLORS: Record<Skill, string> = {
  SAFETY: '#16a34a',
  COMMUNICATION: '#2563eb',
  EMPATHY: '#7c3aed',
  CONFIDENCE: '#f97316',
};

export const SKILL_ICONS: Record<Skill, string> = {
  SAFETY: 'shield',
  COMMUNICATION: 'message',
  EMPATHY: 'heart',
  CONFIDENCE: 'star',
};

/**
 * A child who answers one question correctly is not "100% safe". Scores are smoothed
 * towards a neutral 50 by pretending we have already seen PRIOR_WEIGHT half-correct
 * answers, so a score only reaches an extreme once there is enough evidence for it.
 */
const PRIOR_WEIGHT = 4;
const PRIOR_MEAN = 0.5;

/** Below this a skill is surfaced to the parent as needing attention. */
export const ATTENTION_THRESHOLD = 78;

export type SkillTally = { correct: number; total: number };

export function emptyTallies(): Record<Skill, SkillTally> {
  return {
    SAFETY: { correct: 0, total: 0 },
    COMMUNICATION: { correct: 0, total: 0 },
    EMPATHY: { correct: 0, total: 0 },
    CONFIDENCE: { correct: 0, total: 0 },
  };
}

/** Converts a correct/total tally into a 0-100 score. No data yields a neutral 50. */
export function scoreFromTally(tally: SkillTally): number {
  const smoothed =
    (tally.correct + PRIOR_WEIGHT * PRIOR_MEAN) / (tally.total + PRIOR_WEIGHT);

  return Math.round(smoothed * 100);
}

export function scoresFromTallies(
  tallies: Record<Skill, SkillTally>,
): Record<Skill, number> {
  return {
    SAFETY: scoreFromTally(tallies.SAFETY),
    COMMUNICATION: scoreFromTally(tallies.COMMUNICATION),
    EMPATHY: scoreFromTally(tallies.EMPATHY),
    CONFIDENCE: scoreFromTally(tallies.CONFIDENCE),
  };
}

/** Overall wellbeing is the mean of the four skill scores. */
export function overallScore(scores: Record<Skill, number>): number {
  const total = ALL_SKILLS.reduce((sum, skill) => sum + scores[skill], 0);

  return Math.round(total / ALL_SKILLS.length);
}

export function wellbeingLabel(score: number): string {
  if (score >= 85) return 'Excellent';
  if (score >= 70) return 'Good';
  if (score >= 55) return 'Developing';

  return 'Needs support';
}

export function skillNote(score: number): string {
  if (score >= 85) return 'Strong understanding';
  if (score >= 70) return 'Actively improving';
  if (score >= 55) return 'Showing steady growth';

  return 'More scenarios recommended';
}

/** Skills below the attention threshold, weakest first. */
export function needsAttention(scores: Record<Skill, number>): Skill[] {
  return ALL_SKILLS.filter((skill) => scores[skill] < ATTENTION_THRESHOLD).sort(
    (a, b) => scores[a] - scores[b],
  );
}

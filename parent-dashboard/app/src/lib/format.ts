import type { SkillKey } from '../api/types';

/** Icon name for a skill, matching the keys SkillIcon understands. */
export const SKILL_ICON: Record<SkillKey, string> = {
  SAFETY: 'shield',
  COMMUNICATION: 'message',
  EMPATHY: 'heart',
  CONFIDENCE: 'star',
};

/**
 * Formats a timestamp the way a parent reads it: "Today, 6:12 PM" for recent
 * activity, a weekday within the last week, and a date beyond that.
 */
export function formatWhen(iso: string): string {
  const date = new Date(iso);

  if (Number.isNaN(date.getTime())) return '';

  const time = date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });

  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);

  const daysAgo = Math.floor((startOfToday.getTime() - date.getTime()) / 86_400_000) + 1;

  if (date >= startOfToday) return `Today, ${time}`;
  if (daysAgo === 1) return `Yesterday, ${time}`;
  if (daysAgo < 7) return `${date.toLocaleDateString([], { weekday: 'short' })}, ${time}`;

  return date.toLocaleDateString([], { month: 'short', day: 'numeric' });
}

export function formatMinutes(minutes: number): string {
  if (minutes < 60) return `${minutes}m`;

  return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}

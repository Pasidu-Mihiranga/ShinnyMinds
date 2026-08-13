import { env } from '../config/env.js';

/**
 * Day-boundary helpers.
 *
 * "Today" and "this week" have to mean the same thing everywhere, and UTC is not it:
 * for a family at UTC+5:30, a UTC day begins at 5:30am local, so an early-morning
 * session would be counted against the previous day. Every boundary is therefore
 * computed against APP_TIMEZONE_OFFSET_MINUTES - at write time when a daily snapshot
 * is keyed, and at read time when the dashboard asks for today or this week - so the
 * two can never disagree.
 */

const MINUTE_MS = 60_000;
const DAY_MS = 86_400_000;

function offsetOrDefault(offsetMinutes?: number): number {
  return offsetMinutes ?? env.APP_TIMEZONE_OFFSET_MINUTES;
}

/**
 * The instant at which the local day containing `date` began.
 * Use this to filter timestamp columns, e.g. sessions started today.
 */
export function startOfLocalDay(date: Date, offsetMinutes?: number): Date {
  const offset = offsetOrDefault(offsetMinutes) * MINUTE_MS;

  const shifted = new Date(date.getTime() + offset);

  const localMidnight = Date.UTC(
    shifted.getUTCFullYear(),
    shifted.getUTCMonth(),
    shifted.getUTCDate(),
  );

  return new Date(localMidnight - offset);
}

/**
 * The local calendar date of `date`, as a Date at UTC midnight.
 *
 * This is what goes in a DATE column: it identifies a day, not an instant. Keeping it
 * distinct from startOfLocalDay is what stops snapshots being filed under the wrong day.
 */
export function localDayKey(date: Date, offsetMinutes?: number): Date {
  const shifted = new Date(date.getTime() + offsetOrDefault(offsetMinutes) * MINUTE_MS);

  return new Date(
    Date.UTC(shifted.getUTCFullYear(), shifted.getUTCMonth(), shifted.getUTCDate()),
  );
}

export function addDays(date: Date, days: number): Date {
  return new Date(date.getTime() + days * DAY_MS);
}

/** The last `count` local calendar days ending today, oldest first, as day keys. */
export function lastNLocalDays(count: number, now = new Date(), offsetMinutes?: number): Date[] {
  const today = localDayKey(now, offsetMinutes);

  return Array.from({ length: count }, (_, index) => addDays(today, index - (count - 1)));
}

/** Stable "YYYY-MM-DD" for a day key, used to match snapshots to chart columns. */
export function toDayKey(date: Date): string {
  return date.toISOString().slice(0, 10);
}

// Day keys are UTC-midnight stand-ins for a local date, so they must be formatted in
// UTC. Formatting them locally would shift them back by the offset and name the wrong day.
export function formatDayLabel(dayKey: Date): string {
  return dayKey.toLocaleDateString('en-US', { weekday: 'short', timeZone: 'UTC' });
}

export function formatRange(fromDayKey: Date, toDayKey_: Date): string {
  const options: Intl.DateTimeFormatOptions = {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  };

  return `${fromDayKey.toLocaleDateString('en-US', options)} – ${toDayKey_.toLocaleDateString('en-US', options)}`;
}

/** Date helpers shared by the analytics queries. All values are UTC. */

export function startOfUtcDay(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
}

export function addDays(date: Date, days: number): Date {
  const next = new Date(date);
  next.setUTCDate(next.getUTCDate() + days);

  return next;
}

/** The last `count` days ending today, oldest first. */
export function lastNDays(count: number, today = new Date()): Date[] {
  const end = startOfUtcDay(today);

  return Array.from({ length: count }, (_, index) => addDays(end, index - (count - 1)));
}

export function toDayKey(date: Date): string {
  return startOfUtcDay(date).toISOString().slice(0, 10);
}

export function formatDayLabel(date: Date): string {
  return date.toLocaleDateString('en-US', { weekday: 'short', timeZone: 'UTC' });
}

export function formatRange(from: Date, to: Date): string {
  const options: Intl.DateTimeFormatOptions = {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  };

  return `${from.toLocaleDateString('en-US', options)} – ${to.toLocaleDateString('en-US', options)}`;
}

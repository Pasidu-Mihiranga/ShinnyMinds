import type { Child } from '../api/types';

/**
 * A child's avatar. Falls back to their initials on a tinted circle, so the dashboard
 * works before any avatar images exist rather than showing broken images.
 */
export default function ChildAvatar({
  child,
  size = 56,
}: {
  child: Pick<Child, 'displayName' | 'avatarUrl'>;
  size?: number;
}) {
  if (child.avatarUrl) {
    return (
      <img
        src={child.avatarUrl}
        alt={child.displayName}
        className="rounded-full object-cover ring-2 ring-teal-300"
        style={{ width: size, height: size }}
      />
    );
  }

  return (
    <div
      className="rounded-full grid place-items-center ring-2 ring-teal-300 bg-teal-50 text-teal-700 font-extrabold"
      style={{ width: size, height: size, fontSize: size * 0.38 }}
    >
      {initials(child.displayName)}
    </div>
  );
}

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');
}

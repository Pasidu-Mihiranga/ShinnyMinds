import { ShieldIcon, MessageIcon, HeartIcon, StarIcon, UsersIcon } from './icons';

const map: Record<string, React.ComponentType<React.SVGProps<SVGSVGElement>>> = {
  shield: ShieldIcon,
  message: MessageIcon,
  heart: HeartIcon,
  star: StarIcon,
  users: UsersIcon,
};

export default function SkillIcon({
  icon,
  color,
  size = 20,
}: {
  icon: string;
  color: string;
  size?: number;
}) {
  const Icon = map[icon] ?? StarIcon;
  return (
    <div
      className="flex items-center justify-center rounded-xl shrink-0"
      style={{ width: size + 20, height: size + 20, background: `${color}1A`, color }}
    >
      <Icon width={size} height={size} />
    </div>
  );
}

import { NavLink } from 'react-router-dom';
import { HomeIcon, InsightsIcon, ChatIcon, SettingsIcon } from './icons';

const tabs = [
  { to: '/', label: 'Home', Icon: HomeIcon },
  { to: '/insights', label: 'Insights', Icon: InsightsIcon },
  { to: '/assistant', label: 'Assistant', Icon: ChatIcon },
  { to: '/settings', label: 'Settings', Icon: SettingsIcon },
];

export default function BottomNav() {
  return (
    <nav className="shrink-0 border-t border-slate-100 bg-white/95 backdrop-blur px-2 pt-2 pb-[max(0.5rem,env(safe-area-inset-bottom))]">
      <div className="flex items-stretch justify-between">
        {tabs.map(({ to, label, Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              `flex flex-1 flex-col items-center gap-1 py-1.5 text-[11px] font-medium transition-colors ${
                isActive ? 'text-violet-600' : 'text-slate-400'
              }`
            }
          >
            <Icon width={22} height={22} />
            {label}
          </NavLink>
        ))}
      </div>
    </nav>
  );
}

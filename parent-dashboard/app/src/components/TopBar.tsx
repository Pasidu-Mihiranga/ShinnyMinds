import { useAuth } from '../auth/AuthContext';
import { BellIcon } from './icons';

export default function TopBar() {
  const { parent } = useAuth();

  return (
    <div className="flex items-center justify-between px-5 pb-3 border-b border-slate-100 shrink-0 pt-[max(0.75rem,env(safe-area-inset-top))]">
      <div className="flex items-center gap-2">
        <img src="/images/logo.svg" alt="" className="w-7 h-7 rounded-lg" />
        <span className="text-[17px] font-bold text-slate-900">
          Shinyminds <span className="font-medium text-slate-500">Parent</span>
        </span>
      </div>
      <div className="flex items-center gap-3">
        <button className="text-slate-500 hover:text-slate-700" aria-label="Notifications">
          <BellIcon />
        </button>
        {/* Initials rather than a stock avatar: parents have no uploaded photo yet. */}
        <div
          className="w-9 h-9 rounded-full bg-violet-100 text-violet-700 grid place-items-center text-sm font-extrabold"
          title={parent?.displayName ?? ''}
        >
          {(parent?.displayName ?? '?').trim().charAt(0).toUpperCase()}
        </div>
      </div>
    </div>
  );
}

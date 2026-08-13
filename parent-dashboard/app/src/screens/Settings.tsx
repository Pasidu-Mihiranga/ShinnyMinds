import { useState } from 'react';
import ChildAvatar from '../components/ChildAvatar';
import { useAuth } from '../auth/AuthContext';
import { CheckCircleIcon, ChevronRightIcon, LogoutIcon, UserIcon } from '../components/icons';

export default function Settings() {
  const { parent, children, selectedChild, selectChild, signOut } = useAuth();

  const [copied, setCopied] = useState(false);

  async function copyLinkCode() {
    if (!parent?.linkCode) return;

    try {
      await navigator.clipboard.writeText(parent.linkCode);

      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard access is blocked outside a secure context; the code is on screen
      // anyway, so there is nothing to recover from.
    }
  }

  return (
    <div className="flex flex-col">
      <div className="px-5 pb-24 space-y-5 pt-[max(1.25rem,env(safe-area-inset-top))]">
        <h1 className="text-[26px] font-extrabold text-slate-900">Settings</h1>

        <div className="rounded-2xl bg-white border border-slate-100 shadow-sm p-4 flex items-center gap-4">
          <div className="w-14 h-14 rounded-full bg-violet-100 text-violet-700 grid place-items-center text-xl font-extrabold">
            {(parent?.displayName ?? '?').trim().charAt(0).toUpperCase()}
          </div>
          <div className="min-w-0">
            <div className="font-bold text-slate-900 text-[16px] truncate">
              {parent?.displayName}
            </div>
            <div className="text-sm text-slate-500 truncate">{parent?.email}</div>
          </div>
        </div>

        {/* The link code is what connects a child's game account to this dashboard,
            so it is given its own card rather than buried in a submenu. */}
        <div className="rounded-2xl bg-violet-50 border border-violet-100 p-4 space-y-2">
          <div className="text-[13px] font-bold text-violet-700">Parent code</div>
          <p className="text-[13px] text-slate-600 leading-relaxed">
            Your child enters this in the game to link their player to your dashboard.
          </p>
          <button
            onClick={() => void copyLinkCode()}
            className="w-full rounded-xl bg-white border border-violet-200 py-3 flex items-center justify-center gap-2"
          >
            <span className="text-xl font-extrabold tracking-[0.3em] text-violet-700">
              {parent?.linkCode}
            </span>
            {copied && <CheckCircleIcon width={16} height={16} className="text-emerald-600" />}
          </button>
          <p className="text-[11px] text-slate-400 text-center">
            {copied ? 'Copied to clipboard' : 'Tap to copy'}
          </p>
        </div>

        <div className="rounded-2xl bg-white border border-slate-100 shadow-sm overflow-hidden">
          <div className="px-4 pt-4 pb-1 text-[13px] font-bold text-violet-600">
            Children ({children.length})
          </div>

          {children.length === 0 && (
            <p className="px-4 pb-4 pt-2 text-[13.5px] text-slate-500 leading-relaxed">
              No child has used your parent code yet.
            </p>
          )}

          {children.map((child, index) => (
            <button
              key={child.id}
              onClick={() => selectChild(child.id)}
              className={`w-full flex items-center gap-3 px-4 py-3.5 text-left ${
                index !== children.length - 1 ? 'border-b border-slate-100' : ''
              }`}
            >
              <ChildAvatar child={child} size={40} />
              <div className="flex-1 min-w-0">
                <div className="text-[14.5px] font-semibold text-slate-800 truncate">
                  {child.displayName}
                </div>
                <div className="text-xs text-slate-400">
                  @{child.username}
                  {child.age !== null && ` · Age ${child.age}`} · Wellbeing {child.overallScore}
                </div>
              </div>
              {selectedChild?.id === child.id ? (
                <CheckCircleIcon width={18} height={18} className="text-violet-600" />
              ) : (
                <ChevronRightIcon className="text-slate-300" />
              )}
            </button>
          ))}
        </div>

        <div className="rounded-2xl bg-white border border-slate-100 shadow-sm overflow-hidden">
          <div className="px-4 pt-4 pb-1 text-[13px] font-bold text-violet-600">About</div>
          <div className="px-4 py-3.5 flex items-center gap-3">
            <UserIcon width={20} height={20} className="text-slate-500" />
            <span className="flex-1 text-[14.5px] font-medium text-slate-800">
              Shinyminds Parent
            </span>
            <span className="text-[13px] text-slate-400">v1.0</span>
          </div>
        </div>

        <button
          onClick={() => void signOut()}
          className="w-full flex items-center justify-center gap-2 rounded-2xl bg-rose-50 py-3.5 font-bold text-rose-500 text-[15px]"
        >
          <LogoutIcon width={19} height={19} />
          Log Out
        </button>
      </div>
    </div>
  );
}

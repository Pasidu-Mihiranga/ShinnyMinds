import { useCallback } from 'react';
import TopBar from '../components/TopBar';
import ProgressRing from '../components/ProgressRing';
import SkillIcon from '../components/SkillIcon';
import ChildAvatar from '../components/ChildAvatar';
import { EmptyCard, ErrorCard, LoadingCard, NoChildCard } from '../components/StateViews';
import { LightbulbIcon, CalendarIcon } from '../components/icons';
import { api } from '../api/client';
import { useApiData } from '../hooks/useApiData';
import { useAuth } from '../auth/AuthContext';
import { SKILL_ICON, formatWhen } from '../lib/format';

export default function Home() {
  const { parent, selectedChild } = useAuth();
  const childId = selectedChild?.id ?? null;

  const overview = useApiData(
    useCallback(() => api.overview(childId as string), [childId]),
    [childId],
    { enabled: Boolean(childId) },
  );

  const activity = useApiData(
    useCallback(() => api.activity(childId as string, 3), [childId]),
    [childId],
    { enabled: Boolean(childId) },
  );

  // A parent with no linked child sees the code to share, not an empty dashboard.
  if (!selectedChild) {
    return (
      <div className="flex flex-col">
        <TopBar />
        <div className="px-5 pt-6 pb-24">
          <NoChildCard linkCode={parent?.linkCode ?? '……'} />
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col">
      <TopBar />
      <div className="px-5 pt-5 pb-24 space-y-5">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-[22px] font-extrabold text-slate-900">
              Hello, {parent?.displayName}! 👋
            </h1>
            <p className="text-slate-500 text-sm mt-0.5">
              Let's see how {selectedChild.displayName} is growing.
            </p>
          </div>
          <div className="flex flex-col items-center gap-1">
            <ChildAvatar child={selectedChild} size={56} />
            <div className="text-center leading-none">
              <div className="text-sm font-bold text-slate-900">{selectedChild.displayName}</div>
              {selectedChild.age !== null && (
                <div className="text-xs text-slate-400">Age {selectedChild.age}</div>
              )}
            </div>
          </div>
        </div>

        {overview.loading && <LoadingCard label="Loading progress…" />}
        {overview.error && <ErrorCard message={overview.error} onRetry={overview.reload} />}

        {/* Before any mission is played every skill sits at a neutral 50. Showing the
            ring here would present that placeholder as a real 50% result. */}
        {overview.data && !overview.data.hasPlayed && (
          <EmptyCard
            title={`${selectedChild.displayName} hasn't played yet`}
            body="Wellbeing and skill scores appear as soon as they finish their first mission in the game."
          />
        )}

        {overview.data?.hasPlayed && (
          <>
            <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-5">
              <h2 className="text-[15px] font-bold text-slate-900 mb-3">Overall Wellbeing</h2>
              <div className="flex items-center gap-5">
                <ProgressRing
                  value={overview.data.overallWellbeing.score}
                  size={130}
                  stroke={12}
                  label={`${overview.data.overallWellbeing.score}%`}
                  sublabel={overview.data.overallWellbeing.label}
                />
                <div className="flex-1 space-y-3">
                  {overview.data.skills.map((s) => (
                    <div key={s.key} className="flex items-center gap-2.5">
                      <SkillIcon icon={s.icon} color={s.color} size={16} />
                      <span className="flex-1 text-[13.5px] font-medium text-slate-700">
                        {s.label}
                      </span>
                      <span className="text-[13.5px] font-bold" style={{ color: s.color }}>
                        {s.score}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-5">
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-1.5 text-[15px] font-bold text-slate-900">
                  This week <CalendarIcon width={16} height={16} className="text-slate-400" />
                </div>
                <span className="text-xs text-slate-400">{overview.data.weekSummary.range}</span>
              </div>
              <div className="grid grid-cols-3 text-center divide-x divide-slate-100">
                <Stat
                  value={overview.data.weekSummary.scenariosCompleted}
                  label={
                    <>
                      Scenarios
                      <br />
                      Completed
                    </>
                  }
                />
                <Stat
                  value={overview.data.weekSummary.skillsPracticed}
                  label={
                    <>
                      Skills
                      <br />
                      Practiced
                    </>
                  }
                />
                <div>
                  <div className="text-2xl font-extrabold text-violet-600">
                    {overview.data.weekSummary.screenTimePerDayMin}
                    <span className="text-sm font-semibold text-slate-500"> mins</span>
                  </div>
                  <div className="text-xs text-slate-500 mt-1 leading-tight">
                    Screen time
                    <br />/ day
                  </div>
                </div>
              </div>
            </div>
          </>
        )}

        <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-5">
          <h2 className="text-[15px] font-bold text-slate-900 mb-3">Recent Activity</h2>

          {activity.loading && <p className="text-sm text-slate-400">Loading…</p>}
          {activity.error && <p className="text-sm text-rose-600">{activity.error}</p>}

          {activity.data?.activity.length === 0 && (
            <p className="text-sm text-slate-500 leading-relaxed">
              {selectedChild.displayName} hasn't played a mission yet. Progress appears here after
              their first one.
            </p>
          )}

          <div className="space-y-3">
            {activity.data?.activity.map((a) => (
              <div key={a.id} className="flex items-center gap-3">
                <SkillIcon icon={SKILL_ICON[a.skill]} color={a.color} size={26} />
                <div className="flex-1 min-w-0">
                  <div className="font-bold text-slate-900 text-[14px] truncate">{a.title}</div>
                  <div className="text-xs text-slate-500 truncate">Focused on: {a.focus}</div>
                  <div className="text-xs text-slate-400">{formatWhen(a.when)}</div>
                </div>
                <span
                  className={`text-[11px] font-semibold px-2.5 py-1 rounded-full whitespace-nowrap ${
                    a.status === 'Completed'
                      ? 'text-emerald-700 bg-emerald-50'
                      : 'text-amber-700 bg-amber-50'
                  }`}
                >
                  {a.status === 'Completed' && a.score !== null
                    ? `${a.score}/${a.maxScore}`
                    : a.status}
                </span>
              </div>
            ))}
          </div>
        </div>

        {overview.data && (
          <div className="rounded-3xl bg-violet-50 border border-violet-100 p-4 flex gap-3">
            <div className="text-violet-600 shrink-0 mt-0.5">
              <LightbulbIcon width={20} height={20} />
            </div>
            <div>
              <div className="font-bold text-slate-900 text-[14px] mb-0.5">AI Assistant Tip</div>
              <p className="text-[13px] text-slate-600 leading-snug">{overview.data.aiTip}</p>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function Stat({ value, label }: { value: number; label: React.ReactNode }) {
  return (
    <div>
      <div className="text-2xl font-extrabold text-slate-900">{value}</div>
      <div className="text-xs text-slate-500 mt-1 leading-tight">{label}</div>
    </div>
  );
}

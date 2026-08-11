import { useCallback, useState } from 'react';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts';
import SkillIcon from '../components/SkillIcon';
import { EmptyCard, ErrorCard, LoadingCard, NoChildCard } from '../components/StateViews';
import { api } from '../api/client';
import { useApiData } from '../hooks/useApiData';
import { useAuth } from '../auth/AuthContext';
import { SKILL_ICON, formatWhen } from '../lib/format';
import type { ActivityItem, Insights as InsightsData, Overview, SkillsProgress } from '../api/types';

const tabs = ['Progress', 'Skills', 'Activity'] as const;
type Tab = (typeof tabs)[number];

export default function Insights() {
  const { parent, selectedChild } = useAuth();
  const childId = selectedChild?.id ?? null;

  const [tab, setTab] = useState<Tab>('Progress');

  const overview = useApiData(
    useCallback(() => api.overview(childId as string), [childId]),
    [childId],
  );
  const progress = useApiData(
    useCallback(() => api.skillsProgress(childId as string, 7), [childId]),
    [childId],
  );
  const insights = useApiData(
    useCallback(() => api.insights(childId as string), [childId]),
    [childId],
  );
  const activity = useApiData(
    useCallback(() => api.activity(childId as string, 20), [childId]),
    [childId],
  );

  if (!selectedChild) {
    return (
      <div className="px-5 pt-8 pb-24">
        <NoChildCard linkCode={parent?.linkCode ?? '……'} />
      </div>
    );
  }

  const loading = overview.loading || progress.loading || insights.loading;
  const error = overview.error ?? progress.error ?? insights.error;

  return (
    <div className="flex flex-col">
      <div className="px-5 pb-3 shrink-0 pt-[max(1.25rem,env(safe-area-inset-top))]">
        <h1 className="text-[26px] font-extrabold text-slate-900 mb-4">Insights</h1>
        <div className="flex bg-slate-100 rounded-2xl p-1 gap-1">
          {tabs.map((t) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={`flex-1 py-2 rounded-xl text-[14px] font-semibold transition-colors ${
                tab === t ? 'bg-white text-violet-600 shadow-sm' : 'text-slate-500'
              }`}
            >
              {t}
            </button>
          ))}
        </div>
      </div>

      <div className="px-5 pb-24 space-y-5">
        <div className="flex items-center justify-between">
          <span className="text-[13px] font-semibold text-slate-700">This week</span>
          <span className="text-xs text-slate-400">{overview.data?.weekSummary.range ?? ''}</span>
        </div>

        {loading && <LoadingCard label="Loading insights…" />}
        {!loading && error && <ErrorCard message={error} onRetry={overview.reload} />}

        {!loading && !error && overview.data && progress.data && insights.data && (
          <>
            {tab === 'Progress' && (
              <ProgressTab
                overview={overview.data}
                progress={progress.data}
                insights={insights.data}
                activity={activity.data?.activity ?? []}
              />
            )}
            {tab === 'Skills' && <SkillsTab overview={overview.data} />}
            {tab === 'Activity' && (
              <ActivityBlock items={activity.data?.activity ?? []} loading={activity.loading} showAll />
            )}
          </>
        )}
      </div>
    </div>
  );
}

function ProgressTab({
  overview,
  progress,
  insights,
  activity,
}: {
  overview: Overview;
  progress: SkillsProgress;
  insights: InsightsData;
  activity: ActivityItem[];
}) {
  // Strengths are the top three skills by score, so the list reflects this child
  // rather than a fixed order.
  const strengths = [...overview.skills].sort((a, b) => b.score - a.score).slice(0, 3);

  return (
    <>
      <div className="grid grid-cols-4 gap-2">
        <StatTile
          icon={<RingMini value={overview.overallWellbeing.score} />}
          big={`${overview.overallWellbeing.score}%`}
          sub="Wellbeing"
        />
        <StatTile icon={<Dot bg="bg-slate-100" fg="text-slate-400" glyph="⏱" />} big={`${overview.weekSummary.screenTimeTodayMin}m`} sub="Today" />
        <StatTile icon={<Dot bg="bg-emerald-50" fg="text-emerald-600" glyph="✓" />} big={overview.weekSummary.scenariosCompleted} sub="This week" />
        <StatTile icon={<Dot bg="bg-blue-50" fg="text-blue-600" glyph="↗" />} big={`${overview.weekSummary.screenTimePerDayMin}m`} sub="Daily avg" />
      </div>

      <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-5">
        <h2 className="text-[15px] font-bold text-slate-900 mb-2">Skills Progress</h2>

        <div className="h-[220px] -ml-4">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={toChartRows(progress)} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
              <CartesianGrid vertical={false} stroke="#f1f0f5" />
              <XAxis dataKey="day" tick={{ fontSize: 11, fill: '#94a3b8' }} axisLine={false} tickLine={false} />
              <YAxis domain={[0, 100]} tick={{ fontSize: 11, fill: '#94a3b8' }} axisLine={false} tickLine={false} width={28} />
              <Tooltip contentStyle={{ borderRadius: 12, fontSize: 12, border: '1px solid #eee' }} />
              {progress.series.map((s) => (
                <Line
                  key={s.key}
                  type="monotone"
                  dataKey={s.key}
                  name={s.label}
                  stroke={s.color}
                  strokeWidth={2.5}
                  dot={{ r: 3, fill: s.color, strokeWidth: 0 }}
                  activeDot={{ r: 5 }}
                  // Days before this child first played have no score at all; skipping
                  // them draws a gap instead of a line down to zero.
                  connectNulls
                />
              ))}
            </LineChart>
          </ResponsiveContainer>
        </div>

        <div className="grid grid-cols-2 gap-2 mt-2">
          {overview.skills.map((s) => (
            <div key={s.key} className="flex items-center gap-2 text-[12.5px]">
              <span className="w-2.5 h-2.5 rounded-full shrink-0" style={{ background: s.color }} />
              <span className="text-slate-600 flex-1 truncate">{s.label}</span>
              <span className="font-bold" style={{ color: s.color }}>
                {s.score}
              </span>
            </div>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-4">
          <h2 className="text-[14px] font-bold text-slate-900 mb-3">Weekly Completions</h2>

          {insights.weeklyCompletions.total === 0 ? (
            <p className="text-[12px] text-slate-400">No missions completed this week.</p>
          ) : (
            <div className="flex flex-col items-center">
              <Donut data={insights.weeklyCompletions.breakdown} total={insights.weeklyCompletions.total} />
              <div className="w-full mt-3 space-y-1.5">
                {insights.weeklyCompletions.breakdown.map((b) => (
                  <div key={b.key} className="flex items-center gap-1.5 text-[11px]">
                    <span className="w-2 h-2 rounded-full shrink-0" style={{ background: b.color }} />
                    <span className="text-slate-500 flex-1 truncate">{b.label}</span>
                    <span className="font-semibold text-slate-700">
                      {b.count} ({b.pct}%)
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-4">
          <h2 className="text-[14px] font-bold text-slate-900 mb-3">Strengths</h2>
          <div className="space-y-3">
            {strengths.map((s) => (
              <div key={s.key} className="flex items-center gap-2">
                <SkillIcon icon={s.icon} color={s.color} size={14} />
                <div className="flex-1 min-w-0">
                  <div className="text-[12.5px] font-bold text-slate-900 truncate">{s.label}</div>
                  <div className="text-[10.5px] text-slate-400 truncate">{s.note}</div>
                </div>
                <span className="text-[13px] font-bold" style={{ color: s.color }}>
                  {s.score}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-4">
        <h2 className="text-[15px] font-bold text-slate-900 mb-3">Needs Attention</h2>

        {insights.needsAttention.length === 0 ? (
          <p className="text-[13px] text-slate-500">
            Every skill is above the attention threshold. Nothing to worry about this week.
          </p>
        ) : (
          <div className="grid grid-cols-2 gap-3">
            {insights.needsAttention.map((n) => (
              <div key={n.key} className="flex items-start gap-2.5 rounded-2xl bg-slate-50 p-3">
                <SkillIcon icon={n.icon} color={n.color} size={16} />
                <div className="min-w-0">
                  <div className="flex items-baseline gap-1.5">
                    <span className="font-bold text-slate-900 text-[13.5px]">{n.label}</span>
                    <span className="font-bold text-[13.5px]" style={{ color: n.color }}>
                      {n.score}
                    </span>
                  </div>
                  <div className="text-[11px] text-slate-500 leading-snug">{n.note}</div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <ActivityBlock items={activity.slice(0, 3)} loading={false} />
    </>
  );
}

function SkillsTab({ overview }: { overview: Overview }) {
  return (
    <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-5 space-y-4">
      <h2 className="text-[15px] font-bold text-slate-900">Skill Breakdown</h2>
      {overview.skills.map((s) => (
        <div key={s.key}>
          <div className="flex items-center gap-2.5 mb-1.5">
            <SkillIcon icon={s.icon} color={s.color} size={16} />
            <span className="flex-1 font-semibold text-slate-800 text-[14px]">{s.label}</span>
            <span className="font-bold text-[14px]" style={{ color: s.color }}>
              {s.score}
            </span>
          </div>
          <div className="h-2.5 rounded-full bg-slate-100 overflow-hidden">
            <div className="h-full rounded-full" style={{ width: `${s.score}%`, background: s.color }} />
          </div>
          <div className="text-[12px] text-slate-400 mt-1">{s.note}</div>
        </div>
      ))}
    </div>
  );
}

function ActivityBlock({
  items,
  loading,
  showAll = false,
}: {
  items: ActivityItem[];
  loading: boolean;
  showAll?: boolean;
}) {
  if (loading) return <LoadingCard label="Loading activity…" />;

  if (items.length === 0) {
    return (
      <EmptyCard
        title="No activity yet"
        body="Missions your child plays will be listed here, with the score they earned."
      />
    );
  }

  return (
    <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-5">
      <h2 className="text-[15px] font-bold text-slate-900 mb-3">
        {showAll ? 'All Activity' : 'Recent Activity'}
      </h2>
      <div className="space-y-3">
        {items.map((a) => (
          <div key={a.id} className="flex items-center gap-3">
            <SkillIcon icon={SKILL_ICON[a.skill]} color={a.color} size={26} />
            <div className="flex-1 min-w-0">
              <div className="font-bold text-slate-900 text-[14px] truncate">{a.title}</div>
              <div className="text-xs text-slate-500 truncate">Focused on: {a.focus}</div>
              <div className="text-xs text-slate-400">{formatWhen(a.when)}</div>
            </div>
            <span
              className={`text-[11px] font-semibold px-2.5 py-1 rounded-full whitespace-nowrap ${
                a.status === 'Completed' ? 'text-emerald-700 bg-emerald-50' : 'text-amber-700 bg-amber-50'
              }`}
            >
              {a.status === 'Completed' && a.score !== null ? `${a.score}/${a.maxScore}` : a.status}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

function toChartRows(progress: SkillsProgress) {
  return progress.days.map((day, index) => {
    const row: Record<string, string | number | null> = { day };

    progress.series.forEach((series) => {
      row[series.key] = series.values[index] ?? null;
    });

    return row;
  });
}

function StatTile({ icon, big, sub }: { icon: React.ReactNode; big: React.ReactNode; sub: string }) {
  return (
    <div className="flex-1 rounded-2xl bg-white border border-slate-100 shadow-sm p-3 text-center">
      <div className="flex justify-center mb-1.5">{icon}</div>
      <div className="text-lg font-extrabold text-slate-900 leading-none">{big}</div>
      <div className="text-[11px] text-slate-500 mt-1 leading-tight">{sub}</div>
    </div>
  );
}

function Dot({ bg, fg, glyph }: { bg: string; fg: string; glyph: string }) {
  return (
    <div className={`w-[34px] h-[34px] rounded-full ${bg} ${fg} flex items-center justify-center text-[16px]`}>
      {glyph}
    </div>
  );
}

function RingMini({ value }: { value: number }) {
  const r = 14;
  const c = 2 * Math.PI * r;

  return (
    <svg width={34} height={34} className="-rotate-90">
      <circle cx={17} cy={17} r={r} fill="none" stroke="#eceafc" strokeWidth={4} />
      <circle
        cx={17}
        cy={17}
        r={r}
        fill="none"
        stroke="#7c3aed"
        strokeWidth={4}
        strokeLinecap="round"
        strokeDasharray={c}
        strokeDashoffset={c - (value / 100) * c}
      />
    </svg>
  );
}

function Donut({
  data,
  total,
}: {
  data: { key: string; pct: number; color: string }[];
  total: number;
}) {
  const r = 40;
  const c = 2 * Math.PI * r;
  let offset = 0;

  return (
    <div className="relative" style={{ width: 110, height: 110 }}>
      <svg width={110} height={110} className="-rotate-90">
        <circle cx={55} cy={55} r={r} fill="none" stroke="#f1f0f5" strokeWidth={14} />
        {data.map((slice) => {
          const length = (slice.pct / 100) * c;
          const element = (
            <circle
              key={slice.key}
              cx={55}
              cy={55}
              r={r}
              fill="none"
              stroke={slice.color}
              strokeWidth={14}
              strokeDasharray={`${length} ${c - length}`}
              strokeDashoffset={-offset}
            />
          );

          offset += length;

          return element;
        })}
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-xl font-extrabold text-slate-900 leading-none">{total}</span>
        <span className="text-[10px] text-slate-400">Total</span>
      </div>
    </div>
  );
}

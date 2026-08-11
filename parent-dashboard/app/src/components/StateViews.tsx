/**
 * The three states every data-backed screen has to handle. Sharing them keeps
 * loading and failure looking the same everywhere instead of each screen inventing
 * its own spinner.
 */

export function LoadingCard({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-8 flex flex-col items-center gap-3">
      <div className="w-8 h-8 rounded-full border-[3px] border-violet-200 border-t-violet-600 animate-spin" />
      <p className="text-sm text-slate-500">{label}</p>
    </div>
  );
}

export function ErrorCard({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="rounded-3xl bg-white border border-rose-100 shadow-sm p-6 text-center space-y-3">
      <div className="w-10 h-10 rounded-full bg-rose-50 text-rose-500 grid place-items-center mx-auto text-xl font-bold">
        !
      </div>
      <p className="text-sm text-slate-600">{message}</p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="text-sm font-semibold text-violet-600 hover:text-violet-700"
        >
          Try again
        </button>
      )}
    </div>
  );
}

export function EmptyCard({ title, body }: { title: string; body: string }) {
  return (
    <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-6 text-center space-y-1.5">
      <p className="text-[15px] font-bold text-slate-900">{title}</p>
      <p className="text-sm text-slate-500 leading-relaxed">{body}</p>
    </div>
  );
}

/** Shown when a parent has signed up but no child has entered their link code yet. */
export function NoChildCard({ linkCode }: { linkCode: string }) {
  return (
    <div className="rounded-3xl bg-white border border-slate-100 shadow-sm p-6 text-center space-y-3">
      <p className="text-[17px] font-extrabold text-slate-900">No child linked yet</p>
      <p className="text-sm text-slate-500 leading-relaxed">
        Ask your child to open ShinyMinds, create their player, and enter this code when it asks
        for a parent code.
      </p>
      <div className="inline-block rounded-2xl bg-violet-50 px-6 py-3">
        <span className="text-2xl font-extrabold tracking-[0.3em] text-violet-700">{linkCode}</span>
      </div>
      <p className="text-xs text-slate-400">Their progress appears here as soon as they play.</p>
    </div>
  );
}

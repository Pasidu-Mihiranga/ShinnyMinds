import { useCallback, useEffect, useRef, useState } from 'react';
import ChildAvatar from '../components/ChildAvatar';
import SkillIcon from '../components/SkillIcon';
import { ErrorCard, LoadingCard, NoChildCard } from '../components/StateViews';
import { LightbulbIcon, TrendingUpIcon, StarIcon, SendIcon } from '../components/icons';
import { ApiError, api } from '../api/client';
import { useApiData } from '../hooks/useApiData';
import { useAuth } from '../auth/AuthContext';
import type { ChatMessage } from '../api/types';

const suggestions = [
  { Icon: LightbulbIcon, label: 'What should I discuss today?' },
  { Icon: TrendingUpIcon, label: 'Show weak areas' },
  { Icon: StarIcon, label: 'How can I build their confidence?' },
];

/**
 * The parent assistant.
 *
 * Messages go to the backend, which grounds the model in this child's real recorded
 * scores before replying. Nothing is generated in the browser and no API key is
 * present here.
 */
export default function Assistant() {
  const { parent, selectedChild } = useAuth();
  const childId = selectedChild?.id ?? null;

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  const history = useApiData(
    useCallback(() => api.chat.history(childId as string), [childId]),
    [childId],
  );

  const overview = useApiData(
    useCallback(() => api.overview(childId as string), [childId]),
    [childId],
  );

  useEffect(() => {
    if (history.data) setMessages(history.data.messages);
  }, [history.data]);

  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, sending]);

  async function send(text: string) {
    const trimmed = text.trim();

    if (!trimmed || sending || !childId) return;

    setInput('');
    setSendError(null);
    setSending(true);

    // The parent's own message is shown straight away; only the reply has to wait
    // on the network, so the conversation never feels frozen.
    const pending: ChatMessage = {
      id: `pending-${Date.now()}`,
      from: 'parent',
      text: trimmed,
      createdAt: new Date().toISOString(),
    };

    setMessages((current) => [...current, pending]);

    try {
      const result = await api.chat.send(childId, trimmed);

      setMessages((current) => [
        ...current.filter((message) => message.id !== pending.id),
        result.parentMessage,
        result.aiMessage,
      ]);
    } catch (cause) {
      setSendError(
        cause instanceof ApiError ? cause.message : 'Could not reach the assistant. Please try again.',
      );

      setMessages((current) => current.filter((message) => message.id !== pending.id));
      setInput(trimmed);
    } finally {
      setSending(false);
    }
  }

  if (!selectedChild) {
    return (
      <div className="px-5 pt-8 pb-24">
        <NoChildCard linkCode={parent?.linkCode ?? '……'} />
      </div>
    );
  }

  return (
    <div className="flex flex-col min-h-full">
      <div className="px-5 pb-4 border-b border-slate-100 pt-[max(1.25rem,env(safe-area-inset-top))]">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h1 className="text-[22px] font-extrabold text-slate-900">AI Assistant</h1>
            <p className="text-slate-500 text-sm mt-0.5">
              Support for {selectedChild.displayName}'s growth
            </p>
          </div>
          <ChildAvatar child={selectedChild} size={48} />
        </div>

        {overview.data && (
          <div className="rounded-2xl bg-white border border-slate-100 shadow-sm p-4 flex items-center gap-4">
            <div className="text-center shrink-0">
              <div className="text-[11px] font-semibold text-slate-500 mb-0.5">Wellbeing</div>
              <div className="text-2xl font-extrabold text-violet-600 leading-none">
                {overview.data.overallWellbeing.score}
              </div>
            </div>
            <div className="flex-1 grid grid-cols-2 gap-y-1.5 gap-x-2">
              {overview.data.skills.map((s) => (
                <div key={s.key} className="flex items-center gap-1.5 text-[11.5px]">
                  <SkillIcon icon={s.icon} color={s.color} size={12} />
                  <span className="text-slate-600 flex-1 truncate">{s.label}</span>
                  <span className="font-bold" style={{ color: s.color }}>
                    {s.score}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      <div className="flex-1 px-5 py-4 space-y-3">
        {history.loading && <LoadingCard label="Loading your conversation…" />}
        {history.error && <ErrorCard message={history.error} onRetry={history.reload} />}

        {!history.loading && !history.error && messages.length === 0 && (
          <div className="rounded-2xl bg-violet-50 border border-violet-100 p-4">
            <p className="text-[13.5px] text-slate-700 leading-relaxed">
              Hi {parent?.displayName}! Ask me anything about {selectedChild.displayName}'s
              progress. I can only see what they've actually recorded in the game, so everything
              I tell you comes from their real results.
            </p>
          </div>
        )}

        {messages.map((message) => (
          <Bubble key={message.id} message={message} />
        ))}

        {sending && (
          <div className="flex gap-1.5 px-4 py-3 bg-slate-100 rounded-2xl rounded-bl-md w-fit">
            {[0, 1, 2].map((index) => (
              <span
                key={index}
                className="w-1.5 h-1.5 rounded-full bg-slate-400 animate-bounce"
                style={{ animationDelay: `${index * 120}ms` }}
              />
            ))}
          </div>
        )}

        {sendError && <p className="text-[13px] text-rose-600 px-1">{sendError}</p>}

        <div ref={bottomRef} />
      </div>

      <div className="sticky bottom-0 bg-white border-t border-slate-100 px-4 py-3 space-y-2">
        {messages.length === 0 && (
          <div className="flex gap-2 overflow-x-auto pb-1">
            {suggestions.map(({ Icon, label }) => (
              <button
                key={label}
                onClick={() => void send(label)}
                disabled={sending}
                className="flex items-center gap-1.5 shrink-0 rounded-full border border-slate-200 px-3 py-1.5 text-[12px] font-medium text-slate-600 disabled:opacity-50"
              >
                <Icon width={14} height={14} />
                {label}
              </button>
            ))}
          </div>
        )}

        <form
          onSubmit={(event) => {
            event.preventDefault();
            void send(input);
          }}
          className="flex items-center gap-2"
        >
          <input
            value={input}
            onChange={(event) => setInput(event.target.value)}
            placeholder="Ask about your child's progress…"
            disabled={sending}
            className="flex-1 rounded-full bg-slate-100 px-4 py-3 text-[14px] text-slate-900 outline-none focus:ring-2 focus:ring-violet-300 disabled:opacity-60"
          />
          <button
            type="submit"
            disabled={sending || !input.trim()}
            aria-label="Send"
            className="w-11 h-11 rounded-full bg-violet-600 text-white grid place-items-center disabled:opacity-40"
          >
            <SendIcon width={18} height={18} />
          </button>
        </form>
      </div>
    </div>
  );
}

function Bubble({ message }: { message: ChatMessage }) {
  const fromParent = message.from === 'parent';

  return (
    <div className={`flex ${fromParent ? 'justify-end' : 'justify-start'}`}>
      <div
        className={`max-w-[85%] px-4 py-3 text-[13.5px] leading-relaxed whitespace-pre-wrap ${
          fromParent
            ? 'bg-violet-600 text-white rounded-2xl rounded-br-md'
            : 'bg-slate-100 text-slate-800 rounded-2xl rounded-bl-md'
        }`}
      >
        {message.text}
      </div>
    </div>
  );
}

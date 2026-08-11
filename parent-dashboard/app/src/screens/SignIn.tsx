import { useState } from 'react';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';

/**
 * Sign in and create account, on one screen.
 *
 * Registration is intentionally not a separate route: a parent arriving for the first
 * time should not have to hunt for a sign-up link.
 */
export default function SignIn() {
  const { signIn, register } = useAuth();

  const [mode, setMode] = useState<'signin' | 'register'>('signin');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);

  const registering = mode === 'register';

  async function submit(event: React.FormEvent) {
    event.preventDefault();

    setBusy(true);
    setError(null);
    setFieldErrors({});

    try {
      if (registering) await register(displayName.trim(), email.trim(), password);
      else await signIn(email.trim(), password);
    } catch (cause) {
      if (cause instanceof ApiError) {
        setError(cause.fields.length > 0 ? 'Please check the highlighted fields.' : cause.message);

        // The API validates every field at once, so all messages are shown together
        // rather than making the parent fix one problem per attempt.
        setFieldErrors(
          Object.fromEntries(cause.fields.map((item) => [item.field, item.message])),
        );
      } else {
        setError('Something went wrong. Please try again.');
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex-1 overflow-y-auto px-6 py-10 flex flex-col justify-center">
      <div className="text-center mb-8">
        <img src="/images/logo.svg" alt="" className="w-14 h-14 rounded-2xl mx-auto mb-3" />
        <h1 className="text-[26px] font-extrabold text-slate-900">
          Shinyminds <span className="font-medium text-slate-500">Parent</span>
        </h1>
        <p className="text-slate-500 text-sm mt-1">
          {registering ? 'Create your parent account.' : 'Sign in to follow your child’s progress.'}
        </p>
      </div>

      <form onSubmit={submit} className="space-y-4" noValidate>
        {registering && (
          <Field
            label="Your name"
            value={displayName}
            onChange={setDisplayName}
            autoComplete="name"
            error={fieldErrors.displayName}
          />
        )}

        <Field
          label="Email"
          type="email"
          value={email}
          onChange={setEmail}
          autoComplete="email"
          error={fieldErrors.email}
        />

        <Field
          label="Password"
          type="password"
          value={password}
          onChange={setPassword}
          autoComplete={registering ? 'new-password' : 'current-password'}
          error={fieldErrors.password}
          hint={registering ? 'At least 8 characters.' : undefined}
        />

        {error && (
          <p className="text-sm text-rose-600 bg-rose-50 rounded-xl px-4 py-3">{error}</p>
        )}

        <button
          type="submit"
          disabled={busy}
          className="w-full rounded-2xl bg-violet-600 text-white font-bold py-4 text-[15px] disabled:opacity-60 hover:bg-violet-700 transition-colors"
        >
          {busy ? 'Please wait…' : registering ? 'Create account' : 'Sign in'}
        </button>
      </form>

      <button
        onClick={() => {
          setMode(registering ? 'signin' : 'register');
          setError(null);
          setFieldErrors({});
        }}
        className="mt-6 text-sm font-semibold text-violet-600 hover:text-violet-700"
      >
        {registering ? 'I already have an account' : 'Create a parent account'}
      </button>
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  type = 'text',
  autoComplete,
  error,
  hint,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  autoComplete?: string;
  error?: string;
  hint?: string;
}) {
  return (
    <label className="block">
      <span className="text-[13px] font-semibold text-slate-700">{label}</span>
      <input
        type={type}
        value={value}
        autoComplete={autoComplete}
        onChange={(event) => onChange(event.target.value)}
        className={`mt-1.5 w-full rounded-2xl border px-4 py-3.5 text-[15px] text-slate-900 outline-none transition-colors focus:border-violet-500 ${
          error ? 'border-rose-400 bg-rose-50/40' : 'border-slate-200 bg-white'
        }`}
      />
      {error ? (
        <span className="text-xs text-rose-600 mt-1 block">{error}</span>
      ) : hint ? (
        <span className="text-xs text-slate-400 mt-1 block">{hint}</span>
      ) : null}
    </label>
  );
}

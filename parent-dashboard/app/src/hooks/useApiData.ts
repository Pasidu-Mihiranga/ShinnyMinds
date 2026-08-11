import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../api/client';

interface State<T> {
  data: T | null;
  error: string | null;
  loading: boolean;
  reload: () => void;
}

interface Options {
  /**
   * When false, no request is made and the hook settles as idle.
   *
   * Screens have to call hooks unconditionally, before the early return for "no child
   * linked yet". Without this the hook would fire a request for child `null` on every
   * such screen and get a guaranteed 404 back.
   */
  enabled?: boolean;
}

/**
 * Runs an API call and tracks loading, data and error in one place, so no screen has
 * to reimplement the three states or risk rendering against half-loaded data.
 *
 * `deps` behaves like a useEffect dependency list - pass the child id so switching
 * child refetches.
 */
export function useApiData<T>(
  fetcher: () => Promise<T>,
  deps: unknown[],
  options: Options = {},
): State<T> {
  const { enabled = true } = options;

  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(enabled);
  const [nonce, setNonce] = useState(0);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(fetcher, deps);

  useEffect(() => {
    if (!enabled) {
      setData(null);
      setError(null);
      setLoading(false);

      return;
    }

    let cancelled = false;

    setLoading(true);
    setError(null);

    run()
      .then((result) => {
        // A fast child-switch can resolve two requests out of order; ignoring the
        // stale one stops the previous child's numbers flashing up.
        if (!cancelled) setData(result);
      })
      .catch((cause: unknown) => {
        if (cancelled) return;

        setError(
          cause instanceof ApiError ? cause.message : 'Something went wrong. Please try again.',
        );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [run, nonce, enabled]);

  return { data, error, loading, reload: () => setNonce((value) => value + 1) };
}

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api, setSessionLostHandler, tokenStore } from '../api/client';
import type { ChildWithScore, Parent } from '../api/types';

/**
 * Holds who is signed in and which child is being viewed.
 *
 * Every screen reads the selected child from here rather than taking it as a prop, so
 * switching child updates the whole dashboard at once.
 */
interface AuthState {
  status: 'loading' | 'signed-out' | 'signed-in';
  parent: Parent | null;
  children: ChildWithScore[];
  selectedChild: ChildWithScore | null;
  selectChild: (childId: string) => void;
  signIn: (email: string, password: string) => Promise<void>;
  register: (displayName: string, email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
  reloadChildren: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

const SELECTED_CHILD_KEY = 'shinyminds.parent.selectedChild';

export function AuthProvider({ children: reactChildren }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthState['status']>('loading');
  const [parent, setParent] = useState<Parent | null>(null);
  const [childList, setChildList] = useState<ChildWithScore[]>([]);
  const [selectedChildId, setSelectedChildId] = useState<string | null>(
    () => localStorage.getItem(SELECTED_CHILD_KEY),
  );

  const loadChildren = useCallback(async () => {
    const { children: fetched } = await api.children();

    setChildList(fetched);

    // Keep the stored choice if it is still one of this parent's children;
    // otherwise fall back to the first, so the dashboard is never left blank.
    setSelectedChildId((current) => {
      if (current && fetched.some((child) => child.id === current)) return current;

      return fetched[0]?.id ?? null;
    });
  }, []);

  const bootstrap = useCallback(async () => {
    if (!tokenStore.isSignedIn) {
      setStatus('signed-out');

      return;
    }

    try {
      const { parent: me } = await api.auth.me();

      setParent(me);

      await loadChildren();

      setStatus('signed-in');
    } catch {
      tokenStore.clear();
      setStatus('signed-out');
    }
  }, [loadChildren]);

  useEffect(() => {
    void bootstrap();
  }, [bootstrap]);

  // A refresh token that has expired or been revoked drops the app back to sign-in
  // from wherever the user happened to be.
  useEffect(() => {
    setSessionLostHandler(() => {
      setParent(null);
      setChildList([]);
      setStatus('signed-out');
    });

    return () => setSessionLostHandler(null);
  }, []);

  useEffect(() => {
    if (selectedChildId) localStorage.setItem(SELECTED_CHILD_KEY, selectedChildId);
    else localStorage.removeItem(SELECTED_CHILD_KEY);
  }, [selectedChildId]);

  const signIn = useCallback(
    async (email: string, password: string) => {
      const result = await api.auth.login({ email, password });

      tokenStore.save(result.tokens);
      setParent(result.parent);

      await loadChildren();

      setStatus('signed-in');
    },
    [loadChildren],
  );

  const register = useCallback(
    async (displayName: string, email: string, password: string) => {
      const result = await api.auth.register({ displayName, email, password });

      tokenStore.save(result.tokens);
      setParent(result.parent);

      await loadChildren();

      setStatus('signed-in');
    },
    [loadChildren],
  );

  const signOut = useCallback(async () => {
    await api.auth.logout();

    setParent(null);
    setChildList([]);
    setSelectedChildId(null);
    setStatus('signed-out');
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      status,
      parent,
      children: childList,
      selectedChild: childList.find((child) => child.id === selectedChildId) ?? null,
      selectChild: setSelectedChildId,
      signIn,
      register,
      signOut,
      reloadChildren: loadChildren,
    }),
    [status, parent, childList, selectedChildId, signIn, register, signOut, loadChildren],
  );

  return <AuthContext.Provider value={value}>{reactChildren}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const context = useContext(AuthContext);

  if (!context) throw new Error('useAuth must be used inside an AuthProvider.');

  return context;
}

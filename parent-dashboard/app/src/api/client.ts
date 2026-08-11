import type {
  ActivityItem,
  AuthResponse,
  ChatMessage,
  ChatSendResponse,
  ChildWithScore,
  Insights,
  Overview,
  Parent,
  SkillsProgress,
  Tokens,
} from './types';

const BASE_URL = (import.meta.env.VITE_API_URL ?? 'http://localhost:4000').replace(/\/$/, '');

const ACCESS_KEY = 'shinyminds.parent.accessToken';
const REFRESH_KEY = 'shinyminds.parent.refreshToken';

/**
 * Error carrying the API's own message, so screens can show what actually went wrong
 * instead of a generic failure.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fields: { field: string; message: string }[];

  constructor(status: number, code: string, message: string, fields: { field: string; message: string }[] = []) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.fields = fields;
  }
}

export const tokenStore = {
  get access() {
    return localStorage.getItem(ACCESS_KEY);
  },
  get refresh() {
    return localStorage.getItem(REFRESH_KEY);
  },
  save(tokens: Tokens) {
    localStorage.setItem(ACCESS_KEY, tokens.accessToken);
    localStorage.setItem(REFRESH_KEY, tokens.refreshToken);
  },
  clear() {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
  },
  get isSignedIn() {
    return Boolean(localStorage.getItem(REFRESH_KEY));
  },
};

/** Called when the session cannot be recovered, so the app can return to sign-in. */
let onSessionLost: (() => void) | null = null;

export function setSessionLostHandler(handler: (() => void) | null) {
  onSessionLost = handler;
}

/**
 * A refresh in flight, shared by every request that hits a 401 at the same time.
 * Without this, a screen loading four endpoints at once would fire four refreshes and
 * three of them would fail, because the server rotates (and so invalidates) the token
 * on first use.
 */
let refreshInFlight: Promise<boolean> | null = null;

async function refreshTokens(): Promise<boolean> {
  const refreshToken = tokenStore.refresh;

  if (!refreshToken) return false;

  if (!refreshInFlight) {
    refreshInFlight = fetch(`${BASE_URL}/api/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })
      .then(async (response) => {
        if (!response.ok) return false;

        const data = (await response.json()) as { tokens: Tokens };

        tokenStore.save(data.tokens);

        return true;
      })
      .catch(() => false)
      .finally(() => {
        refreshInFlight = null;
      });
  }

  return refreshInFlight;
}

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const body = (await response.json()) as {
      error?: { code?: string; message?: string; details?: { field: string; message: string }[] };
    };

    return new ApiError(
      response.status,
      body.error?.code ?? 'UNKNOWN',
      body.error?.message ?? 'Something went wrong. Please try again.',
      body.error?.details ?? [],
    );
  } catch {
    return new ApiError(response.status, 'UNKNOWN', 'Something went wrong. Please try again.');
  }
}

async function request<T>(
  path: string,
  options: { method?: string; body?: unknown; auth?: boolean; retry?: boolean } = {},
): Promise<T> {
  const { method = 'GET', body, auth = true, retry = true } = options;

  const headers: Record<string, string> = {};

  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const accessToken = tokenStore.access;

  if (auth && accessToken) headers.Authorization = `Bearer ${accessToken}`;

  let response: Response;

  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(
      0,
      'NETWORK',
      `Cannot reach the ShinyMinds server at ${BASE_URL}. Check that the backend is running.`,
    );
  }

  // An expired access token is routine: refresh once and replay before giving up.
  if (response.status === 401 && auth && retry && tokenStore.isSignedIn) {
    if (await refreshTokens()) {
      return request<T>(path, { ...options, retry: false });
    }

    tokenStore.clear();
    onSessionLost?.();

    throw new ApiError(401, 'UNAUTHORIZED', 'Your session has expired. Please sign in again.');
  }

  if (!response.ok) throw await toApiError(response);

  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}

/** Every endpoint the dashboard uses, in one place. */
export const api = {
  auth: {
    register: (input: { email: string; password: string; displayName: string }) =>
      request<AuthResponse>('/api/auth/parent/register', { method: 'POST', body: input, auth: false }),

    login: (input: { email: string; password: string }) =>
      request<AuthResponse>('/api/auth/parent/login', { method: 'POST', body: input, auth: false }),

    me: () => request<{ role: 'parent'; parent: Parent }>('/api/auth/me'),

    logout: async () => {
      const refreshToken = tokenStore.refresh;

      tokenStore.clear();

      if (!refreshToken) return;

      // Revoke server-side too, so the token is dead even if it was copied elsewhere.
      await request<void>('/api/auth/logout', {
        method: 'POST',
        body: { refreshToken },
        auth: false,
      }).catch(() => undefined);
    },
  },

  children: () => request<{ children: ChildWithScore[] }>('/api/dashboard/children'),

  overview: (childId: string) => request<Overview>(`/api/dashboard/children/${childId}/overview`),

  skillsProgress: (childId: string, days = 7) =>
    request<SkillsProgress>(`/api/dashboard/children/${childId}/skills/progress?days=${days}`),

  activity: (childId: string, take = 10) =>
    request<{ activity: ActivityItem[] }>(`/api/dashboard/children/${childId}/activity?take=${take}`),

  insights: (childId: string) => request<Insights>(`/api/dashboard/children/${childId}/insights`),

  chat: {
    history: (childId: string) =>
      request<{ messages: ChatMessage[] }>(`/api/dashboard/children/${childId}/chat`),

    send: (childId: string, text: string) =>
      request<ChatSendResponse>(`/api/dashboard/children/${childId}/chat`, {
        method: 'POST',
        body: { text },
      }),
  },
};

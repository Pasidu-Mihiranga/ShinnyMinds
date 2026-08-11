# Shinyminds Parent — dashboard

A mobile-first, installable (PWA) web app where a parent follows their child's progress
in the ShinyMinds game.

React 19 · Vite · Tailwind · Recharts

Every screen reads live data from the ShinyMinds API. There is no mock data — the
previous `src/data/mock.ts` has been removed, so nothing on screen is invented.

See the [root README](../../README.md) for the whole system, and
[backend/README.md](../../backend/README.md) for the API.

---

## Running it

The API must be running first, or every screen will show
"Cannot reach the ShinyMinds server".

```bash
cd app
npm install
cp .env.example .env.local
npm run dev
```

Open the URL it prints (usually `http://localhost:5173`) and create a parent account.

To see it as a phone would: open DevTools (`Cmd+Option+I`), toggle the device toolbar
(`Cmd+Shift+M`), and pick a phone. Or just narrow the window — the layout is responsive.

| | |
|---|---|
| `npm run dev` | Dev server |
| `npm run build` | Production build into `dist/` |
| `npm run preview` | Serve the production build |
| `npm run lint` | oxlint |

### Configuration

One variable, in `.env.local`:

```
VITE_API_URL=http://localhost:4000
```

**Never put a secret in this file.** Vite inlines every `VITE_` variable into the
JavaScript bundle that each visitor downloads. The assistant works by calling the API,
which holds the Groq key server-side.

---

## Seeing real data

The dashboard shows a child's progress, so a child has to exist and have played.

1. Create a parent account here.
2. Go to **Settings** and copy your six-character **parent code**.
3. In the Unity game, create a player and enter that code when asked.
4. Play a mission. It appears on the dashboard immediately.

Until a child links, every screen shows the parent code to share rather than an empty
dashboard.

---

## Structure

```
src/
├── api/
│   ├── client.ts      Every endpoint, token storage, refresh-and-retry
│   └── types.ts       Response shapes, mirroring the API's read models
├── auth/
│   └── AuthContext    Who is signed in, and which child is selected
├── hooks/
│   └── useApiData     loading / data / error for one call
├── components/        Shared UI, including LoadingCard / ErrorCard / EmptyCard
├── screens/           SignIn, Home, Insights, Assistant, Settings
└── lib/format.ts      Dates, durations, skill→icon
```

### How a screen loads data

```tsx
const overview = useApiData(
  useCallback(() => api.overview(childId), [childId]),
  [childId],
);

if (overview.loading) return <LoadingCard />;
if (overview.error) return <ErrorCard message={overview.error} onRetry={overview.reload} />;
```

Every data-backed screen handles all three states. `useApiData` ignores a response that
arrives after the child has been switched, so the previous child's numbers never flash up.

### Adding a screen or endpoint

1. Add the response type to `api/types.ts`, matching the backend's read model.
2. Add a method to `api/client.ts`.
3. Call it with `useApiData` and handle loading, error and empty.

Colours, labels and icons for skills come from the API (`color`, `icon`, `label`), so the
game, the API and the dashboard cannot disagree about what Empathy looks like. Don't
hard-code them here.

---

## Images

`public/images/logo.svg` is the app logo, used in the top bar, the browser tab and the
PWA home-screen icon. Replace it with your real logo, keeping the filename and a square
aspect ratio.

Child avatars come from `avatarUrl` on the API. Until one is set, `ChildAvatar` renders
the child's initials on a tinted circle, so there are no broken images and no stock
photos of children who don't exist.

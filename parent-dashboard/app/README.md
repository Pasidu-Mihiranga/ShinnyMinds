# Shinyminds Parent — Frontend Prototype

A mobile-first, responsive web app (installable as a PWA) for the Shinyminds Parent dashboard. This is a **frontend-only prototype** — all data (child profile, scores, activity history, chat messages) is mock data in one file. There is no backend.

## What's in it

Four screens, matching the UI mockups:

- **Home** — greeting, overall wellbeing score, this week's stats, recent activity, AI tip
- **Insights** — Progress / Skills / Activity tabs, charts, strengths & needs-attention
- **AI Assistant** — chat thread with quick-reply suggestion chips
- **Settings** — child profile, account & app settings, log out

## How to run it

You need [Node.js](https://nodejs.org) installed (18+).

```bash
cd app
npm install
npm run dev
```

Then open the URL it prints (e.g. `http://localhost:5173`) in your browser.

To see it as a phone would:
- Open Chrome DevTools (`Cmd+Option+I`), click the device toolbar icon (`Cmd+Shift+M`), and pick any phone.
- Or just shrink your browser window narrow — the layout is responsive and fills the screen edge-to-edge on mobile widths.

Other commands:
```bash
npm run build      # production build (outputs to app/dist)
npm run preview    # preview the production build locally
```

## Where the mock data lives

Everything shown on screen — the child's name, scores, this week's stats, activity list, chat messages — is in one file:

```
src/data/mock.ts
```

Edit values there and the whole app updates. No other file needs to change for content edits.

## Adding real images

Right now every photo is a placeholder (a colored gradient square/circle with an icon or initial). Placeholders live in:

```
public/images/
```

Replace a placeholder by **dropping in a real image with the exact same filename** (any format — `.png`/`.jpg` works fine, just keep the name, or update the path in `src/data/mock.ts` if you rename it). No code changes needed if you keep the names.

| Filename | Used for | Suggested image |
|---|---|---|
| `avatar-child-mihiri.svg` | Child's profile photo (Home, Insights, Assistant, Settings) | A square/circle headshot of the child |
| `avatar-parent-nadee.svg` | Parent's avatar (top-right of Home/Insights/Assistant) | A square/circle headshot of the parent |
| `activity-strangers-gift.svg` | "The Stranger's Gift" activity thumbnail | Scene illustration/screenshot from that scenario |
| `activity-helping-friend.svg` | "Helping a Friend" activity thumbnail | Scene illustration/screenshot |
| `activity-sharing-toys.svg` | "Sharing Toys" activity thumbnail | Scene illustration/screenshot |
| `activity-online-safety.svg` | "Online Safety Basics" activity thumbnail | Scene illustration/screenshot |
| `activity-saying-sorry.svg` | "Saying Sorry" activity thumbnail | Scene illustration/screenshot |
| `activity-new-classmate.svg` | "The New Classmate" activity thumbnail | Scene illustration/screenshot |
| `logo.svg` | App logo (browser tab icon, top bar, PWA home-screen icon) | Your real app logo, ideally square |

Avatars are displayed round and cropped to a circle — a centered square photo works best. Activity thumbnails are displayed as rounded squares.

Want more activities than the ones listed? Add a new image to `public/images/`, then add a matching entry to the `recentActivity` array in `src/data/mock.ts` pointing at it.

## Project structure

```
src/
  data/mock.ts        all mock data — edit this for content changes
  screens/            one file per screen (Home, Insights, Assistant, Settings)
  components/         shared UI: top bar, bottom nav, icons, progress ring, etc.
public/images/        all images (placeholders — replace as described above)
```

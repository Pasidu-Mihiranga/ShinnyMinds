# ShinyMinds API

Accounts, player progress, skill scoring and the parent assistant. Serves both the Unity
client and the parent dashboard.

Node 20 · Express · Prisma · PostgreSQL · TypeScript

See the [root README](../README.md) for how the three parts fit together.

---

## Running it

```bash
npm install
cp .env.example .env          # then fill it in
createdb shinyminds
npm run prisma:migrate
npm run db:seed
npm run dev
```

`npm run dev` restarts on save. `curl http://localhost:4000/health` confirms it is up and
reports whether the assistant has a key.

Generate the two JWT secrets with `openssl rand -base64 48`. The server refuses to start
with a secret shorter than 32 characters, and the error names the variable.

### Scripts

| | |
|---|---|
| `npm run dev` | Watch mode |
| `npm run build` · `npm start` | Compile to `dist/`, then run it |
| `npm run typecheck` | Types only, no output |
| `npm run prisma:migrate` | Create and apply a migration |
| `npm run prisma:deploy` | Apply existing migrations (production) |
| `npm run prisma:studio` | Browse the data in a browser |
| `npm run db:seed` | Load the mission catalogue. Safe to re-run |
| `npm run db:reset` | Drop everything and re-migrate |

---

## Layout

```
src/
├── config/env.ts        Validates every environment variable at boot
├── domain/skills.ts     Scoring rules. Pure functions, no I/O
├── dto/                 One zod schema per request body
├── lib/                 Prisma client, HttpError, date helpers
├── middleware/          auth, validate, error-handler
├── repositories/        The only files that call Prisma
├── routes/              Paths, methods, required token
├── services/            Business rules and ownership checks
├── app.ts               Express wiring
└── server.ts            Listen and shut down cleanly
```

The dependency direction is one-way: `routes → services → repositories → Prisma`, with
`domain/` at the bottom depending on nothing. A route never writes a query and a
repository never decides who is allowed to see something.

---

## Conventions

**Errors.** Services throw `HttpError`; `middleware/error-handler.ts` turns it into a
response. Anything else that reaches the handler is logged in full and reported as a
generic 500, so a stack trace or a SQL fragment cannot leak to a client.

```ts
throw HttpError.notFound('Child not found.');
```

**Validation.** Put the schema in `dto/index.ts` and apply it with `validateBody`. A bad
payload comes back as a 400 listing every offending field at once.

**Authorisation.** `authenticate('parent')` or `authenticate('child')` on the router
decides which kind of account may reach it at all. Ownership is then re-checked inside
the service — `dashboardService.assertOwnsChild`, `gameService.assertAttemptOwned` — so
an id in a request body cannot be used to reach someone else's data.

**Scores are derived, never accumulated.** `progressService` recomputes from the
`decisions` table. An incremental counter drifts the moment a write is retried or a row
is deleted.

---

## Adding an endpoint

1. Schema in `dto/index.ts` if it takes a body.
2. Query in the matching repository.
3. Rule and ownership check in the service.
4. Route, with the right `authenticate(...)`.
5. Add the response type to `parent-dashboard/app/src/api/types.ts` and a method to
   `client.ts` if the dashboard needs it, or to `Assets/Scripts/Api/` if the game does.

---

## Adding a mission

Missions are data. Add an entry to `prisma/seed.ts` and run `npm run db:seed` — the upsert
updates copy without wiping anyone's attempts.

```ts
{
  code: 'playground_conflict',
  title: 'Sharing the Swing',
  description: 'Take turns and settle a disagreement without an adult stepping in.',
  topic: 'sharing and taking turns',   // steers Unity's GroqDialogue
  skill: Skill.EMPATHY,
  orderIndex: 9,
  maxScore: 100,
}
```

Missions unlock in `orderIndex` order: the first incomplete one is playable and later
ones are locked.

---

## The assistant

`chat.service.ts` builds the system prompt from the child's own rows and passes it to
`groq.service.ts`. The key is read from `env` and used only there.

If you change what the model is told, change `buildContext`. Keep the ground rules
intact — the model is instructed to use only the supplied figures, to invent nothing, and
not to give clinical advice.

Without `GROQ_API_KEY`, chat endpoints return 503 with an actionable message and
everything else keeps working.

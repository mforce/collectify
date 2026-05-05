# Conventions

Style and process expectations for Collectify. Keep this list short and updated when something changes.

## Backend (C# / ASP.NET Core)

### Project structure
- One file per type. Filename matches type name.
- Endpoints grouped by resource in `Collectify.Api/Endpoints/<Resource>Endpoints.cs`.
- DTOs declared as `record` types alongside the endpoint group that owns them.

### Style
- File-scoped namespaces.
- `var` for local variables; explicit types only when inference is unclear.
- Nullable reference types are enabled across all projects. Never disable per-file.
- Prefer immutable: `record`, `init` setters, `IReadOnlyList<T>` returns.
- Async all the way: every IO method ends in `Async` and accepts a `CancellationToken` (Phase 2+).

### Error handling
- Don't catch exceptions broadly. Let ASP.NET Core's exception middleware return 500.
- Validate input at the endpoint boundary. Return `Results.BadRequest(new { error = "..." })` with a stable error shape.
- Don't leak exception messages to the client; log them with `ILogger<T>` and return a generic message.

### Tests
- xUnit + `WebApplicationFactory<Program>` for integration tests.
- Use a SQLite in-memory connection (`Data Source=:memory:`) seeded per-test.
- Name tests `Method_State_Expected`, e.g. `CreateMovie_AsAuthenticatedUser_PersistsRow`.
- One assertion theme per test. Multiple `Assert` calls are fine when they verify a single behavior.

## Frontend (React / TS)

### Style
- Strict TS; never `any`. If a third-party type is missing, add it locally in `client/types.d.ts`.
- Functional components. No class components.
- One component per file. Filename matches the export.
- Prefer `useState` and TanStack Query over global state. No Redux unless the app demands it.

### Forms
- Controlled state for now. Promote to `react-hook-form` + Zod only when validation gets non-trivial.
- All forms call a TanStack Query mutation. Error state is rendered from the mutation result.

### API hooks
- One file per resource in `api/`.
- Query keys are `[type, 'list' | 'item', ...args]`.
- Mutations invalidate the matching `[type]` family.

### Styling
- Tailwind utilities only. No CSS-in-JS, no external CSS frameworks.
- Mobile-first: design for `< 640px` first, scale up with `sm:` / `md:` / `lg:`.

## Git / GitHub

- One branch per phase / feature: `claude/<short-slug>` for AI-generated work, `feat/<slug>` / `fix/<slug>` for human work.
- Conventional-ish commit subjects (`Phase 2: …`, `fix: …`, `docs: …`). Body explains *why*.
- Every PR links its issue with `Closes #<n>` (or `Refs #<n>` for partial work).
- PR description includes: summary, what's deliberately out, verification steps, test plan checklist.

## When in doubt

- Match what's already in the codebase before inventing a new pattern.
- Smaller PR > bigger PR. Split aggressively if review > 30 minutes.

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
See [`docs/testing.md`](testing.md) for the full TDD workflow, layered strategy, and per-endpoint coverage requirements. Naming-only summary:
- xUnit + `WebApplicationFactory<Program>` for integration tests.
- Tests are named `Method_State_Expected`, e.g. `CreateMovie_AsAuthenticatedUser_PersistsRow`.

### Dependencies (Central Package Management)
- All NuGet versions live in [`src/server/Directory.Packages.props`](../src/server/Directory.Packages.props). `<ManagePackageVersionsCentrally>` is on.
- `<PackageReference>` in csproj files **must omit `Version=`**. Build will fail otherwise.
- To add a package: declare `<PackageVersion Include="X" Version="Y" />` in the central props (under the right `Label` group), then `<PackageReference Include="X" />` in the consuming csproj. `PrivateAssets` / `IncludeAssets` stay on the `PackageReference`.
- To bump a version: edit the central props only.
- Pin a transitive override (e.g. for a CVE) by adding both a `PackageVersion` entry with a comment citing the advisory and a `PackageReference` (with `PrivateAssets=all` if the parent is design-time only) in the project that drags it in.

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
- **Conventional commits, and they feed the changelog.** Subjects are `type(scope): summary` — `feat`, `fix`, `perf`, `refactor`, `docs`, plus the hidden `ci` / `build` / `chore` / `test` / `style`. `feat` bumps the minor, `fix` the patch; below 1.0.0 both damp one digit down. release-please reads these to build `CHANGELOG.md` and pick the version — a non-conventional or typo'd prefix silently contributes **no** changelog line and **no** bump. Body explains *why*.
- **The PR title is the release note.** PRs squash-merge, and for a multi-commit PR GitHub uses the **PR title** as the squashed commit subject — which is exactly what release-please parses. No local hook sees the PR title, so getting its prefix right is on the author and reviewer.
- **Use the real type names — these look right but silently parse as nothing:** `feature:` (it's `feat:`), `bug:` (it's `fix:`), and any bare prefix like `update`, `wip`, or `Phase 2:`. release-please can't classify an unlisted type, so it contributes no changelog line and no bump — a release whose every commit is mis-typed ships with empty notes. When unsure, `chore:` is the safe hidden-but-parseable default.
- **Enable the git hooks once per clone:** `git config core.hooksPath .githooks`. `commit-msg` rejects a subject that isn't a conventional header and a body line that would break release-please's parser (catches the single-commit case the PR-title rule can't); `pre-commit` is a fast build tripwire. Skip either with `--no-verify` or `SKIP_HOOKS=1`.
- Every PR links its issue with `Closes #<n>` (or `Refs #<n>` for partial work).
- PR description includes: summary, what's deliberately out, verification steps, test plan checklist.

## When in doubt

- Match what's already in the codebase before inventing a new pattern.
- Smaller PR > bigger PR. Split aggressively if review > 30 minutes.

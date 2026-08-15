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
- **Conventional commits, and they feed the changelog.** The type table, the "PR title is the release note" rule, and how to enable the git hooks live in one place — [README → Contributing](../README.md#contributing). Read it before opening a PR; a non-conventional or typo'd prefix silently costs a changelog line and a version bump.
- Every PR links its issue with `Closes #<n>` (or `Refs #<n>` for partial work).
- PR description includes: summary, what's deliberately out, verification steps, test plan checklist.

## When in doubt

- Match what's already in the codebase before inventing a new pattern.
- Smaller PR > bigger PR. Split aggressively if review > 30 minutes.

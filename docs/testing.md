# Testing strategy

How we test Collectify, and the TDD workflow every change is expected to follow. Read this before writing code or tests.

## TL;DR

1. **Red → Green → Refactor.** Write a failing test that names the behavior you want, then the smallest code that makes it pass, then clean up.
2. **Integration over unit.** A minimal-API endpoint that hits SQLite through EF Core is best tested end-to-end via `WebApplicationFactory<Program>`. Don't mock `DbContext`.
3. **Every endpoint owes four tests:** success path, auth required, ownership boundary, validation failure.
4. **Frontend tests come with the form / hook they cover** — Vitest + React Testing Library, scoped to behavior the user can observe.

## Why TDD here

- The data model is still evolving (Phase 1.1, then Phase 2 metadata, then Phase 3 scanning). Tests are the contract that survives refactors.
- Multi-user readiness depends on every query filtering by `OwnerId`. A regression here is a security bug, not a feature bug. Tests are the only defense.
- Migrations are write-once-deploy-everywhere. Round-trip tests catch schema mistakes before they ship.

## The cycle

For each unit of work — a new endpoint, a new field, a bug fix — follow the same loop:

1. **Red.** Write one test that asserts the behavior you want. Run it. Watch it fail with a *meaningful* error (not a compile error). If it doesn't fail, the test isn't testing anything.
2. **Green.** Write the smallest production code that makes it pass. Don't generalize yet.
3. **Refactor.** With the test green, restructure for clarity. Re-run; stay green.
4. **Repeat** for the next behavior. Build features one assertion at a time.

Bug fixes are the same loop with one rule: **the test goes in first and reproduces the bug**. If you can't reproduce it as a failing test, you can't claim to have fixed it.

## Test layers

We use two layers. No mocking frameworks — if a seam is hard to test, refactor it.

### 1. Domain unit tests

Pure C#, no infrastructure. Use these for:

- Validation logic on entities or value objects (`PersonalRating` must be 1–10, `AcquisitionPrice >= 0`, etc.).
- Enum parsing / serialization edge cases.
- Pure functions (when we have any).

Lives under `tests/Collectify.Tests/Domain/`. Fast, deterministic, no I/O.

### 2. API integration tests

`WebApplicationFactory<Program>` boots the full app against an in-memory SQLite connection. Use these for everything that touches the DB or the HTTP pipeline:

- Auth (signup / login / logout / cookie scoping).
- CRUD endpoints (success, auth-required, owner-scoped, validation, 404).
- Migrations (round-trip a row through the new schema).

Lives under `tests/Collectify.Tests/Api/`. Slower than unit tests, but still seconds for the whole suite.

### What we deliberately don't test

- **Framework code.** EF Core, ASP.NET Identity, Tailwind. Trust they work; test our use of them.
- **Trivial getters / setters.** No-behavior code earns no tests.
- **External APIs themselves** (Phase 2+). Stub the typed `HttpClient` at the seam; record fixture responses for edge cases.

## Required coverage per endpoint

When you add or change an endpoint, the PR must include tests for **all four** of:

| # | Scenario | Asserts |
|---|---|---|
| 1 | **Happy path** | Authenticated owner gets the expected status + body shape |
| 2 | **Auth required** | Unauthenticated request returns 401 (or 302 to login) |
| 3 | **Ownership boundary** | User A cannot read / mutate / delete user B's row — returns 404, never the row |
| 4 | **Validation failure** | Bad input returns 400 with the documented error shape |

Skipping any of these is a review-blocker. Add a test even if the behavior is "obvious" — that's the regression guard.

## Test setup conventions

Lift these from the conventions doc; the actual helpers live in `tests/Collectify.Tests/Infrastructure/` (to be created with the first real integration test):

- **`CollectifyApiFactory : WebApplicationFactory<Program>`** — overrides DI to swap the SQLite file for a shared `Data Source=:memory:` connection per test instance. EF migrations run in `EnsureCreated` mode against the in-memory DB.
- **`AuthenticatedClient(string userName)`** — seeds an Identity user and returns an `HttpClient` with the auth cookie already attached. Each test gets a fresh user.
- **`SeedAsync<T>(this CollectifyDbContext, params T[])`** — inserts entities and saves; returns them with assigned IDs.
- **One DbContext per test** via `AsyncLifetime` / `IClassFixture` — never share state between tests.

## Naming

`Method_State_Expected` (already in `conventions.md`). Concrete examples:

- `CreateMovie_AsAuthenticatedUser_PersistsRow`
- `GetMovie_AsDifferentUser_Returns404`
- `Login_WithWrongPassword_ReturnsBadRequest`
- `MigrateUp_FromV1Schema_PreservesExistingRows`

The name is the spec. If the name doesn't fit on one line, the test is doing too much.

## Assertions

- One **behavior** per test. Multiple `Assert` calls are fine when they verify the same behavior (e.g. status code + body shape together).
- Prefer fluent reads: `response.StatusCode.Should().Be(HttpStatusCode.Created)` if we adopt FluentAssertions; otherwise plain xUnit asserts. Pick one and stay consistent.
- Compare DTOs by value (records make this free), not field-by-field.

## Frontend tests

- **Vitest + React Testing Library + jsdom**, configured under the `test` key in [`src/client/vite.config.ts`](../src/client/vite.config.ts) (using `defineConfig` from `vitest/config` so the same file drives dev / build / test).
- Setup file `src/client/test/setup.ts` registers `@testing-library/jest-dom` matchers and clears the DOM after each test.
- Test **what the user sees and does**, not implementation details. Query by role / label, never by `data-testid` unless there's no other handle.
- Mock TanStack Query at the network boundary (`msw`), not at the hook.
- Co-locate tests next to the component: `MovieForm.test.tsx` next to `MovieForm.tsx`.

Run with `npm test` (single run) or `npm run test:watch` (interactive). The `client` job in CI runs `npm test` before `npm run build`, so a regression fails the build before the bundle is produced.

## Running

```bash
cd src/server
dotnet test                          # whole suite
dotnet test --filter FullyQualifiedName~MoviesEndpoints   # subset
dotnet watch test --project tests/Collectify.Tests        # red-green loop while editing
```

Tests must pass on the agent's machine before commit. CI is not a substitute for a green local run.

## When you find untested code

Two rules:

1. **Don't add tests retroactively just to hit a number.** Coverage targets push toward useless tests. We don't have a percentage gate.
2. **Do add a test the moment you touch untested code** — even a trivial change. The first PR to touch a file is responsible for leaving it tested. Over time the codebase converges to fully tested without a backfill sprint.

## Anti-patterns

- **Mocking `DbContext` or `IQueryable`.** Use the real EF Core in-memory SQLite — mocks pass while real queries fail.
- **Tests that pass when the code is deleted.** If commenting out the production code leaves the test green, the test is testing nothing.
- **Per-test database file.** Use one in-memory connection per `WebApplicationFactory` instance; isolate via test-class fixtures, not file paths.
- **Asserting on log output.** Logs change. Assert on observable behavior.
- **Sleeping in tests.** If you need to wait for something, the design has a race; fix the design.

# Contributing

## Once per clone

```bash
git config core.hooksPath .githooks
```

Enables a fast `pre-commit` (path-filtered build checks) and a `commit-msg`
check that keeps the message parseable by release-please. Both are skippable with
`--no-verify` or `SKIP_HOOKS=1` — on purpose. A hook nobody can skip is a hook
people route around. See [`.githooks/README.md`](.githooks/README.md).

## Branches and PRs

- `main` is protected. Branch, push, open a PR.
- Branch names: `feat/…`, `fix/…`, `chore/…`, `docs/…`.
- PRs squash-merge. Link the issue with `Closes #<n>` (or `Refs #<n>` for partial
  work).

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/). The type decides
the version bump and the changelog section:

```
feat(scope): add a thing          → minor (or patch below 1.0.0)
fix(scope): stop doing a thing    → patch
docs|chore|test|ci|build|style    → patch, hidden from the changelog
feat!: ... / BREAKING CHANGE:     → major (damped to minor below 1.0.0)
```

**Use the real type names.** These look right but parse as nothing — no changelog
line, no bump: `feature:` (it's `feat:`), `bug:` (it's `fix:`), and bare subjects
like `update` or `Phase 2:`. When unsure, `chore:` is the safe hidden-but-parseable
default.

Two traps, both of which produce a **green run with no release entry**:

1. **The PR title is the release note.** For a multi-commit PR, the squashed
   subject comes from the PR title — which no local hook can see. A
   non-conventional title silently costs the bump.
2. **A body line that starts with `word(` and contains another `(` before the
   closing `)` breaks the commit parser**, and a commit that fails to parse is
   *dropped entirely* — no changelog entry, no bump, and the release workflow
   still reports success. Indent the line, make it a list item, or put a word in
   front of it:

   ```
   Assert.Equal(2, Foo(Bar()))      ← breaks the parser
     Assert.Equal(2, Foo(Bar()))    ← fine (indented)
   - Assert.Equal(2, Foo(Bar()))    ← fine (list item)
   see Assert.Equal(2, Foo(Bar()))  ← fine (word in front)
   ```

   `.githooks/commit-msg` catches this locally.

## Tests

Every change to `src/` ships with tests in the same PR (see
[`docs/testing.md`](docs/testing.md) for the TDD workflow and required coverage).

- **Server** — xUnit: `cd src/server && dotnet test`. Integration tests use
  `WebApplicationFactory<Program>`.
- **Client** — Vitest + Testing Library: `cd src/client && npm test`.

CI runs the full suite plus the image build, Trivy scan, and boot smoke test on
every PR. Mirror the build/test locally with
[`./scripts/ci-local.sh`](scripts/ci-local.sh).

## Reviewing

Treat these like a missing test and block on them:

- a hardcoded credential, in application code **or** test code;
- a hardcoded hosting-provider name in code, config, or a committed doc (the repo
  is host-agnostic — every environment value comes from config);
- a third-party GitHub Action pinned to a tag rather than a full commit SHA;
- a user-visible change with no matching documentation update.

## Dependencies

- A package add or bump must commit the regenerated lock file **in the same
  commit** — CI restores in locked mode (`--locked-mode`) and a stale lock fails
  the run with `NU1004`. Regenerate the NuGet locks with
  `dotnet restore src/server/Collectify.slnx` and commit the changed
  `packages.lock.json` files.
- A known-vulnerable production dependency fails CI. The only mute is a dated
  entry in [`.github/security-exceptions.json`](.github/security-exceptions.json);
  prefer, in order: bump the package → pin/override the patched transitive →
  only then an exception, with the unblocking PR linked in the reason.

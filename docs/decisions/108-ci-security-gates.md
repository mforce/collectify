# CI security gates, lock files, digest & action pinning (#108)

The compressed rule is in [`AGENTS.md`](../../AGENTS.md) under **CI / CD**. This
is the reasoning.

## The gate script, and why it isn't the stock commands

`.github/scripts/vuln-gate.mjs` parses `dotnet list package --vulnerable` and
`npm audit` JSON and fails on any advisory at or above a threshold. Two reasons a
script rather than the built-in flags:

- `npm audit --audit-level=high` gates but has **no allowlist**. One unfixable
  upstream advisory would block every unrelated PR until it is patched.
- `dotnet list package --vulnerable` **always exits 0**, so on its own it gates
  nothing — its output must be parsed either way.

Both sides need parsing, so parsing once buys a single exceptions file, one
severity ladder, and one report format across both ecosystems.

**Fail closed.** Unreadable input, an unrecognised report shape, an unknown
severity, or a malformed exception all err toward blocking. The specific trap
that motivates this: `npm audit` is network-backed and on a registry failure
prints `{ "error": … }` with a non-zero exit — which the workflow's `|| true`
swallows. Without a shape check that parses as "clean" and passes the gate (fail
**open**). `reportProblem` rejects it. The gate ships with `vuln-gate.test.mjs`
(26 `node:test` cases), which CI runs **before** trusting the gate's verdict —
there is no point gating on an untested gate.

## Where each gate runs, and the postures

- **NuGet, blocking, high+** — in `build-and-test`, after restore (needs the
  resolved graph) and before build (so a vulnerable dep is caught even when
  compilation would fail later).
- **npm production deps, blocking, high+** (`--omit=dev`) — what reaches the
  browser.
- **npm full tree, advisory only, moderate+** (`--warn-only`) — build tooling
  (vite, postcss, undici) does not ship to users, so it surfaces in the log
  without wedging unrelated PRs.
- **dependency-review, PR-only** — scores the *diff* rather than the tree, so a
  newly introduced bad dep is named in the PR. Its allowlist is computed from the
  same exceptions file (`--emit-allowlist`), so the two gates can never disagree
  about what is excepted.
- **security-audit.yml, scheduled** — the same gates on a Monday cron, because
  the CI gates only run when CI runs, so an advisory against a dependency nobody
  is touching would otherwise go unnoticed until the next PR.
- **codeql.yml, advisory SAST** — CodeQL scans C# and TypeScript; findings show
  as neutral PR annotations (the file itself carries the query/build-mode
  rationale).

### Required one-time repo setting: turn OFF CodeQL "default setup"

`codeql.yml` is an **advanced** CodeQL configuration. GitHub refuses to ingest an
advanced workflow's SARIF while **default setup** is enabled, so the `Analyze`
jobs stay red — `"CodeQL analyses from advanced configurations cannot be
processed when the default setup is enabled"` — until an owner switches it off:
**Settings → Code security → Code scanning → CodeQL analysis → `…` → Switch to
advanced** (which disables default setup). This is a repo-settings step no commit
can perform, and the check is red on `main` too until it is done. Same shape as
the Dependency-graph toggle the `dependency-review` job probes for — except
CodeQL's upload can't self-skip, so this one must be flipped by hand before the
gate is green.

### The exceptions file

`.github/security-exceptions.json` mutes ONE advisory in ONE ecosystem until a
stated date. Every field is validated; a malformed entry is **ignored** (never
suppresses) and warned about. `expires` must be a real calendar date that
round-trips — `2026-02-30` normalises to Mar 2 and is rejected, so it can't open
an indefinite hole. An exception id must be an **exact** GHSA: a value like
`GHSA-…,GHSA-…` or one carrying a newline is rejected, because it would otherwise
forge extra entries once interpolated into dependency-review's comma-separated
`allow-ghsas`. Prefer, in order: bump the package; pin the transitive; only then
an exception with an expiry and a linked tracking issue.

## Why the 10.0.7 → 10.0.11 bump was necessary before the gate could go blocking

When the blocking NuGet gate was added, `main` already carried **6 high-severity
advisories**: `System.Security.Cryptography.Xml@10.0.7` (five) and the transitive
`SQLitePCLRaw.lib.e_sqlite3@2.1.11` (one). The irony worth remembering:
`Directory.Packages.props` pinned `System.Security.Cryptography.Xml` to 10.0.7
*specifically to fix* two older advisories, and 10.0.7 then collected five of its
own. Bumping the whole 10.0.x family to 10.0.11 cleared all six (the EF Core
Sqlite bump pulled the vulnerable SQLitePCLRaw transitive forward too). The
lesson encoded in the props comment: keep the pin *ahead* of the advisories, and
treat a gate failure there as "bump this", not "except it".

## Lock files: `--locked-mode`

`Directory.Build.props` sets `RestorePackagesWithLockFile`, and every project
commits a `packages.lock.json`. CI (and the Docker build) restore with
`--locked-mode`, which fails `NU1004` if the resolved graph no longer matches the
committed lock — so a dependency cannot float to a different version between a
green local run and CI. Adding or bumping a package means regenerating the locks
and committing them in the same change.

**MSBuild trap:** a `.props` file is XML, so a comment cannot contain `--`.
Writing `--locked-mode` inside the explanatory comment fails the *entire solution
load* with an error that points at `Microsoft.Common.props`, not at your file.
Spell the switches out in prose.

## Digest pinning, and the action-pinning rule

The three Dockerfile base images are pinned to immutable `@sha256` digests, so a
rebuild reproduces the exact bytes Trivy cleared, and Dependabot's `docker`
ecosystem owns bumping them. Never a floating tag.

**Third-party GitHub Actions are pinned to a full commit SHA** with a trailing
`# vX.Y.Z` comment — never a mutable tag. A tag can be re-pointed to malicious
code: `aquasecurity/trivy-action` was compromised this way in 2026-03 and
`tj-actions/changed-files` in 2025-03, both by retargeting a tag. `actions/*` and
`github/*` may keep major-version tags (first-party, and Dependabot tracks them).
Dependabot reads the trailing comment to bump both the SHA and the comment
together.

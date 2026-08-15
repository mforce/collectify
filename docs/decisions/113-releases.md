# Releases: commit-image publish + digest promotion (#113)

The compressed rule is in [`AGENTS.md`](../../AGENTS.md) under **CI / CD**; the
human how-to is in [`README.md`](../../README.md#releases--container-images). This
is the reasoning and the boundary.

## What changed, and why

The old `release.yml` triggered on a `v*` tag and **rebuilt the image from
scratch**. A second `docker build` produces different bytes (fresh restore
timestamps, layer metadata) and therefore a different digest — so the image
carrying a version tag was one no gate in the repo had ever scanned. The tests
that ran in that workflow ran against source, not against the artifact that
shipped.

The model now:

```
merge a PR      → release-please updates a "Release vX.Y.Z" PR (changelog from
                  conventional commits)
merge that PR   → a DRAFT release is created, the image CI already published for
                  that commit is PROMOTED to :vX.Y.Z, then the release publishes
```

Promotion is a **server-side retag of an existing digest**
(`docker buildx imagetools create --prefer-index=false`), never a rebuild. The
bytes carrying a version are, by construction, the bytes CI built, Trivy-scanned
and boot-tested.

`--prefer-index=false` is load-bearing: it defaults to *true*, which wraps a
single manifest in a new image index with a different top-level digest —
defeating the entire point that the version tag resolves to the digest CI
scanned.

## Draft until promoted

The release stays a **draft** until promotion succeeds, and GitHub withholds the
git tag for a draft. So a run that cannot find a verified image leaves **no tag
and no public release**, rather than a version pointing at nothing. This is safe
for release-please's own bookkeeping because manifest mode reads the current
version from `.release-please-manifest.json` — a committed file — not from tags.

## Why the digest comes from CI's artifact, not the `:sha-<commit>` tag

A registry tag is mutable by anyone holding push rights, and the release PR's
merge commit is public seconds after the merge while CI needs minutes to build
and push. Resolving `:sha-<commit>` would accept the *first* manifest to appear
under that name — so a forged push placed in that window could be promoted to
`:vX.Y.Z`. Instead promotion reads `published-digest-<sha>`, an artifact bound to
the CI run that built and scanned those bytes.

The artifact is looked up by **name** (repo-wide), not by run, because a repair
dispatch reports the branch tip as its `head_sha`, not the commit it built — a
run-keyed lookup would miss exactly the case the repair path exists for.

**Invariant, and it is load-bearing:** the artifact exists only if `publish` ran,
and `publish` requires `build-and-test`, `client` and `image` to have passed — so
its existence *is* the proof those gates passed. A CI job that should gate a
release MUST be added to `publish.needs` in `ci.yml`, or it can be red while
publish still records a digest. Nothing enforces this; the list is hand-kept.

## The two provenance gates, stated at the strength the argument supports

`ci.yml` writes a build-provenance attestation over the pushed digest; `promote`
verifies it before retagging:

```
gh attestation verify oci://<image>@<digest> \
  --repo <owner>/<repo> \
  --signer-workflow <owner>/<repo>/.github/workflows/ci.yml \
  --source-ref refs/heads/main \
  --bundle-from-oci
```

All three flags are load-bearing and none is the default:

- `--bundle-from-oci` reads the attestation stored beside the image in the
  registry (proving that referrer, which the deploy side depends on, exists).
- `--signer-workflow` binds the identity to the workflow *path*, not just the
  repo — so any workflow holding `attestations: write` doesn't satisfy it.
- `--source-ref` binds to the ref. `--signer-workflow` pins only the path, and
  `workflow_dispatch` runs the workflow definition from whichever ref is chosen —
  so without this, someone who can push a branch could edit `ci.yml` there,
  dispatch it, and produce a promotable attestation. Consequence: **a CI repair
  dispatch must run from `main`.**

What this does and does not close:

- The internal gate (promotion refusing without the artifact) fails closed
  against a leaked **registry** credential — a credential that can push to the
  registry cannot mint a valid attestation.
- The external gate (deploy verifying the attestation) additionally stops a
  **branch push** substituting its own bytes.
- **Neither survives a change merged to `main`.** Once a backdoored `ci.yml` is
  the definition on `main`, its attestation is genuinely valid — right signer
  workflow, right source ref — because `--source-ref` records *which ref built
  this*, not *whether that ref's content is trustworthy*. Review of changes to
  `main` is the only control that closes that, and `main` currently requires a PR
  but zero approving reviews, so the path is open today.

## The App-token choice

release-please must open a PR, which `GITHUB_TOKEN` cannot do by default. Both
release-please passes mint a short-lived **GitHub App** token and keep their own
`GITHUB_TOKEN` at `contents: read`; `promote` does not use the App at all (it
holds no `pull-requests` scope). Two reasons for the App over flipping the
repo-wide "Allow GitHub Actions to create and approve pull requests" setting:

1. That setting is **coarser** — it frees the ambient `GITHUB_TOKEN` repo-wide,
   so any job declaring `pull-requests: write` could then open and approve PRs.
   The App keeps that gate on; PR-write is reachable only by a job referencing
   the private key.
2. GitHub does not trigger workflows for a `GITHUB_TOKEN`-opened PR, so the
   release PR would carry **no CI checks**. An App identity is not subject to that
   suppression.

Precise about reason 2: it does **not** mean the version commit would ship
unverified. Merging the release PR is a human action producing an ordinary push,
which runs CI in full, and `promote` refuses without the digest artifact. What it
buys is seeing red *before* the merge, and not deadlocking the release the day
required status checks are enabled on `main`.

Caveats the mitigation depends on:

- `permission-*` caps the token the action **returns**, not the key. The private
  key is a repository secret, so any job referencing it can mint the App's full
  grant. What keeps that safe is that no job holding it runs PR-controlled code —
  not the input downscoping. Omitting `permission-*` silently mints the union of
  every grant the App holds; always downscope explicitly.
- "Pull requests: write" is **indivisible** — opening and approving are the same
  scope. Inert while `main` requires zero approving reviews; the point is to keep
  the capability behind a secret before required reviews are switched on.
- `groom`'s draft-release guard **needs the App token too**: GitHub returns draft
  releases only to a caller with push access, so probing with the job's own
  `contents: read` `GITHUB_TOKEN` would list no drafts and the guard would
  silently never fire — worse than no guard.

The workflow **fails closed** until `RELEASE_APP_CLIENT_ID` and
`RELEASE_APP_PRIVATE_KEY` exist: the token-mint step errors, so no release is
cut. The App serves only this workflow (nothing else here needs PR-write), so its
setup cost lands on one consumer.

## Known limitation: the assembly version of a released image

CI builds the commit image without passing the Dockerfile `VERSION` build-arg, so
the published assembly's `InformationalVersion` is the `0.0.0` default and
`/api/health` reports `0.0.0` regardless of the release tag. This is inherent to
promotion — the version lives in the git tag, the release notes, and the
`image.json` asset, not in the bytes (re-stamping the version would rebuild them
and break the "what shipped is what was scanned" guarantee). If a self-reported
version is wanted later, stamp the commit SHA at CI build time (already unique
per commit) rather than the release version.

## Bootstrap

release-please was seeded at the existing **v0.0.7** (manifest + `version.txt`),
so it computes the next release from commits since that tag. Releases before
v0.0.8 predate the automation and live only as GitHub releases/tags; `CHANGELOG.md`
says so. Never hand-edit `.release-please-manifest.json` or `version.txt` —
release-please owns them, and a manual edit desynchronises it from the tags.

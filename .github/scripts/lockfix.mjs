#!/usr/bin/env node
// Classifier for the dependabot-lockfix workflow (#224).
//
// The workflow reverts Dependabot's csproj edits, regenerates every
// packages.lock.json, and must decide whether the result is safe to commit back
// to the PR branch. This is that decision, kept out of the YAML so it can be
// unit-tested (mirrors vuln-gate.mjs).
//
// It reads `git status --porcelain -z` on stdin (NUL-delimited, so a path with
// a space or newline is one field) and answers with an exit code:
//   0  every changed path is a known lock file, or a csproj now identical to
//      the merge base -> commit
//   2  anything else is dirty -> abort, do not commit (fail closed)
//   3  nothing changed -> no-op
// Any other exit (a crash) the caller also treats as abort.
//
// Usage: git status --porcelain -z | node .github/scripts/lockfix.mjs <merge-base-sha>
//        node .github/scripts/lockfix.mjs --list   (print the lock allowlist)

import { execFileSync } from "node:child_process";
import { realpathSync } from "node:fs";
import { pathToFileURL } from "node:url";

// An EXACT allowlist, not a `**/packages.lock.json` glob: a lock written
// somewhere unexpected (a new project, a planted file) must abort, not pass.
export const LOCK_FILES = Object.freeze([
  "src/server/Collectify.Api/packages.lock.json",
  "src/server/Collectify.Domain/packages.lock.json",
  "src/server/Collectify.Infrastructure/packages.lock.json",
  "src/server/Collectify.PostgresMigrations/packages.lock.json",
  "src/server/tests/Collectify.Tests/packages.lock.json",
  "src/server/tests/Collectify.PostgresTests/packages.lock.json",
]);

const ALLOW = new Set(LOCK_FILES);
const CSPROJ = /^src\/server\/.+\.csproj$/;

// Parse `git status --porcelain -z` into records. A rename/copy (X or Y is
// 'R'/'C') is followed by its source path as its own NUL field; both paths are
// kept so a rename onto a lock file can't smuggle a foreign source past.
export function changedRecords(porcelainZ) {
  const parts = String(porcelainZ).split("\0");
  const records = [];
  for (let i = 0; i < parts.length; i++) {
    const rec = parts[i];
    if (!rec) continue; // trailing empty field after the final NUL
    const xy = rec.slice(0, 2);
    records.push({ xy, path: rec.slice(3) });
    if (/[RC]/.test(xy)) {
      const source = parts[++i];
      if (source) records.push({ xy, path: source });
    }
  }
  return records;
}

// A csproj counts as a revert only when it was modified in place (no add,
// delete, rename or untracked) and its content now equals the merge base.
function isRevert({ xy, path }, matchesBase) {
  return CSPROJ.test(path) && /^[ M]{2}$/.test(xy) && matchesBase(path);
}

// commit | abort | noop, with the paths behind the verdict for the log.
export function classify(porcelainZ, matchesBase) {
  const records = changedRecords(porcelainZ);
  if (records.length === 0) return { action: "noop", foreign: [], locks: [], reverted: [] };
  const locks = [], reverted = [], foreign = [];
  for (const r of records) {
    if (ALLOW.has(r.path)) locks.push(r.path);
    else if (isRevert(r, matchesBase)) reverted.push(r.path);
    else foreign.push(r.path);
  }
  return { action: foreign.length > 0 ? "abort" : "commit", foreign, locks, reverted };
}

// The working-tree file is byte-identical to its content at `base`.
export function gitMatchesBase(base) {
  return (path) => {
    try {
      execFileSync("git", ["diff", "--quiet", base, "--", path]);
      return true;
    } catch {
      return false;
    }
  };
}

const EXIT = { commit: 0, abort: 2, noop: 3 };

async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) chunks.push(chunk);
  return Buffer.concat(chunks).toString("utf8");
}

async function main() {
  const arg = process.argv[2];
  if (arg === "--list") {
    console.log(LOCK_FILES.join("\n"));
    return;
  }
  if (!/^[0-9a-f]{40}$/.test(arg ?? "")) {
    console.error("::error::lockfix: usage: lockfix.mjs <merge-base-sha> | --list");
    process.exitCode = 2;
    return;
  }
  const { action, foreign, locks, reverted } = classify(await readStdin(), gitMatchesBase(arg));
  if (action === "commit") {
    console.log(`lockfix: ${locks.length} lock file(s), ${reverted.length} reverted csproj:\n  ${[...locks, ...reverted].join("\n  ")}`);
  } else if (action === "noop") {
    console.log("lockfix: no changes — nothing to commit.");
  } else {
    console.error(
      `::error::lockfix: refusing to commit — ${foreign.length} path(s) are neither a lock file nor a csproj reverted to ${arg}:\n  ${foreign.join("\n  ")}`,
    );
  }
  process.exitCode = EXIT[action];
}

// realpath: run through a symlink, argv[1] differs from import.meta.url and the
// guard would skip main(), exiting 0 ("commit") without classifying anything.
if (process.argv[1] && import.meta.url === pathToFileURL(realpathSync(process.argv[1])).href) await main();

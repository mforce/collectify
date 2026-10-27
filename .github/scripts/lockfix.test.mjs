import test, { after } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { chmodSync, copyFileSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { changedRecords, classify, gitMatchesBase, LOCK_FILES } from "./lockfix.mjs";

const z = (...records) => records.map((r) => r + "\0").join("");
const API_CSPROJ = "src/server/Collectify.Api/Collectify.Api.csproj";
const MIGRATIONS_CSPROJ = "src/server/Collectify.PostgresMigrations/Collectify.PostgresMigrations.csproj";
const atBase = () => true;
const notAtBase = () => false;
const scratch = mkdtempSync(join(tmpdir(), "lockfix-test-"));
after(() => rmSync(scratch, { recursive: true, force: true }));

// A hand-kept list silently misses a new project.
test("the allowlist covers exactly the projects in Collectify.slnx", () => {
  const slnx = readFileSync(new URL("../../src/server/Collectify.slnx", import.meta.url), "utf8")
    // Keep a separator so removing a comment cannot assemble a project tag.
    .replace(/<!--[\s\S]*?-->/g, " ");
  const projects = [...slnx.matchAll(/<Project\b[^>]*\bPath\s*=\s*["']([^"']+\.csproj)["'][^>]*>/g)]
    .map((m) => "src/server/" + m[1].replaceAll("\\", "/").replace(/[^/]+\.csproj$/, "packages.lock.json"))
    .sort();
  assert.equal(projects.length, 6, `parsed ${projects.length} projects from the slnx`);
  assert.deepEqual([...LOCK_FILES].sort(), projects);
});

test("only lock files -> commit", () => {
  const r = classify(
    z(" M src/server/Collectify.Domain/packages.lock.json",
      " M src/server/tests/Collectify.Tests/packages.lock.json"),
    notAtBase,
  );
  assert.equal(r.action, "commit");
  assert.equal(r.locks.length, 2);
  assert.deepEqual(r.foreign, []);
});

test("locks plus a csproj restored to the merge base -> commit, csproj listed as reverted", () => {
  const r = classify(
    z(" M src/server/Collectify.PostgresMigrations/packages.lock.json", ` M ${MIGRATIONS_CSPROJ}`),
    atBase,
  );
  assert.equal(r.action, "commit");
  assert.deepEqual(r.reverted, [MIGRATIONS_CSPROJ]);
});

test("a reverted csproj alone -> commit", () => {
  assert.equal(classify(z(` M ${API_CSPROJ}`), atBase).action, "commit");
});

test("no changes -> noop", () => {
  assert.equal(classify("", atBase).action, "noop");
});

test("a csproj changed to anything other than the merge base -> abort", () => {
  const r = classify(z(" M src/server/Collectify.Api/packages.lock.json", ` M ${API_CSPROJ}`), notAtBase);
  assert.equal(r.action, "abort");
  assert.deepEqual(r.foreign, [API_CSPROJ]);
});

test("an added, deleted, untracked or renamed csproj -> abort even if it matches the base", () => {
  for (const rec of [`A  ${API_CSPROJ}`, ` D ${API_CSPROJ}`, `?? ${API_CSPROJ}`]) {
    assert.equal(classify(z(rec), atBase).action, "abort", rec);
  }
  assert.equal(classify(`R  ${API_CSPROJ}\0src/server/Other/Other.csproj\0`, atBase).action, "abort");
});

test("a csproj outside src/server -> abort", () => {
  assert.equal(classify(z(" M tools/Evil/Evil.csproj"), atBase).action, "abort");
});

test("a non-allowlisted file -> abort", () => {
  for (const p of ["nuget.config", "src/server/Directory.Packages.props", "src/server/New/packages.lock.json"]) {
    const r = classify(z(` M src/server/Collectify.Api/packages.lock.json`, ` M ${p}`), atBase);
    assert.equal(r.action, "abort", p);
    assert.deepEqual(r.foreign, [p]);
  }
});

test("NUL-delimited: a path containing a space is ONE field", () => {
  const r = classify(z(" M weird dir/packages.lock.json"), atBase);
  assert.equal(r.action, "abort");
  assert.deepEqual(r.foreign, ["weird dir/packages.lock.json"]);
});

test("a rename or copy that drags in a non-lock SOURCE is caught", () => {
  for (const xy of ["R ", "C "]) {
    const r = classify(`${xy} src/server/Collectify.Api/packages.lock.json\0evil/secrets.txt\0`, atBase);
    assert.equal(r.action, "abort", xy);
    assert.ok(r.foreign.includes("evil/secrets.txt"));
  }
});

test("changedRecords returns both target and source of a rename", () => {
  assert.deepEqual(
    changedRecords("R  a/packages.lock.json\0b/old.json\0").map((r) => r.path),
    ["a/packages.lock.json", "b/old.json"],
  );
});

test("gitMatchesBase compares the working tree with the given commit", () => {
  const dir = mkdtempSync(join(scratch, "base-"));
  const git = (...args) => execFileSync("git", ["-C", dir, ...args], { encoding: "utf8" }).trim();
  git("init", "-q");
  mkdirSync(join(dir, "src/server/P"), { recursive: true });
  const file = join(dir, "src/server/P/P.csproj");
  writeFileSync(file, "<Project />\n");
  git("add", ".");
  git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-qm", "base");
  const base = git("rev-parse", "HEAD");
  const cwd = process.cwd();
  process.chdir(dir);
  try {
    const matches = gitMatchesBase(base);
    assert.equal(matches("src/server/P/P.csproj"), true);
    writeFileSync(file, "<Project><ItemGroup /></Project>\n");
    assert.equal(matches("src/server/P/P.csproj"), false);
  } finally {
    process.chdir(cwd);
  }
});

test("the CLI runs when invoked through a symlink", () => {
  const dir = mkdtempSync(join(scratch, "link-"));
  const link = join(dir, "lockfix.mjs");
  symlinkSync(fileURLToPath(new URL("./lockfix.mjs", import.meta.url)), link);
  const out = execFileSync(process.execPath, [link, "--list"], { encoding: "utf8" });
  assert.deepEqual(out.trim().split("\n"), [...LOCK_FILES]);
});

// Runs lockfix-apply.sh in a scratch workspace laid out like the commit job:
// trusted/ scripts, a pr/ checkout of a pushed branch, and an artifact of new
// locks. `tamper(pr, git, commit)` shapes the PR checkout before the run;
// `classifier` replaces the trusted lockfix.mjs, which stays importable as
// lockfix-real.mjs.
function runApply(tamper = () => {}, classifier) {
  const dir = mkdtempSync(join(scratch, "apply-"));
  const git = (cwd, ...args) => execFileSync("git", ["-C", cwd, ...args], { encoding: "utf8" });
  const scripts = join(dir, "trusted/.github/scripts");
  mkdirSync(scripts, { recursive: true });
  for (const f of ["lockfix.mjs", "lockfix-apply.sh", "lockfix-revert.sh"]) {
    copyFileSync(new URL(`./${f}`, import.meta.url), join(scripts, f));
  }
  if (classifier) {
    copyFileSync(new URL("./lockfix.mjs", import.meta.url), join(scripts, "lockfix-real.mjs"));
    writeFileSync(join(scripts, "lockfix.mjs"), classifier);
  }
  const pr = join(dir, "pr");
  git(dir, "init", "-q", "--bare", "remote.git");
  git(dir, "init", "-q", "-b", "main", "pr");
  for (const lock of LOCK_FILES) {
    for (const [root, body] of [[pr, "{}\n"], [join(dir, "art"), '{"new":1}\n']]) {
      mkdirSync(dirname(join(root, lock)), { recursive: true });
      writeFileSync(join(root, lock), body);
    }
  }
  const commit = (msg) => git(pr, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-qam", msg);
  git(pr, "add", ".");
  commit("base");
  git(pr, "remote", "add", "origin", join(dir, "remote.git"));
  git(pr, "push", "-q", "origin", "main");
  git(pr, "fetch", "-q", "origin");
  tamper(pr, git, commit);
  const run = spawnSync("bash", [join(scripts, "lockfix-apply.sh")], {
    cwd: dir,
    encoding: "utf8",
    env: { ...process.env, ARTIFACT_DIR: join(dir, "art"), DEFAULT_BRANCH: "main", HEAD_BRANCH: "pr" },
  });
  const show = (path) => spawnSync("git", ["-C", join(dir, "remote.git"), "show", `pr:${path}`], { encoding: "utf8" }).stdout;
  return { run, show, pushed: show(LOCK_FILES[0]), applyScript: readFileSync(join(scripts, "lockfix-apply.sh"), "utf8") };
}

const API_LOCK = LOCK_FILES[0];
const APPLY_SH = readFileSync(new URL("./lockfix-apply.sh", import.meta.url), "utf8");

test("apply: regular tracked locks -> commit and push", () => {
  const { run, pushed } = runApply();
  assert.equal(run.status, 0, run.stdout + run.stderr);
  assert.equal(pushed, '{"new":1}\n');
});

test("apply: a lock committed as a symlink -> abort without writing through it", () => {
  const { run, applyScript } = runApply((pr, git, commit) => {
    rmSync(join(pr, API_LOCK));
    symlinkSync("../../../../trusted/.github/scripts/lockfix-apply.sh", join(pr, API_LOCK));
    commit("symlink");
  });
  assert.notEqual(run.status, 0);
  assert.equal(applyScript, APPLY_SH);
});

test("apply: a symlink in the working tree over a 100644 entry -> abort without writing through it", () => {
  const { run, applyScript } = runApply((pr) => {
    rmSync(join(pr, API_LOCK));
    symlinkSync("../../../../trusted/.github/scripts/lockfix-apply.sh", join(pr, API_LOCK));
  });
  assert.notEqual(run.status, 0);
  assert.match(run.stdout, /goes through a symlink/);
  assert.equal(applyScript, APPLY_SH);
});

test("apply: a lock tracked as 100755 -> abort", () => {
  const { run, pushed } = runApply((pr, git, commit) => {
    chmodSync(join(pr, API_LOCK), 0o755);
    commit("exec");
  });
  assert.notEqual(run.status, 0);
  assert.match(run.stdout, /not a tracked regular file/);
  assert.equal(pushed, "");
});

test("apply: a directory committed at the lock path -> abort", () => {
  const { run, pushed } = runApply((pr, git, commit) => {
    rmSync(join(pr, API_LOCK));
    mkdirSync(join(pr, API_LOCK));
    writeFileSync(join(pr, API_LOCK, "packages.lock.json"), "{}\n");
    git(pr, "add", "-A");
    commit("dir");
  });
  assert.notEqual(run.status, 0);
  assert.match(run.stdout, /not a tracked regular file/);
  assert.equal(pushed, "");
});

test("apply: csproj edits at any depth under src/server revert to the merge base", () => {
  const csprojs = ["src/server/Top.csproj", "src/server/tests/Collectify.Tests/Collectify.Tests.csproj"];
  const { run, show } = runApply((pr, git, commit) => {
    for (const p of csprojs) writeFileSync(join(pr, p), "<Project />\n");
    git(pr, "add", ".");
    commit("csproj");
    git(pr, "push", "-q", "origin", "HEAD:main");
    git(pr, "fetch", "-q", "origin");
    for (const p of csprojs) writeFileSync(join(pr, p), "<Project><ItemGroup /></Project>\n");
    commit("dependabot csproj edit");
  });
  assert.equal(run.status, 0, run.stdout + run.stderr);
  for (const p of csprojs) assert.equal(show(p), "<Project />\n", p);
  assert.equal(show(API_LOCK), '{"new":1}\n');
});

test("apply: a classifier run that never reaches main() -> abort, nothing pushed", () => {
  const { run, pushed } = runApply(undefined, [
    'import { LOCK_FILES } from "./lockfix-real.mjs";',
    'if (process.argv[2] === "--list") console.log(LOCK_FILES.join("\\n"));',
  ].join("\n"));
  assert.notEqual(run.status, 0);
  assert.match(run.stdout, /Classifier refused \(exit 0\)/);
  assert.equal(pushed, "");
});

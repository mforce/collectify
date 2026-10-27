// `node --test .github/scripts/lockfix-stage.test.mjs`
import test from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { LOCK_FILES } from "./lockfix.mjs";

const here = dirname(fileURLToPath(import.meta.url));

// A workspace laid out like the compute job: trusted/ tooling, pr/ written by
// the restore container, and a host secret outside both.
function workspace(t) {
  const ws = mkdtempSync(join(tmpdir(), "lockfix-stage-"));
  t.after(() => rmSync(ws, { recursive: true, force: true }));
  const scripts = join(ws, "trusted/.github/scripts");
  mkdirSync(scripts, { recursive: true });
  for (const f of ["lockfix.mjs", "lockfix-stage.sh"]) copyFileSync(join(here, f), join(scripts, f));
  for (const rel of LOCK_FILES) {
    mkdirSync(dirname(join(ws, "pr", rel)), { recursive: true });
    writeFileSync(join(ws, "pr", rel), `{"lock":"${rel}"}`);
  }
  writeFileSync(join(ws, "secret"), "host secret");
  return ws;
}

const stage = (ws) =>
  spawnSync("bash", ["trusted/.github/scripts/lockfix-stage.sh", join(ws, "out")], { cwd: ws, encoding: "utf8" });

test("stages every allowlisted lock", (t) => {
  const ws = workspace(t);
  const r = stage(ws);
  assert.equal(r.status, 0, r.stdout + r.stderr);
  for (const rel of LOCK_FILES) assert.equal(readFileSync(join(ws, "out", rel), "utf8"), `{"lock":"${rel}"}`);
});

test("a lock that is a symlink to a host file is refused", (t) => {
  const ws = workspace(t);
  const rel = LOCK_FILES[0];
  rmSync(join(ws, "pr", rel));
  symlinkSync(join(ws, "secret"), join(ws, "pr", rel));
  const r = stage(ws);
  assert.notEqual(r.status, 0);
  assert.match(r.stdout, /goes through a symlink/);
  assert.ok(!existsSync(join(ws, "out", rel)), "the host file must not reach the artifact");
});

test("a symlinked parent directory is refused", (t) => {
  const ws = workspace(t);
  const rel = LOCK_FILES[0];
  const dir = join(ws, "pr", dirname(rel));
  rmSync(dir, { recursive: true });
  mkdirSync(join(ws, "elsewhere"));
  writeFileSync(join(ws, "elsewhere/packages.lock.json"), "host file");
  symlinkSync(join(ws, "elsewhere"), dir);
  const r = stage(ws);
  assert.notEqual(r.status, 0);
  assert.match(r.stdout, /goes through a symlink/);
});

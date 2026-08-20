// Enum parity check between the server enums and the client tables in
// src/client/services/types.ts.
//
// Runs in the client CI job (and locally via `npm run check:enums`)
// without a .NET runtime: it parses the server .cs enum source files and
// compares them against the hand-mirrored client tables. Fails on any
// add / rename of a member, a renumber of a [Flags] member (the client
// stores those numerics), a duplicate client entry, or a union/type
// member that has no table entry.
//
// The .NET side runs the complementary comparison in
// src/server/tests/Collectify.Tests/Domain/EnumParityTests.cs, which also
// pins every *persisted* enum's numeric values in a golden test (the
// client does not store those numerics, so a GamePlatform renumber is
// caught there, not here). Keeping both means drift fails in whichever CI
// job picks up the change.
//
// Usage: node scripts/check-enum-parity.mjs

import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// Script lives in src/client/scripts/; repo root is three levels up.
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const enumsDir = join(root, 'src', 'server', 'Collectify.Domain', 'Enums');
const typesPath = join(root, 'src', 'client', 'services', 'types.ts');

// enum name -> client table constant. Keep in sync with the golden set
// asserted in EnumParityTests. Neither file reads the other, so agreement is
// enforced INDIRECTLY: each half fails closed on its own if its map drifts
// (the xUnit EveryClientTableIsRegistered test asserts its map against the
// server enums; this script asserts its map against the client tables). Add
// an enum to BOTH maps.
const clientTable = {
  CollectionStatus: 'COLLECTION_STATUSES',
  Condition: 'CONDITIONS',
  WatchStatus: 'WATCH_STATUSES',
  CompletionStatus: 'COMPLETION_STATUSES',
  MovieFormat: 'MOVIE_FORMAT_FLAGS',
  MusicFormat: 'MUSIC_FORMATS',
  DigitalStore: 'DIGITAL_STORE_FLAGS',
  GamePlatform: 'GAME_PLATFORMS',
};

// Server-internal public enums in Collectify.Domain/Enums that are
// intentionally NOT mirrored on the client (no UI). Must stay in sync with
// notMirroredOnClient in EnumParityTests.cs. Empty today.
const notMirroredOnClient = [
  // (none today)
];

/**
 * Strip C# comments (line `//` and block comments) from source,
 * comment-aware so string, char, and verbatim literals survive. Applied to a
 * server enum file BEFORE counting declarations and parsing, so doc text
 * like `// public enum Example` cannot create a phantom enum that fails the
 * declaration-count check. (The enum files use only single-line `//`
 * comments and plain string/char literals, so this scanner is sufficient.)
 */
function stripCSharpComments(source) {
  let out = '';
  let i = 0;
  let mode = 'code'; // code | line | block | str | chr | verbatim
  while (i < source.length) {
    const c = source[i];
    const d = source[i + 1];
    if (mode === 'code') {
      if (c === '/' && d === '/') { mode = 'line'; i += 2; continue; }
      if (c === '/' && d === '*') { mode = 'block'; i += 2; continue; }
      if (c === '"') { mode = source[i - 1] === '@' ? 'verbatim' : 'str'; out += c; i++; continue; }
      if (c === "'") { mode = 'chr'; out += c; i++; continue; }
      out += c; i++; continue;
    }
    if (mode === 'line') {
      if (c === '\n') { mode = 'code'; out += c; }
      i++; continue;
    }
    if (mode === 'block') {
      if (c === '*' && d === '/') { mode = 'code'; i += 2; continue; }
      if (c === '\n') out += c; // preserve line numbers
      i++; continue;
    }
    if (mode === 'str') {
      if (c === '\\') { out += c + (d ?? ''); i += 2; continue; }
      if (c === '"') { mode = 'code'; out += c; i++; continue; }
      out += c; i++; continue;
    }
    if (mode === 'verbatim') {
      if (c === '"' && d === '"') { out += c + d; i += 2; continue; }
      if (c === '"') { mode = 'code'; out += c; i++; continue; }
      out += c; i++; continue;
    }
    if (mode === 'chr') {
      if (c === '\\') { out += c + (d ?? ''); i += 2; continue; }
      if (c === "'") { mode = 'code'; out += c; i++; continue; }
      out += c; i++; continue;
    }
  }
  return out;
}

/**
 * Parse ALL `public enum X { A = 1, B = 2, ... }` declarations in a source
 * string into ordered [name, value] members. Global (matchAll) so a file
 * with more than one enum is fully covered -- a non-global match would
 * parse only the first and let the rest escape the check. Returns [] when
 * the file declares no enums (e.g. GamePlatformMapping.cs).
 *
 * Member lines may carry a trailing `//` comment (stripped before
 * matching). Any body line that is not blank, not a comment, and not a
 * parseable member is a hard failure -- a silently-skipped line is how
 * the check would otherwise fail open (e.g. a new member written with a
 * trailing comment).
 */
function parseServerEnums(source) {
  const declRe = /(?:\[[^\]]*\]\s*)*public\s+enum\s+(\w+)[^{]*\{([\s\S]*?)\n\}/g;
  return [...source.matchAll(declRe)].map((decl) => {
    const name = decl[1];
    // `[Flags]` sits on its own line(s) above `public enum`; the declRe
    // prefix `(?:\[[^\]]*\]\s*)*` consumes the attributes as part of the
    // match, so the full match text (decl[0]) includes the `[Flags]`
    // marker. Test the match head (before `public enum`) for it.
    const head = decl[0].slice(0, decl[0].indexOf('public'));
    const isFlags = /\[\s*Flags\s*\]/.test(head);
    const { members: parsedMembers } = parseOneEnum(name, decl[2]);
    return { name, isFlags, members: parsedMembers };
  });
}

function parseOneEnum(name, body) {
  const members = [];
  let next = 0;
  for (const rawLine of body.split('\n')) {
    const line = rawLine.replace(/\/\/.*$/, '').trim();
    if (line === '') continue;
    const m = line.match(/^(\w+)\s*(?:=\s*(-?\d+))?\s*,?$/);
    if (!m) throw new Error(`${name}: cannot parse enum member line: ${rawLine.trim()}`);
    members.push([m[1], m[2] !== undefined ? parseInt(m[2], 10) : next]);
    next = members.at(-1)[1] + 1;
  }
  return { name, members };
}

/** Find the end of a top-level `]` array literal starting at `start`. */
function findArrayEnd(source, start) {
  let depth = 0;
  let inStr = false;
  let strCh = '';
  for (let i = start; i < source.length; i++) {
    const c = source[i];
    if (inStr) {
      if (c === '\\') i++;
      else if (c === strCh) inStr = false;
      continue;
    }
    if (c === "'" || c === '"' || c === '`') { inStr = true; strCh = c; }
    else if (c === '[') depth++;
    else if (c === ']') { depth--; if (depth === 0) return i; }
  }
  throw new Error('unterminated array literal');
}

/**
 * Strip `//` line comments (outside single-quoted string literals) and
 * `/* ... *​/` block comments from source. String values in these tables
 * contain neither `//` nor `/*`, so a comment-aware scan is safe. Applied to
 * the WHOLE source before locating table declarations, so a commented-out
 * (dead) table is invisible to the lookup -- not just a commented-out entry
 * inside a live table. Mirrors EnumParityTests.
 */
function stripComments(source) {
  // Remove block comments first (they may span lines and contain `//`).
  const noBlocks = source.replace(/\/\*[\s\S]*?\*\//g, '');
  // Then truncate each line at its first `//` outside a string literal.
  return noBlocks
    .split('\n')
    .map((line) => {
      let inStr = false;
      for (let i = 0; i < line.length; i++) {
        if (inStr) {
          if (line[i] === '\\') i++;
          else if (line[i] === "'") inStr = false;
        } else if (line[i] === "'") inStr = true;
        else if (line[i] === '/' && line[i + 1] === '/') {
          return line.slice(0, i);
        }
      }
      return line;
    })
    .join('\n');
}

/**
 * Parse the client table for `tableName`. Handles the two shapes in
 * types.ts: plain object-literal entries (`{ value: 'X', label: 'Y' }`)
 * and the one-liner-per-entry shape (GAME_PLATFORMS). Returns [name,
 * value] where value is the numeric for flags entries or null otherwise.
 */
function parseClientTable(source, tableName, isFlags) {
  // Work on comment-stripped source so a commented-out (dead) table
  // declaration is invisible to the lookup -- not just a commented-out entry
  // inside a live table. (stripComments removes both `//` line comments and
  // `/* ... *​/` block comments, comment-aware so string values survive.)
  const live = stripComments(source);
  // Anchor on a `:` or `=` after the name so a decoy const whose name merely
  // starts with the real table name (e.g. `GAME_PLATFORMS_V2` declared above
  // the real `GAME_PLATFORMS`) cannot shadow it. Fail closed on ambiguity
  // (0 or >1 hits) rather than silently taking the first match -- that is
  // exactly the fail-open a reviewer could plant to drift the real table
  // free. Mirrors EnumParityTests.ParseClientTable. The lookup runs on the
  // comment-stripped `live` source so a commented-out (dead) table is
  // invisible to it.
  const hits = [...live.matchAll(new RegExp(`export const ${tableName}\\s*[:=]`, 'g'))];
  if (hits.length !== 1) return null;
  const start = hits[0].index;
  // The array literal starts after the `=`, not at the `[]` in the type
  // annotation. Use `indexOf('=')` (not `'= '`) so `= [` without a space
  // still resolves; fail closed if there is no `=` at all.
  const eq = live.indexOf('=', start);
  if (eq < 0) return null;
  const open = live.indexOf('[', eq + 1);
  const close = findArrayEnd(live, open);
  const body = live.slice(open, close + 1);
  // NOTE: this parser only inspects the array LITERAL. A deliberate
  // post-declaration runtime mutation of the exported const (e.g. `TABLE
  // .push(...)` / `TABLE[i].value = ...`) is OUT OF SCOPE: it is not
  // client/server *drift* (the literal still matches the server), it is a
  // separate threat best addressed by typing the tables `readonly` (a
  // deliberate, wider change) or code review. A regex scan for such
  // mutations was tried and dropped because it false-positives on ordinary
  // reads (`TABLE[0]` followed by any later `=`) and on non-mutating
  // methods (`.concat`), while still missing real mutators (`.fill`,
  // `.copyWithin`) -- net negative for a mandatory CI gate.

  // Every top-level element must be an inline object literal. A spread
  // (`...extraPlatforms`) or any other non-object element would be silently
  // ignored by the `{...}` scan, letting a duplicate member ride in from an
  // external array -- fail closed instead. Split on commas at depth 0 (not a
  // naive `.split(',')`, which would break inside object literals that
  // contain their own commas).
  const inner = body.slice(1, -1);
  const topLevel = [];
  let depth = 0, inStr = false, segStart = 0;
  for (let i = 0; i < inner.length; i++) {
    const c = inner[i];
    if (inStr) { if (c === '\\') i++; else if (c === "'") inStr = false; }
    else if (c === "'") inStr = true;
    else if (c === '{') depth++;
    else if (c === '}') depth--;
    else if (c === ',' && depth === 0) { topLevel.push(inner.slice(segStart, i).trim()); segStart = i + 1; }
  }
  const last = inner.slice(segStart).trim();
  if (last !== '') topLevel.push(last);
  for (const el of topLevel) {
    if (el === '') continue;
    if (!el.startsWith('{') || !el.endsWith('}')) {
      throw new Error(`${tableName}: table element is not an inline object literal (e.g. a spread): ${el}`);
    }
  }

  const members = [];
  const entries = body.match(/\{[^{}]*\}/g) ?? [];
  // The brace scan must find exactly one entry per top-level element. Fewer
  // means a nested object literal (`{ meta: { value: 'X' }, value: 'Y' }`) let
  // the regex match only the inner pair and shadow the entry's real value.
  // (tsc's excess-property check already rejects this at the build gate; this
  // is defense-in-depth for the parity check on its own.)
  const nonEmptyTopLevel = topLevel.filter((el) => el !== '');
  if (entries.length !== nonEmptyTopLevel.length) {
    throw new Error(`${tableName}: entry count (${entries.length}) != top-level element count (${nonEmptyTopLevel.length}) -- nested object literal or unbalanced braces`);
  }
  for (const entry of entries) {
    const name = entry.match(/(?:key|value)\s*:\s*'([^']*)'/);
    if (!name) throw new Error(`${tableName}: entry with no string value: ${entry}`);
    // Flags tables carry a numeric `value: 1`; non-flags tables carry a
    // string `value: 'X'`. Flags-ness comes from the SERVER enum (isFlags),
    // not the entry shape, so a quoted value in a flags table is rejected
    // rather than misclassified as non-flags and skipping numeric parity.
    // Grab the raw value token (up to the next `}`/`,` or newline). A plain
    // (optionally signed) integer is accepted; a string is accepted only for
    // non-flags tables; anything else -- e.g. `4 << 1` -- is rejected rather
    // than partially captured as `4` (which would pass a value that is
    // actually 8 at runtime).
    const rawValue = entry.match(/value\s*:\s*([^,}\n]+)/)?.[1]?.trim();
    if (rawValue === undefined) {
      if (isFlags) throw new Error(`${tableName}: flags entry has no value: ${entry}`);
      members.push([name[1], null]);
    } else if (/^'[^']*'$/.test(rawValue)) {
      if (isFlags) throw new Error(`${tableName}: flags entry has a quoted value: ${entry}`);
      members.push([name[1], null]); // string value: non-flags table
    } else if (/^-?\d+$/.test(rawValue)) {
      members.push([name[1], parseInt(rawValue, 10)]);
    } else {
      throw new Error(`${tableName}: entry has a non-plain-numeric value: ${entry}`);
    }
  }
  return { tableName, members };
}

/**
 * Parse the `export type X = 'A' | 'B' | ...;` union that backs each
 * table. Returns the set of union members. Catches a union member with no
 * table entry (the reverse -- a table entry with no union member -- is a
 * tsc error, so it is caught by `npm run build`).
 */
function parseClientUnion(source, typeName) {
  // Work on comment-stripped source so a commented-out (dead) union
  // declaration is invisible -- a `// export type X = ...` retained above the
  // live union would otherwise be matched first and validated in place of the
  // live one. Mirrors parseClientTable.
  const live = stripComments(source);
  // Tolerate both single-line (`= 'A' | 'B';`) and multi-line unions
  // (`=\n  | 'A'\n  | 'B';`). `\s` after the `=` and `[^;]+` (which spans
  // newlines) cover both without requiring a trailing space on the first
  // line. Require exactly one live declaration (fail closed on 0 or >1).
  const hits = [...live.matchAll(new RegExp(`export type ${typeName}\\s*=\\s*([^;]+);`, 'g'))];
  if (hits.length !== 1) return null;
  const rhs = hits[0][1];
  // The RHS must consist ONLY of string literals and `|`/whitespace. A
  // broadening member such as `| string` or `| number` would let the client
  // represent values absent from the server enum; the literal-extraction
  // alone would still report parity, so reject any non-literal token here.
  // The `|` may be leading (continuation lines: `\n | 'A'`) or trailing
  // (single-line: `'A' | 'B'`), so allow a `|` before or after each literal.
  const onlyLiterals =
    /^[\s|]*(?:'[^']*'[\s|]*)*$/.test(rhs);
  if (!onlyLiterals) {
    throw new Error(`${typeName} union contains a non-string-literal member (e.g. | string) -- the client must mirror the server enum exactly`);
  }
  return [...rhs.matchAll(/'([^']*)'/g)].map((x) => x[1]);
}

const clientSource = readFileSync(typesPath, 'utf8');
let failures = 0;
function fail(msg) { console.error(`FAIL ${msg}`); failures++; }

for (const file of readdirSync(enumsDir).filter((f) => f.endsWith('.cs')).sort()) {
  // Strip C# comments first so doc text like `// public enum Example`
  // cannot create a phantom enum that fails the declaration-count check.
  const src = stripCSharpComments(readFileSync(join(enumsDir, file), 'utf8'));
  let parsed;
  try {
    parsed = parseServerEnums(src);
  } catch (err) {
    // A malformed enum (unbalanced braces, etc.) throws; surface it as a
    // clean FAIL rather than a raw Node stack trace. Still fails closed.
    fail(`${file}: parse error -- ${err && err.message ? err.message : err}`);
    continue;
  }

  // Fail closed if the file declares more enums than we parsed: the global
  // regex only matches the fixed `public enum X { ... }` shape, so a
  // declaration we could not parse (e.g. a different brace layout) must not
  // be silently skipped -- a skipped enum escapes the whole check. Files
  // with no `public enum` at all (GamePlatformMapping.cs) are fine.
  const declaredCount = (src.match(/public\s+enum\s+\w+/g) || []).length;
  if (declaredCount > parsed.length) {
    fail(`${file}: declares ${declaredCount} enum(s) but only ${parsed.length} parsed (check-enum-parity.mjs uses a fixed shape -- update the parser or the enum)`);
    continue;
  }

  for (const { name, members, isFlags } of parsed) {
  // Server-internal enums intentionally not mirrored on the client. Must stay
  // in sync with notMirroredOnClient in EnumParityTests.cs. Add an enum here
  // only if it is genuinely server-internal (no UI).
  if (notMirroredOnClient.includes(name)) continue;

  const table = clientTable[name];
  if (!table) {
    fail(`${name}: no client table registered in clientTable (add one in types.ts and here)`);
    continue;
  }

  try {
  const client = parseClientTable(clientSource, table, isFlags);
  if (!client) {
    fail(`${name}: client table ${table} not found in services/types.ts`);
    continue;
  }

  // 'None' is the flags-zero value with no UI checkbox, so the client
  // TABLE omits it -- but ONLY for the two [Flags] enums that declare a None
  // member (MovieFormat, DigitalStore). Every other server member must
  // appear exactly once in the table. The union type must list EVERY server
  // member including 'None' (the type names all members even when the table
  // does not render one). Keep both views: the table-scoped name list (None
  // dropped only for the exempted flags enums) and the full member list
  // (always with None).
  const allServerNames = members.map((m) => m[0]);
  const noneExempt = name === 'MovieFormat' || name === 'DigitalStore';
  const serverNames = noneExempt ? allServerNames.filter((n) => n !== 'None') : allServerNames;
  const clientNames = client.members.map((m) => m[0]);

  // Duplicates: a repeated member passes set-membership but renders a
  // duplicate <Select> option + duplicate React key.
  const dupes = [...new Set(clientNames.filter((n, i) => clientNames.indexOf(n) !== i))];

  const missing = serverNames.filter((n) => !clientNames.includes(n));
  const unknown = clientNames.filter((n) => !serverNames.includes(n));

  // Numeric values only matter where the client stores them (the [Flags]
  // enums). Non-flags enums travel as string names on the wire, so their
  // server values are pinned by the .NET golden test instead.
  const valueDiff = members
    .map((m, i) => ({ m, i: clientNames.indexOf(m[0]) }))
    .find(({ m, i }) => i >= 0 && client.members[i][1] !== null && client.members[i][1] !== m[1]);

  // The union type must list EVERY server member -- including 'None'.
  // (The table may omit 'None', but the type must not.) Comparing against
  // allServerNames catches a removed 'None' that the table-scoped check
  // above would let through.
  // A missing union parse must FAIL, not be skipped: a union-only member
  // (present in the type, absent from the table) compiles fine in tsc and
  // would otherwise escape every assertion here. (The reverse -- a table
  // entry with no union member -- is itself a tsc error caught by build.)
  const union = parseClientUnion(clientSource, name);
  if (!union) {
    fail(`${name} (${table}): could not parse the ${name} union type in services/types.ts (expected: export type ${name} = 'A' | 'B' | ...;)`);
    continue;
  }
  const unionMissing = allServerNames.filter((n) => !union.includes(n));
  const unionUnknown = union.filter((n) => !serverNames.includes(n) && !(noneExempt && n === 'None'));

  if (dupes.length) fail(`${name} (${table}): duplicate client entries: ${dupes.join(', ')}`);
  if (missing.length) fail(`${name} (${table}): missing on client: ${missing.join(', ')}`);
  if (unknown.length) fail(`${name} (${table}): unknown on client: ${unknown.join(', ')}`);
  if (valueDiff) fail(`${name} (${table}): value diverges — server ${valueDiff.m[0]} = ${valueDiff.m[1]} vs client ${client.members[valueDiff.i][1]}`);
  if (unionMissing.length) fail(`${name} (${table}): union type ${name} is missing: ${unionMissing.join(', ')}`);
  if (unionUnknown.length) fail(`${name} (${table}): union type ${name} has unknown member(s): ${unionUnknown.join(', ')}`);

  if (!dupes.length && !missing.length && !unknown.length && !valueDiff && !unionMissing.length && !unionUnknown.length) {
    console.log(`ok   ${name} (${table}): ${members.length} members`);
  }
  } catch (err) {
    // A parse anomaly (unbalanced braces, ambiguous table, non-object
    // element, ...) throws; surface it as a clean FAIL rather than a raw
    // Node stack trace. Still fails closed (exit 1).
    fail(`${name} (${table}): parse error -- ${err && err.message ? err.message : err}`);
  }
  }
}

// Every registered table must correspond to a server enum (guards against
// a table left behind after its enum is deleted). Collects enum names from
// every parsed enum in every file.
const serverEnumNames = new Set(
  readdirSync(enumsDir).filter((f) => f.endsWith('.cs'))
    // Strip C# comments so a commented-out `// public enum X` in doc text
    // cannot register as a phantom server enum. Mirrors the main loop.
    .flatMap((f) => parseServerEnums(stripCSharpComments(readFileSync(join(enumsDir, f), 'utf8'))).map((e) => e.name)),
);
for (const [enumName, table] of Object.entries(clientTable)) {
  if (!serverEnumNames.has(enumName)) fail(`${enumName}: registered in clientTable but has no server enum`);
}

if (failures) {
  console.error(`\n${failures} enum parity check(s) failed — update the client table(s) in src/client/services/types.ts or the server enum(s) in src/server/Collectify.Domain/Enums/`);
  process.exit(1);
}
console.log('\nAll enum tables are in parity with the server.');

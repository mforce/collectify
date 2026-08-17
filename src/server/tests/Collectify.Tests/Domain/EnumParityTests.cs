using System.Reflection;
using System.Text.RegularExpressions;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;

// Flat-rooted namespace on purpose: nesting under Collectify.Tests.Domain
// would shadow Collectify.Domain in any test file under that branch.
namespace Collectify.Tests;

/// <summary>
/// Fails when <c>src/client/services/types.ts</c> and the server enums
/// disagree: a member added or renamed on either side, a renumber of a
/// persisted or flags enum, a duplicate client entry, or a new server
/// enum that has no client table.
///
/// The companion script <c>src/client/scripts/check-enum-parity.mjs</c>
/// runs the complementary comparison in the client CI job without a .NET
/// runtime. This test is the .NET-side half; the two together mean drift
/// fails in whichever CI job picks up the change.
///
/// What is checked:
///   * Member-set equality (every server member appears in the client
///     table and vice versa) — with the documented exception that
///     <c>MovieFormat.None</c> (the flags-zero value, no UI checkbox) is
///     omitted from the client.
///   * No duplicate client entries.
///   * Numeric values for the <c>[Flags]</c> enums (the client stores
///     those numerics).
///   * A golden pin of every *persisted* enum's numeric values
///     (<see cref="PersistedEnumValuesAreStable"/>) — this is what catches
///     a <c>GamePlatform</c> renumber, which the set/flags checks cannot
///     see because the client stores those as string names.
///   * Registration completeness, via reflection over the Domain assembly:
///     every client-mirrored enum the assembly exposes must be registered,
///     and every registered name must exist.
///
/// What is deliberately NOT checked: dropdown order. The client table is
/// the <c>&lt;Select&gt;</c> order, which may differ from the server
/// declaration order for display reasons. A pure dropdown reorder changes
/// no stored data, so it is not a parity violation.
/// </summary>
public class EnumParityTests
{
    // Test assembly lands at <root>/src/server/tests/Collectify.Tests/
    // bin/Debug/net10.0/ (BaseDirectory points *into* net10.0), so the
    // repo root is seven levels up.
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../../../.."));

    private static string ClientTypesPath => Path.Combine(RepoRoot, "src", "client", "services", "types.ts");

    // enum name -> client table constant. Keep in sync with clientTable in
    // check-enum-parity.mjs. Neither file reads the other, so agreement is
    // enforced INDIRECTLY: EveryClientTableIsRegistered asserts THIS map
    // against the server enums, and the MJS asserts its own map against the
    // client tables -- each half fails closed independently if its map
    // drifts. Add an enum to BOTH maps.
    private static readonly IReadOnlyDictionary<string, string> ClientTable = new Dictionary<string, string>
    {
        ["CollectionStatus"] = "COLLECTION_STATUSES",
        ["Condition"] = "CONDITIONS",
        ["WatchStatus"] = "WATCH_STATUSES",
        ["CompletionStatus"] = "COMPLETION_STATUSES",
        ["MovieFormat"] = "MOVIE_FORMAT_FLAGS",
        ["MusicFormat"] = "MUSIC_FORMATS",
        ["DigitalStore"] = "DIGITAL_STORES",
        ["GamePlatform"] = "GAME_PLATFORMS",
    };

    // EF persists every client-mirrored enum as an int (no HasConversion to
    // string), so renumbering or reordering any of them is a data-corruption
    // event. PersistedValues pins every name->value; the golden test also
    // asserts the pinned set is exactly the enum's members, which catches a
    // renumber, a removal, and a newly-added member that was never pinned.
    // Add a member and its value here in the same change; never renumber an
    // existing value.
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> PersistedValues =
        new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["CollectionStatus"] = new Dictionary<string, int>
            {
                ["Owned"] = 0, ["Wishlist"] = 1, ["OnOrder"] = 2, ["Sold"] = 3,
            },
            ["Condition"] = new Dictionary<string, int>
            {
                ["New"] = 0, ["LikeNew"] = 1, ["Good"] = 2, ["Fair"] = 3, ["Poor"] = 4,
            },
            ["WatchStatus"] = new Dictionary<string, int>
            {
                ["Unwatched"] = 0, ["Watching"] = 1, ["Watched"] = 2,
            },
            ["CompletionStatus"] = new Dictionary<string, int>
            {
                ["NotStarted"] = 0, ["Playing"] = 1, ["Beaten"] = 2, ["HundredPercent"] = 3, ["Abandoned"] = 4,
            },
            // [Flags]; values are powers of two. 'None' (0) is pinned for
            // completeness even though the client table omits it.
            ["MovieFormat"] = new Dictionary<string, int>
            {
                ["None"] = 0, ["Dvd"] = 1, ["BluRay"] = 2, ["UhdBluRay"] = 4, ["Vhs"] = 8, ["Digital"] = 16,
            },
            ["MusicFormat"] = new Dictionary<string, int>
            {
                ["Cd"] = 0, ["Vinyl"] = 1, ["Other"] = 2,
            },
            ["DigitalStore"] = new Dictionary<string, int>
            {
                ["Steam"] = 0, ["Gog"] = 1, ["Epic"] = 2, ["Xbox"] = 3, ["Psn"] = 4, ["Nintendo"] = 5, ["Other"] = 99,
            },
            ["GamePlatform"] = new Dictionary<string, int>
            {
                ["Other"] = 0, ["Pc"] = 1, ["Mac"] = 2, ["Linux"] = 3, ["Mobile"] = 4,
                ["XboxOriginal"] = 10, ["Xbox360"] = 11, ["XboxOne"] = 12, ["XboxSeriesXS"] = 13,
                ["Ps1"] = 20, ["Ps2"] = 21, ["Ps3"] = 22, ["Ps4"] = 23, ["Ps5"] = 24, ["Psp"] = 25, ["PsVita"] = 26,
                ["Nes"] = 30, ["Snes"] = 31, ["N64"] = 32, ["GameCube"] = 33, ["Wii"] = 34, ["WiiU"] = 35,
                ["Switch"] = 36, ["Switch2"] = 37, ["GameBoy"] = 40, ["GameBoyColor"] = 41,
                ["GameBoyAdvance"] = 42, ["NintendoDs"] = 43, ["Nintendo3Ds"] = 44,
                ["SegaGenesis"] = 50, ["SegaSaturn"] = 51, ["SegaDreamcast"] = 52,
            },
        };

    /// <summary>
    /// Integer enum values that were retired and must never be reassigned.
    /// Derived from GamePlatformBackfill.RetiredPlatformValues (the single
    /// source of truth) rather than a hand-maintained copy -- if a value is
    /// retired in the backfill, this picks it up automatically, so a new
    /// member reusing a retired value is caught here instead of being
    /// silently clobbered on every boot. 60 was GamePlatform.SteamDeck,
    /// retired in #103.
    /// </summary>
    // The retired from-values all belong to GamePlatform (the backfill only
    // reclassifies GamePlatform.Platform rows), so key the reserved set by
    // "GamePlatform" -- the enum name EnumMembers() expects. If a future
    // retirement touches another enum, extend this mapping.
    private static readonly IReadOnlyDictionary<string, int[]> ReservedValues =
        new Dictionary<string, int[]>
        {
            ["GamePlatform"] = GamePlatformBackfill.RetiredPlatformValues.Keys.ToArray(),
        };

    public static IEnumerable<object[]> AllTables => ClientTable
        .Select(kv => new object[] { kv.Key, kv.Value });

    [Theory]
    [MemberData(nameof(AllTables))]
    public void ClientTableMatchesServerEnum(string enumName, string tableName)
    {
        var server = EnumMembers(enumName);
        var (clientNames, clientValues) = ParseClientTable(File.ReadAllText(ClientTypesPath), tableName, server.IsFlags);

        // 'None' is the flags-zero value with no UI checkbox; the client
        // table omits it -- but ONLY for MovieFormat (the sole enum with a
        // None member today), mirroring check-enum-parity.mjs. A future
        // None on another enum must appear in the table.
        var noneExempt = enumName == "MovieFormat";
        var serverNames = (noneExempt ? server.Names.Where(n => n != "None") : server.Names).ToArray();

        // Duplicates: a repeated member passes set-membership but renders
        // a duplicate <Select> option + duplicate React key.
        var dupes = clientNames.Where((n, i) => clientNames.IndexOf(n) != i).Distinct().ToArray();

        var missingOnClient = serverNames.Except(clientNames).ToArray();
        var unknownOnClient = clientNames.Except(serverNames).ToArray();

        // Numeric values: only compared for client entries that carry a
        // numeric value (the flags tables). Name-keyed so a client entry
        // without a value (non-flags enums) is skipped.
        var serverByName = server.Names.Zip(server.Values).ToDictionary(p => p.First, p => p.Second);
        var valueDiffs = clientNames
            .Select((n, i) => (Name: n, Client: clientValues[i]))
            .Where(p => p.Client != null && serverByName.ContainsKey(p.Name) && p.Client != serverByName[p.Name])
            .Select(p => $"{p.Name}: client={p.Client} server={serverByName[p.Name]}")
            .ToArray();

        Assert.True(dupes.Length == 0,
            $"{tableName} has duplicate client entries: {string.Join(", ", dupes)}");
        Assert.True(missingOnClient.Length == 0,
            $"{tableName} is missing server member(s): {string.Join(", ", missingOnClient)}");
        Assert.True(unknownOnClient.Length == 0,
            $"{tableName} has member(s) the server enum {enumName} does not: {string.Join(", ", unknownOnClient)}");
        Assert.True(valueDiffs.Length == 0,
            $"{tableName} numeric value diverges from {enumName}: {string.Join("; ", valueDiffs)}");
    }

    [Fact]
    public void PersistedEnumValuesAreStable()
    {
        // The check that catches a renumber: the client stores most enums as
        // string names, so the set/flags checks are blind to a value change.
        // Pin every persisted value AND assert the pinned set is exactly the
        // enum's members, so a renumber, a removal, or a newly-added member
        // that was never pinned all fail here.

        // First, every mirrored enum must have a golden map. Without this, a
        // new enum added to ClientTable (and mirrored in TS) but omitted from
        // PersistedValues would slip past the value loop below and its later
        // renumber would be invisible.
        var missingGolden = ClientTable.Keys.Except(PersistedValues.Keys).ToArray();
        Assert.True(missingGolden.Length == 0,
            "Mirrored enum(s) without a golden value map in PersistedValues (add one, or remove from ClientTable if not persisted): " +
            string.Join(", ", missingGolden));

        var diffs = new List<string>();
        foreach (var (enumName, pinned) in PersistedValues)
        {
            var server = EnumMembers(enumName);
            var byName = server.Names.Zip(server.Values).ToDictionary(p => p.First, p => p.Second);
            foreach (var (name, value) in pinned)
            {
                if (!byName.TryGetValue(name, out var actual))
                {
                    diffs.Add($"{enumName}.{name}: removed (was {value})");
                    continue;
                }
                if (actual != value)
                    diffs.Add($"{enumName}.{name}: renumbered {value} -> {actual}");
            }
            // A member present on the enum but not pinned = a new member
            // added without updating this golden (or an unregistered enum).
            var unpinned = byName.Keys.Where(n => !pinned.ContainsKey(n)).ToArray();
            if (unpinned.Length > 0)
                diffs.Add($"{enumName}: member(s) not pinned (add them to PersistedValues): {string.Join(", ", unpinned)}");
        }

        Assert.True(diffs.Count == 0,
            "Persisted enum value(s) changed (data corruption — renumbering is not allowed): " +
            string.Join("; ", diffs));
    }

    [Fact]
    public void EveryClientTableIsRegistered()
    {
        // Reflection over the Domain assembly, not a directory listing, so
        // a mirrored enum declared outside Collectify.Domain/Enums is still
        // seen. Both directions are checked:
        //   * every registered table names an enum that actually exists
        //     (no dangling table);
        //   * every client-mirrored enum is registered (no enum escapes the
        //     check), except those explicitly excluded below.
        var domainEnums = typeof(CollectionStatus).Assembly.GetTypes()
            .Where(t => t.IsEnum)
            .Select(t => t.Name)
            .ToHashSet();

        var dangling = ClientTable.Keys.Where(n => !domainEnums.Contains(n)).ToArray();
        Assert.True(dangling.Length == 0,
            "Registered client table(s) with no matching server enum: " + string.Join(", ", dangling));

        // Enums that are intentionally NOT mirrored on the client (no UI).
        // Add an enum here only if it is genuinely server-internal; a
        // client-facing enum belongs in ClientTable + PersistedValues.
        var notMirroredOnClient = new string[]
        {
            // (none today)
        };
        var unregistered = domainEnums
            .Except(ClientTable.Keys)
            .Where(n => !notMirroredOnClient.Contains(n))
            .ToArray();
        Assert.True(unregistered.Length == 0,
            "Server enum(s) with no client registration (add to ClientTable + PersistedValues, or to notMirroredOnClient if server-internal): " +
            string.Join(", ", unregistered));
    }

    [Fact]
    public void ReservedValuesAreNotReusedByLiveMembers()
    {
        // A live member reusing a retired value would be silently rewritten
        // by the backfill on every startup (see GamePlatformBackfill). Assert
        // no live member's numeric value collides with the reserved set.
        var collisions = new List<string>();
        foreach (var (enumName, reserved) in ReservedValues)
        {
            var (_, values, _) = EnumMembers(enumName);
            var liveSet = values.ToHashSet();
            var reused = reserved.Where(r => liveSet.Contains(r)).ToArray();
            collisions.AddRange(reused.Select(r => $"{enumName}: {r} is reserved but is a live member"));
        }
        Assert.True(collisions.Count == 0,
            "Reserved (retired) enum value(s) are in use by a live member -- the startup backfill would clobber them: " +
            string.Join("; ", collisions));
    }

    private static (string[] Names, int[] Values, bool IsFlags) EnumMembers(string enumName)
    {
        var t = typeof(CollectionStatus).Assembly.GetTypes()
            .FirstOrDefault(x => x.IsEnum && x.Name == enumName)
            ?? throw new ArgumentException($"Unknown enum: {enumName}", nameof(enumName));
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(f => f.MetadataToken);
        return (
            fields.Select(f => f.Name).ToArray(),
            fields.Select(f => Convert.ToInt32(f.GetRawConstantValue())).ToArray(),
            t.IsDefined(typeof(FlagsAttribute)));
    }

    /// <summary>
    /// Minimal parser for the constant-array shape used in types.ts:
    /// <c>export const NAME: { ... value: 'X' | 1 ... }[] = [ {...}, ... ];</c>
    /// Deliberately regex-based — the tables are machine-shaped by
    /// convention, and the mjs script in CI uses the same approach. The
    /// array end is found by scanning for a top-level ']' (not a raw ';'
    /// search, which a ']' inside a label string would break). Returns
    /// null for entries without a numeric value (non-flags enums), so the
    /// value check skips them.
    /// </summary>
    private static (string[] Names, int?[] Values) ParseClientTable(string source, string tableName, bool isFlags)
    {
        // Work on comment-stripped source so a commented-out (dead) table
        // declaration is invisible to the lookup -- not just a commented-out
        // entry inside a live table. Mirrors check-enum-parity.mjs.
        var live = StripComments(source);

        // Anchor on a `:` or `=` after the name so a decoy const whose name
        // merely starts with the real table name (e.g. `GAME_PLATFORMS_V2`
        // declared above the real `GAME_PLATFORMS`) cannot shadow it. Fail
        // closed on ambiguity (0 or >1 hits) rather than taking the first
        // match. The lookup runs on the comment-stripped `live` source so a
        // commented-out (dead) table is invisible to it.
        var hits = Regex.Matches(live, $@"export const {Regex.Escape(tableName)}\s*[:=]");
        Assert.True(hits.Count == 1,
            $"Client table {tableName}: expected exactly 1 declaration, found {hits.Count}");
        var arrayStart = hits[0].Index;
        // Use IndexOf('=') (not "= ") so `= [` without a space still
        // resolves; fail closed if there is no '=' at all. Mirrors the MJS.
        var eq = live.IndexOf('=', arrayStart);
        Assert.True(eq >= 0, $"Client table {tableName}: no '=' after declaration");
        var open = live.IndexOf('[', eq);
        var close = FindArrayEnd(live, open);
        var body = live[open..(close + 1)];
        // NOTE: this parser only inspects the array LITERAL. A deliberate
        // post-declaration runtime mutation of the exported const (e.g.
        // `TABLE.push(...)` / `TABLE[i].value = ...`) is OUT OF SCOPE: it is
        // not client/server *drift* (the literal still matches the server),
        // it is a separate threat best addressed by typing the tables
        // `readonly` or code review. A regex scan for such mutations was
        // tried and dropped because it false-positives on ordinary reads and
        // non-mutating methods (`.concat`) while missing real mutators
        // (`.fill`, `.copyWithin`) -- net negative for a mandatory CI gate.
        // Mirrors check-enum-parity.mjs.

        // Every top-level element must be an inline object literal. A spread
        // (`...extraPlatforms`) or any other non-object element would be
        // silently ignored by the `{...}` scan, letting a duplicate member
        // ride in from an external array -- fail closed instead. Mirrors
        // check-enum-parity.mjs.
        // Split on commas at depth 0 (not a naive Split(','), which would
        // break inside object literals that contain their own commas).
        var inner = body[1..^1];
        var topLevel = new List<string>();
        var depth = 0;
        bool inStr = false;
        int segStart = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (inStr)
            {
                if (c == '\\') i++;
                else if (c == '\'') inStr = false;
            }
            else if (c == '\'') inStr = true;
            else if (c == '{') depth++;
            else if (c == '}') depth--;
            else if (c == ',' && depth == 0)
            {
                topLevel.Add(inner[segStart..i].Trim());
                segStart = i + 1;
            }
        }
        var tail = inner[segStart..].Trim();
        if (tail.Length > 0) topLevel.Add(tail);
        foreach (var el in topLevel)
        {
            if (el.Length == 0) continue;
            if (!el.StartsWith('{') || !el.EndsWith('}'))
                throw new InvalidOperationException(
                    $"{tableName}: table element is not an inline object literal (e.g. a spread): {el}");
        }

        var entries = Regex.Matches(body, @"\{[^{}]*\}").Select(m => m.Value).ToArray();
        Assert.True(entries.Length > 0, $"{tableName} has no entries");
        // The brace scan must find exactly one entry per non-empty top-level
        // element. Fewer means a nested object literal shadowed an entry's real
        // value (tsc's excess-property check rejects this at the build gate; this
        // is defense-in-depth for the parity check on its own). Mirrors the MJS.
        var nonEmptyTopLevel = topLevel.Count(el => el.Length > 0);
        Assert.True(entries.Length == nonEmptyTopLevel,
            $"{tableName}: entry count ({entries.Length}) != top-level element count ({nonEmptyTopLevel}) -- nested object literal or unbalanced braces");

        var names = new List<string>();
        var values = new List<int?>();
        foreach (var e in entries)
        {
            var name = Regex.Match(e, @"(?:key|value)\s*:\s*'([^']*)'");
            Assert.True(name.Success, $"Entry in {tableName} has no string key/value: {e}");
            names.Add(name.Groups[1].Value);

            // Flags-ness comes from the SERVER enum (isFlags), not the entry
            // shape, so a quoted value in a flags table is rejected rather
            // than misclassified as non-flags and skipping numeric parity.
            // Grab the raw value token (up to the next `}`/`,`). A plain
            // (optionally signed) integer is accepted; a quoted string is
            // accepted only for non-flags tables; anything else -- e.g. a
            // numeric expression like `4 << 1` -- is rejected. Mirrors
            // check-enum-parity.mjs.
            var raw = Regex.Match(e, @"value\s*:\s*([^,}\n]+)");
            var token = raw.Success ? raw.Groups[1].Value.Trim() : null;
            const char Quote = '\'';
            if (token is null)
            {
                if (isFlags) throw new InvalidOperationException($"Entry in {tableName} (flags) has no value: {e}");
                values.Add(null);
            }
            else if (token.Length >= 2 && token[0] == Quote && token[^1] == Quote)
            {
                if (isFlags) throw new InvalidOperationException($"Entry in {tableName} (flags) has a quoted value: {e}");
                values.Add(null);
            }
            else if (int.TryParse(token, out var parsed)) values.Add(parsed);
            else throw new InvalidOperationException(
                $"Entry in {tableName} has a non-plain-numeric value: {e}");
        }

        return (names.ToArray(), values.ToArray());
    }

    /// <summary>
    /// Strip `//` line comments (outside single-quoted string literals) and
    /// `/* ... *​/` block comments from source. String values in these tables
    /// contain neither `//` nor `/*`, so a comment-aware scan is safe. Applied
    /// to the WHOLE source before locating table declarations, so a
    /// commented-out (dead) table is invisible to the lookup. Mirrors
    /// check-enum-parity.mjs stripComments.
    /// </summary>
    private static string StripComments(string source)
    {
        var noBlocks = Regex.Replace(source, @"/\*[\s\S]*?\*/", "");
        return string.Join("\n", noBlocks.Split('\n').Select(StripLineComment));
    }

    /// <summary>
    /// Truncate a line at the first `//` that is not inside a string literal
    /// (single-quoted, backslash-escaped), so a trailing `// { ... }` comment
    /// is dropped while a `{ value: 'A' }` entry is kept. Mirrors the MJS
    /// parser.
    /// </summary>
    private static string StripLineComment(string line)
    {
        bool inStr = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inStr)
            {
                if (c == '\\') i++;
                else if (c == '\'') inStr = false;
            }
            else if (c == '\'') inStr = true;
            else if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                return line[..i];
            }
        }
        return line;
    }

    /// <summary>
    /// Find the index of the ']' that closes the top-level array opened at
    /// <paramref name="start"/>, tracking bracket depth and skipping string
    /// literals (so a ']' inside a label string does not end the scan).
    /// Mirrors findArrayEnd in check-enum-parity.mjs.
    /// </summary>
    private static int FindArrayEnd(string source, int start)
    {
        int depth = 0;
        bool inStr = false;
        char strCh = '\0';
        for (var i = start; i < source.Length; i++)
        {
            var c = source[i];
            if (inStr)
            {
                if (c == '\\') i++;
                else if (c == strCh) inStr = false;
                continue;
            }
            if (c is '\'' or '"' or '`') { inStr = true; strCh = c; }
            else if (c == '[') depth++;
            else if (c == ']') { depth--; if (depth == 0) return i; }
        }
        throw new InvalidOperationException("unterminated array literal");
    }
}

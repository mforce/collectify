using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Collectify.PostgresTests;

public sealed class PostgresProviderSelectionTests
{
    [Fact]
    public void Provenance_CoversEveryLegacyStateWithVerifiedHashes()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var repositoryFixtureRoot = Path.Combine(repositoryRoot, "src", "server", "tests", "Collectify.PostgresTests", "Fixtures");
        var provenancePath = Path.Combine(repositoryFixtureRoot, "generated", "provenance.json");
        Assert.True(File.Exists(provenancePath), $"Missing provenance file: {provenancePath}");

        var inventoryBytes = File.ReadAllBytes(Path.Combine(fixtureRoot, "legacy-state-inventory.json"));
        var provenanceBytes = File.ReadAllBytes(provenancePath);
        AssertCanonicalJson(provenanceBytes);
        using var inventoryDocument = JsonDocument.Parse(inventoryBytes);
        using var provenanceDocument = JsonDocument.Parse(provenanceBytes);
        var inventory = inventoryDocument.RootElement.GetProperty("states").EnumerateArray().ToArray();
        var root = provenanceDocument.RootElement;
        var states = root.GetProperty("states").EnumerateArray().ToArray();
        Assert.Equal(12, root.GetProperty("stateCount").GetInt32());
        Assert.Equal(12, inventory.Length);
        Assert.Equal(12, states.Length);
        Assert.Equal(Sha256(inventoryBytes), root.GetProperty("inventorySha256").GetString());

        var paths = new HashSet<string>(StringComparer.Ordinal);
        var stateDirectories = new HashSet<string>(StringComparer.Ordinal);
        var familyShapes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var globalShapes = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < states.Length; index++)
        {
            var state = states[index];
            var expected = inventory[index];
            foreach (var name in new[] { "commit", "efCoreVersion", "npgsqlVersion", "modelInputSignature" })
                Assert.Equal(expected.GetProperty(name).GetString(), state.GetProperty(name).GetString());
            var commit = state.GetProperty("commit").GetString()!;
            Assert.Matches("^[0-9a-f]{40}$", commit);
            var expectedFamily = index < 4 ? "P0" : index < 8 ? "P1" : index < 10 ? "P2" : index == 10 ? "P3" : "P4";
            Assert.Equal(expectedFamily, state.GetProperty("family").GetString());
            AssertHash(state.GetProperty("rawDumpSha256").GetString()!);

            var hashes = new List<string>();
            foreach (var pair in new[] { ("normalizedFixturePath", "normalizedFixtureSha256"), ("catalogManifestPath", "catalogManifestSha256") })
            {
                var relative = state.GetProperty(pair.Item1).GetString()!;
                Assert.Equal(relative, relative.Replace('\\', '/'));
                Assert.DoesNotContain("..", relative.Split('/'));
                Assert.False(Path.IsPathRooted(relative));
                Assert.StartsWith($"src/server/tests/Collectify.PostgresTests/Fixtures/generated/{commit}/", relative, StringComparison.Ordinal);
                Assert.True(paths.Add(relative), $"Duplicate fixture path: {relative}");
                var full = Path.GetFullPath(Path.Combine(repositoryRoot, relative));
                Assert.StartsWith(Path.GetFullPath(repositoryRoot) + Path.DirectorySeparatorChar, full, StringComparison.Ordinal);
                Assert.True(File.Exists(full), $"Missing generated artifact: {relative}");
                var expectedHash = state.GetProperty(pair.Item2).GetString()!;
                AssertHash(expectedHash);
                Assert.Equal(expectedHash, Sha256(File.ReadAllBytes(full)));
                hashes.Add(expectedHash);
                if (pair.Item1 == "catalogManifestPath") ValidateManifest(File.ReadAllBytes(full));
            }
            stateDirectories.Add(commit);
            var shape = string.Join(':', hashes);
            Assert.False(globalShapes.TryGetValue(shape, out var otherFamily) && otherFamily != expectedFamily,
                $"Shape is ambiguous across {otherFamily} and {expectedFamily}");
            globalShapes[shape] = expectedFamily;
            if (!familyShapes.TryGetValue(expectedFamily, out var variants)) familyShapes[expectedFamily] = variants = [];
            if (!variants.Contains(shape, StringComparer.Ordinal)) variants.Add(shape);
            var variantNumber = variants.IndexOf(shape) + 1;
            Assert.Equal(variantNumber == 1 ? expectedFamily : $"{expectedFamily}-v{variantNumber}", state.GetProperty("variant").GetString());
        }

        var generatedRoot = Path.Combine(repositoryFixtureRoot, "generated");
        var actualDirectories = Directory.GetDirectories(generatedRoot).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(stateDirectories.Order(StringComparer.Ordinal), actualDirectories);
        var expectedFiles = paths.Select(x => Path.GetFullPath(Path.Combine(repositoryRoot, x))).Append(provenancePath).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedFiles, Directory.GetFiles(generatedRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        var forbidden = new Regex(@"(?i)(password\s*=|host\s*=|username\s*=|127\.0\.0\.1:\d{2,}|localhost:\d{2,}|CANARY_)");
        Assert.DoesNotMatch(forbidden, Encoding.UTF8.GetString(provenanceBytes));
    }

    [Fact]
    public void LegacyStateInventory_MatchesIndependentFirstParentSignatures()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var inventoryPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-state-inventory.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(inventoryPath));
        var root = document.RootElement;
        var first = root.GetProperty("firstPostgresCommit").GetString()!;
        var @base = root.GetProperty("baseCommit").GetString()!;
        var inputs = root.GetProperty("modelInputs").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var packageInput = root.GetProperty("packageInput").GetString()!;
        var expected = root.GetProperty("states").EnumerateArray().Select(State.FromJson).ToArray();

        Assert.Equal(12, expected.Length);
        Assert.Equal(expected.Length, expected.Select(x => x.Commit).Distinct(StringComparer.Ordinal).Count());
        Assert.All(expected, state => Assert.Matches("^[0-9a-f]{40}$", state.Commit));

        var commits = Git(repositoryRoot, "log", "--first-parent", "--reverse", "--format=%H", $"{first}^..{@base}")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(38, commits.Length);

        var selected = new List<State>();
        State? previous = null;
        foreach (var commit in commits)
        {
            using var bytes = new MemoryStream();
            foreach (var input in inputs)
            {
                var listing = GitBytes(repositoryRoot, "ls-tree", "-r", commit, "--", input);
                bytes.Write(listing);
            }
            var signature = Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
            var packages = Git(repositoryRoot, "show", $"{commit}:{packageInput}");
            var xml = XDocument.Parse(packages);
            string Version(string name) => xml.Descendants("PackageVersion")
                .Single(x => (string?)x.Attribute("Include") == name).Attribute("Version")!.Value;
            var current = new State(commit, Version("Microsoft.EntityFrameworkCore.Sqlite"),
                Version("Npgsql.EntityFrameworkCore.PostgreSQL"), signature);
            if (previous is null || previous.ModelInputSignature != current.ModelInputSignature ||
                previous.EfCoreVersion != current.EfCoreVersion || previous.NpgsqlVersion != current.NpgsqlVersion)
                selected.Add(current);
            previous = current;
        }

        Assert.Equal(expected, selected);
    }

    private static string ResolveRepositoryRoot()
    {
        var configured = Environment.GetEnvironmentVariable("COLLECTIFY_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(Path.Combine(configured, ".git")))
            return Path.GetFullPath(configured);
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
        throw new InvalidOperationException("Unable to resolve repository root.");
    }

    private static string Git(string root, params string[] arguments) =>
        Encoding.UTF8.GetString(GitBytes(root, arguments));

    private static byte[] GitBytes(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
        return output.ToArray();
    }

    private static string Sha256(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static void AssertHash(string value) => Assert.Matches("^[0-9a-f]{64}$", value);

    private static void AssertCanonicalJson(byte[] bytes)
    {
        Assert.True(bytes.Length > 1 && bytes[^1] == (byte)'\n' && bytes[^2] != (byte)'\n');
        using var document = JsonDocument.Parse(bytes);
        var canonical = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }) + "\n";
        Assert.Equal(canonical, Encoding.UTF8.GetString(bytes));
    }

    private static void ValidateManifest(byte[] bytes)
    {
        AssertCanonicalJson(bytes);
        using var document = JsonDocument.Parse(bytes);
        var required = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["databaseSchema"] = ["databaseOwnerIsCurrentUser", "schemaOwnerIsDatabaseOwner", "currentUserHasUsage", "currentUserHasCreate", "publicHasCreate"],
            ["relations"] = ["name", "kind", "persistence", "ownerIsCurrentUser", "acl"],
            ["columns"] = ["relation", "name", "typeOid", "typeName", "typmod", "length", "notNull", "collation", "default", "identity", "generated", "ownedSequence"],
            ["sequences"] = ["name", "dataType", "start", "increment", "minimum", "maximum", "cache", "cycle", "ownerIsCurrentUser", "dependencyType", "ownedRelation", "ownedColumn"],
            ["constraints"] = ["relation", "name", "type", "columns", "referencedRelation", "referencedColumns", "matchType", "updateAction", "deleteAction", "validated", "deferrable", "initiallyDeferred", "definition"],
            ["indexes"] = ["relation", "name", "unique", "valid", "ready", "method", "keyCount", "columns", "operatorClasses", "collations", "options", "expressions", "predicate"],
            ["triggers"] = ["relation", "name", "enabled", "definition"],
            ["rewriteRules"] = ["relation", "name", "event", "instead", "enabled", "definition"],
            ["rls"] = ["relation", "enabled", "forced"],
            ["policies"] = ["relation", "name", "permissive", "roles", "command", "using", "check"],
            ["inboundDependencies"] = ["sourceClass", "sourceIdentity", "targetRelation", "targetColumn", "dependencyType"]
        };
        Assert.Equal(required.Keys.Order(StringComparer.Ordinal), document.RootElement.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal));
        Assert.Single(document.RootElement.GetProperty("databaseSchema").EnumerateArray());
        foreach (var category in new[] { "relations", "columns", "constraints", "indexes", "rls" })
            Assert.NotEmpty(document.RootElement.GetProperty(category).EnumerateArray());
        foreach (var (category, fields) in required)
        {
            var rows = document.RootElement.GetProperty(category).EnumerateArray().ToArray();
            var serialized = rows.Select(x => JsonSerializer.Serialize(x)).ToArray();
            Assert.Equal(serialized.Order(StringComparer.Ordinal), serialized);
            foreach (var row in rows)
                foreach (var field in fields) Assert.True(row.TryGetProperty(field, out _), $"{category} row lacks {field}");
        }
    }

    private sealed record State(string Commit, string EfCoreVersion, string NpgsqlVersion, string ModelInputSignature)
    {
        public static State FromJson(JsonElement value) => new(
            value.GetProperty("commit").GetString()!, value.GetProperty("efCoreVersion").GetString()!,
            value.GetProperty("npgsqlVersion").GetString()!, value.GetProperty("modelInputSignature").GetString()!);
    }
}

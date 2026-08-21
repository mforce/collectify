using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Collectify.PostgresTests;

public sealed class PostgresProviderSelectionTests
{
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

    private sealed record State(string Commit, string EfCoreVersion, string NpgsqlVersion, string ModelInputSignature)
    {
        public static State FromJson(JsonElement value) => new(
            value.GetProperty("commit").GetString()!, value.GetProperty("efCoreVersion").GetString()!,
            value.GetProperty("npgsqlVersion").GetString()!, value.GetProperty("modelInputSignature").GetString()!);
    }
}

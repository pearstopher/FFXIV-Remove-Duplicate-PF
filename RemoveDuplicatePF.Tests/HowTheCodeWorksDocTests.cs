using System.Text.RegularExpressions;

namespace RemoveDuplicatePF.Tests;

// docs/how-the-code-works.md must describe every source file that ships in the plugin (#100).
public class HowTheCodeWorksDocTests
{
    private static readonly string[] ShippedFolders = ["RemoveDuplicatePF", "RemoveDuplicatePF.Core"];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Repo root (global.json) not found");
    }

    private static string Doc() => File.ReadAllText(Path.Combine(RepoRoot(), "docs", "how-the-code-works.md"));

    private static IEnumerable<string> ShippedSourceFiles()
    {
        var root = RepoRoot();
        foreach (var folder in ShippedFolders)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!relative.Contains("/obj/") && !relative.Contains("/bin/"))
                    yield return relative;
            }
        }
    }

    [Fact]
    public void Every_shipped_source_file_is_linked()
    {
        var doc = Doc();
        var missing = ShippedSourceFiles().Where(f => !doc.Contains($"](../{f})", StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0, "Add these files to docs/how-the-code-works.md: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_linked_source_file_exists()
    {
        var root = RepoRoot();
        var links = Regex.Matches(Doc(), @"\]\(\.\./([^)]+\.cs)\)").Select(m => m.Groups[1].Value);
        var stale = links.Where(l => !File.Exists(Path.Combine(root, l))).ToList();

        Assert.True(stale.Count == 0, "These files no longer exist: " + string.Join(", ", stale));
    }
}

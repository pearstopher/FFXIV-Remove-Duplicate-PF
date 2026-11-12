using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RemoveDuplicatePF.Tests;

// The plugin version lives in the csproj, because the official Dalamud repo builds the committed csproj (#110).
// release.yml checks that the tag matches it, so it must be MAJOR.MINOR.PATCH.0 like the tags (v1.2.3 -> 1.2.3.0).
public class ReleaseVersionTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Repo root (global.json) not found");
    }

    private static string CsprojVersion()
    {
        var csproj = XDocument.Load(Path.Combine(RepoRoot(), "RemoveDuplicatePF", "RemoveDuplicatePF.csproj"));
        return csproj.Descendants("Version").Single().Value.Trim();
    }

    private static string PublishedVersion()
    {
        using var repo = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "repo.json")));
        return repo.RootElement[0].GetProperty("AssemblyVersion").GetString()!;
    }

    [Fact]
    public void Csproj_version_matches_the_tag_format()
    {
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+\.0$"), CsprojVersion());
    }

    [Fact]
    public void Csproj_version_is_not_older_than_the_published_version()
    {
        var csproj = Version.Parse(CsprojVersion());
        var published = Version.Parse(PublishedVersion());

        Assert.True(csproj >= published,
            $"RemoveDuplicatePF.csproj says {csproj}, but repo.json already publishes {published}. Bump <Version> in the csproj.");
    }
}

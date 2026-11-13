using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RemoveDuplicatePF.Tests;

// The draft manifest for the official Dalamud repo must stay in step with the plugin (#111),
// e.g. after a repo rename like #108.
public class SubmissionManifestTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Repo root (global.json) not found");
    }

    private static string Field(string name)
    {
        var toml = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "official-repo-submission", "manifest.toml"));
        var match = Regex.Match(toml, $@"^{name}\s*=\s*(.+?)\s*$", RegexOptions.Multiline);
        Assert.True(match.Success, $"manifest.toml has no {name} field");
        return match.Groups[1].Value;
    }

    private static string CsprojValue(string element)
    {
        var csproj = XDocument.Load(Path.Combine(RepoRoot(), "RemoveDuplicatePF", "RemoveDuplicatePF.csproj"));
        return csproj.Descendants(element).Single().Value.Trim();
    }

    [Fact]
    public void Repository_is_the_csproj_repo_url()
    {
        Assert.Equal($"\"{CsprojValue("RepoUrl")}.git\"", Field("repository"));
    }

    [Fact]
    public void Project_path_points_at_the_plugin_csproj()
    {
        var path = Field("project_path").Trim('"');
        Assert.True(File.Exists(Path.Combine(RepoRoot(), path, "RemoveDuplicatePF.csproj")), $"No RemoveDuplicatePF.csproj in {path}/");
    }

    [Fact]
    public void Owners_and_changelog_are_filled_in()
    {
        Assert.Matches(new Regex(@"^\[\s*""[^""]+"""), Field("owners"));
        Assert.Matches(new Regex(@"^""[^""]+""$"), Field("changelog"));
    }

    // The commit the build bot builds must be a full 40-character hash, not a placeholder (#126).
    // That it's the v1.2.3 tagged commit is checked by hand: CI's shallow checkout has no tags.
    [Fact]
    public void Commit_is_a_full_hash()
    {
        Assert.Matches(new Regex(@"^""[0-9a-f]{40}""$"), Field("commit"));
    }
}

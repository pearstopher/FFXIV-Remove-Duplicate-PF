namespace RemoveDuplicatePF.Core;

/// <summary>
/// Remembers which listings (by match key, <see cref="DescriptionMatch.Key"/>) appeared on each
/// page of the current search, so later pages can hide what earlier pages already showed (#75).
/// </summary>
/// <remarks>
/// Owner's rules:
/// <list type="bullet">
/// <item>Earlier pages win: on page P, a key seen on any page before P is hidden.</item>
/// <item>Searches always start on page 1, so starting page 1 clears everything. Being on a
/// later page means the earlier pages were seen recently.</item>
/// <item>Each visit to a page replaces that page's entry (results can change between visits).</item>
/// </list>
/// </remarks>
public sealed class PageMemory
{
    private readonly Dictionary<int, HashSet<string>> keysByPage = new();

    /// <summary>Call when a result set for <paramref name="page"/> (one-based) starts arriving.</summary>
    public void StartPage(int page)
    {
        if (page <= 1)
            this.keysByPage.Clear();

        this.keysByPage[page] = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether <paramref name="description"/>'s match key appeared on a page before <paramref name="page"/>.
    /// Always <c>false</c> on page 1 and for empty or whitespace-only descriptions.
    /// </summary>
    public bool SeenOnEarlierPage(int page, string? description)
    {
        if (page <= 1 || string.IsNullOrWhiteSpace(description))
            return false;

        var key = DescriptionMatch.Key(description);
        return this.keysByPage.Any(entry => entry.Key < page && entry.Value.Contains(key));
    }

    /// <summary>Records that <paramref name="description"/> appeared on <paramref name="page"/>.</summary>
    public void Record(int page, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;

        if (!this.keysByPage.TryGetValue(page, out var keys))
            this.keysByPage[page] = keys = new HashSet<string>(StringComparer.Ordinal);

        keys.Add(DescriptionMatch.Key(description));
    }
}

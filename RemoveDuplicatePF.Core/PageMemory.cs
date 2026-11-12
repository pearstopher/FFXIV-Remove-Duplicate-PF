namespace RemoveDuplicatePF.Core;

/// <summary>
/// Remembers which listings (by match key, <see cref="DescriptionMatch.Key"/>) appeared on each
/// page of the current search, so later pages can hide what earlier pages already showed (#75).
/// </summary>
/// <remarks>
/// Owner's rules:
/// <list type="bullet">
/// <item>Earlier pages win: on page P, a key seen on any page before P is hidden.</item>
/// <item>A new search (window opened, Refresh, category change) starts on page 1 and clears
/// everything. Going back to page 1 with Previous keeps the other pages (#80).</item>
/// <item>Each visit to a page replaces that page's entry (results can change between visits).</item>
/// <item>The same listing (same ID) shown on an earlier page and sent again on a later one is shown
/// again, as the game does without the plugin. Pages are positions in a list that keeps changing,
/// and the game's Previous can land early, so neighbouring pages often overlap (#84).</item>
/// </list>
/// </remarks>
public sealed class PageMemory
{
    private readonly Dictionary<int, HashSet<string>> keysByPage = new();

    // page -> IDs of the listings left visible on it (#84)
    private readonly Dictionary<int, HashSet<ulong>> shownIdsByPage = new();

    // match key -> IDs of listings hidden because an earlier page had that key, this search (#81)
    private readonly Dictionary<string, HashSet<ulong>> carriedIdsByKey = new(StringComparer.Ordinal);

    /// <summary>Call when a result set for <paramref name="page"/> (one-based) starts arriving.</summary>
    /// <param name="page">The one-based page.</param>
    /// <param name="newSearch">Whether a page-1 result set starts a new search (<see cref="SearchStart"/>).</param>
    public void StartPage(int page, bool newSearch = true)
    {
        if (page <= 1 && newSearch)
        {
            this.keysByPage.Clear();
            this.shownIdsByPage.Clear();
            this.carriedIdsByKey.Clear();
        }

        this.keysByPage[page] = new HashSet<string>(StringComparer.Ordinal);
        this.shownIdsByPage[page] = new HashSet<ulong>();
    }

    /// <summary>
    /// Whether this exact listing was left visible on a page before <paramref name="page"/>: the
    /// pages overlap, and it should be shown again rather than hidden (#84).
    /// </summary>
    public bool ShownOnEarlierPage(int page, ulong listingId) =>
        this.shownIdsByPage.Any(entry => entry.Key < page && entry.Value.Contains(listingId));

    /// <summary>Records that listing <paramref name="listingId"/> was left visible on <paramref name="page"/>.</summary>
    public void RecordShown(int page, ulong listingId)
    {
        if (!this.shownIdsByPage.TryGetValue(page, out var ids))
            this.shownIdsByPage[page] = ids = new HashSet<ulong>();

        ids.Add(listingId);
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

    /// <summary>
    /// Records a listing hidden because an earlier page had its key, so the earlier page's
    /// <c>[N]</c> can include it (#81). Each listing ID counts once per search, however often
    /// its page is revisited.
    /// </summary>
    public void RecordCarried(ulong listingId, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;

        var key = DescriptionMatch.Key(description);
        if (!this.carriedIdsByKey.TryGetValue(key, out var ids))
            this.carriedIdsByKey[key] = ids = new HashSet<ulong>();

        ids.Add(listingId);
    }

    /// <summary>IDs of listings with match key <paramref name="key"/> hidden on later pages this search (#81).</summary>
    public IReadOnlySet<ulong> CarriedIds(string key) =>
        this.carriedIdsByKey.TryGetValue(key, out var ids) ? ids : EmptyIds;

    private static readonly HashSet<ulong> EmptyIds = new();
}

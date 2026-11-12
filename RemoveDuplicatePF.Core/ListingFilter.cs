namespace RemoveDuplicatePF.Core;

/// <summary>
/// Decides the visibility of each Party Finder listing as it arrives.
/// The plugin calls <see cref="Decide"/> from Dalamud's <c>ReceiveListing</c>
/// event and writes the result back to <c>args.Visible</c>.
/// </summary>
/// <remarks>
/// Duplicates are tracked by listing ID across the whole search (#88): the first copy of an ad seen
/// in the search is shown, wherever the game puts it, and every other copy is hidden on every page.
/// Its <c>[N]</c> counts every copy found so far, including copies only found on later pages. A new
/// search (window opened, Refresh, category change, #80) starts over. Pages aren't tracked at all.
/// <para>
/// Each result set (one page as the game sends it) keeps what's needed to describe it: the listings
/// it showed and how many it hid. The last <see cref="HistorySize"/> are kept, because the game can
/// fetch a page in the background while a different page stays on screen (#51). The title and
/// <c>[N]</c> prefixes must use <see cref="ForDisplayed(IEnumerable{ulong})"/>, which picks the
/// result set matching the listings actually shown.
/// </para>
/// </remarks>
public sealed class ListingFilter
{
    /// <summary>How many recent result sets are kept.</summary>
    public const int HistorySize = 4;

    private readonly ResultSetBoundary boundary = new();

    // Every listing of the current search, grouped by match key: the first copy is kept (#88).
    private readonly DuplicateTracker search = new();

    // Whether a result set starts a new search or continues the current one (#80).
    private readonly SearchStart searchStart = new();

    // Newest first. Never empty.
    private readonly LinkedList<ResultSet> history = new();

    // The placeholder set created before any listings arrive is number 0, so the
    // first real result set is 1.
    private int nextSetNumber;

    public ListingFilter()
    {
        this.StartNewSet(newSearch: true);
    }

    /// <summary>
    /// The current search's duplicate groups: every listing seen since the search started, grouped
    /// by match key, with the first copy kept (#88).
    /// </summary>
    public DuplicateTracker Search => this.search;

    /// <summary>
    /// Call each frame the game's displayed list has listings on screen (see
    /// <see cref="ResultSetBoundary.NoteGameListShown"/>). Lets two small result sets in a row be told apart.
    /// </summary>
    public void NoteGameListShown() => this.boundary.NoteGameListShown();

    /// <summary>Call when the Party Finder window opens (#80).</summary>
    public void NoteWindowOpened() => this.searchStart.NoteWindowOpened();

    /// <summary>Call for each button click in the Party Finder window, with its event number (#80).</summary>
    public void NoteButtonClick(int eventParam) => this.searchStart.NoteButtonClick(eventParam);

    /// <summary>
    /// Returns the visibility to write back for a listing.
    /// </summary>
    /// <param name="batchNumber">Dalamud's <c>args.BatchNumber</c> (the segment number).</param>
    /// <param name="listingId">Server listing ID.</param>
    /// <param name="description">Listing description text as shown to players.</param>
    /// <param name="currentlyVisible">
    /// The current <c>args.Visible</c> value. It is shared with other plugins,
    /// so a listing another plugin already hid stays hidden and is not counted.
    /// </param>
    /// <param name="gameListEmpty">
    /// Whether the game's displayed list was empty when the listing arrived (#72);
    /// see <see cref="ResultSetBoundary.IsNewResultSet"/>. Leave <c>false</c> when unknown.
    /// </param>
    public bool Decide(
        int batchNumber,
        ulong listingId,
        string? description,
        bool currentlyVisible,
        bool gameListEmpty = false)
    {
        // Track result-set boundaries for every listing, including ones another plugin hid.
        if (this.boundary.IsNewResultSet(batchNumber, listingId, gameListEmpty))
        {
            var newSearch = this.searchStart.TakeIsNewSearch();
            if (newSearch)
                this.search.Reset();

            this.StartNewSet(newSearch);
        }

        var set = this.history.First!.Value;
        set.Received++;

        // Never un-hide another plugin's decision, and don't let a listing the
        // player won't see become the kept copy of a group.
        if (!currentlyVisible)
        {
            set.HiddenByOthers++;
            return false;
        }

        // The same listing seen again (pages overlap, a page revisited) gets the same decision.
        var visible = this.search.Observe(listingId, description);
        if (visible)
            set.Shown[listingId] = description;
        else
            set.Hidden++;

        return visible;
    }

    /// <summary>
    /// <see cref="ForDisplayed(IEnumerable{ulong})"/> for the game's raw 50 displayed-listing slots.
    /// Only the first <paramref name="displayedCount"/> slots are the page on screen: the game doesn't
    /// clear the rest, so on a short page they still hold the previous page's IDs (#74).
    /// </summary>
    public DisplayedResultSet ForDisplayed(IEnumerable<ulong> displayedListingSlots, int displayedCount) =>
        this.ForDisplayed(displayedListingSlots.Take(Math.Max(0, displayedCount)));

    /// <summary>
    /// The result set whose shown listings best match <paramref name="displayedListingIds"/>
    /// (the listings the Party Finder window is showing). Ties go to the newest set; with
    /// no overlap at all (or no displayed IDs), the newest set is used.
    /// </summary>
    public DisplayedResultSet ForDisplayed(IEnumerable<ulong> displayedListingIds)
    {
        var displayed = displayedListingIds.Where(id => id != 0).ToHashSet();

        var best = this.history.First!.Value;
        var bestOverlap = displayed.Count == 0 ? 0 : best.Shown.Keys.Count(displayed.Contains);

        foreach (var set in this.history.Skip(1))
        {
            var overlap = set.Shown.Keys.Count(displayed.Contains);
            if (overlap > bestOverlap)
            {
                best = set;
                bestOverlap = overlap;
            }
        }

        return new DisplayedResultSet(
            best.Number, best == this.history.First!.Value, bestOverlap, best.Hidden, this.Prefixes(best));
    }

    /// <summary>Sequence number of the newest result set (cheap; for use per listing).</summary>
    public int NewestResultSetNumber => this.history.First!.Value.Number;

    /// <summary>A summary of the newest result set, for the Debug log (#11).</summary>
    public ResultSetSummary NewestSummary
    {
        get
        {
            var set = this.history.First!.Value;
            return new ResultSetSummary(
                set.Number, set.Received, set.HiddenByOthers, set.Hidden, this.Prefixes(set), set.NewSearch);
        }
    }

    // The [N] for each listing the result set showed that has copies: every copy found so far this
    // search, counted by listing ID (#81, #88). Keyed by the listing's full description.
    private Dictionary<string, int> Prefixes(ResultSet set)
    {
        var prefixes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, description) in set.Shown)
        {
            var size = this.search.GroupSize(id);
            if (size > 1 && description is not null)
                prefixes[description] = size;
        }

        return prefixes;
    }

    private void StartNewSet(bool newSearch)
    {
        this.history.AddFirst(new ResultSet(this.nextSetNumber++, newSearch));
        while (this.history.Count > HistorySize)
            this.history.RemoveLast();
    }

    private sealed class ResultSet(int number, bool newSearch)
    {
        public int Number { get; } = number;

        // Whether it started a new search (#80).
        public bool NewSearch { get; } = newSearch;

        // Listings this result set left visible, with their descriptions.
        public Dictionary<ulong, string?> Shown { get; } = new();

        // Listings it hid as copies of a listing shown earlier in the search (on this page or another).
        public int Hidden { get; set; }

        // All listings received in this result set, and how many another plugin had already hidden.
        public int Received { get; set; }

        public int HiddenByOthers { get; set; }
    }
}

/// <summary>The result set chosen to describe what's on screen.</summary>
/// <param name="ResultSetNumber">Sequence number of the result set since load (1 = first page received), for the debug log.</param>
/// <param name="IsNewest">Whether it's the most recently received result set.</param>
/// <param name="MatchedListings">How many displayed listings it matched.</param>
/// <param name="HiddenTotal">
/// Listings hidden on this page as copies of a listing shown earlier in the search. The number in the
/// title. The shown copy of each ad isn't counted.
/// </param>
/// <param name="Prefixes">
/// The <c>[N]</c> for each shown listing that has copies, by full description: every copy found so
/// far this search (#88).
/// </param>
public sealed record DisplayedResultSet(
    int ResultSetNumber,
    bool IsNewest,
    int MatchedListings,
    int HiddenTotal,
    IReadOnlyDictionary<string, int> Prefixes);

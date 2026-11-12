namespace RemoveDuplicatePF.Core;

/// <summary>
/// Decides the visibility of each Party Finder listing as it arrives.
/// The plugin calls <see cref="Decide"/> from Dalamud's <c>ReceiveListing</c>
/// event and writes the result back to <c>args.Visible</c>.
/// </summary>
/// <remarks>
/// Each result set (search, refresh, category or page) gets its own
/// <see cref="DuplicateTracker"/> for duplicates within the page. Listings already seen on an
/// earlier page of the search are hidden entirely (#75, <see cref="PageMemory"/>). The last
/// <see cref="HistorySize"/> result sets are kept, because the game can fetch a
/// page in the background while a different page stays on screen (#51). The
/// title and <c>[N]</c> prefixes must use <see cref="ForDisplayed"/>, which
/// picks the result set matching the listings actually shown.
/// </remarks>
public sealed class ListingFilter
{
    /// <summary>How many recent result sets are kept.</summary>
    public const int HistorySize = 4;

    private readonly ResultSetBoundary boundary = new();

    // Newest first. Never empty.
    private readonly LinkedList<ResultSet> history = new();
    // The placeholder set created before any listings arrive is number 0, so the
    // first real result set is 1.
    private int nextSetNumber;

    public ListingFilter()
    {
        this.StartNewSet();
    }

    /// <summary>The grouping state for the newest result set.</summary>
    public DuplicateTracker Tracker => this.history.First!.Value.Tracker;

    /// <summary>Assigns each result set its page and checks it against the window (#74).</summary>
    public PageTracker Pages { get; } = new();

    // What earlier pages of the current search showed (#75).
    private readonly PageMemory memory = new();

    /// <summary>
    /// Call each frame the game's displayed list has listings on screen (see
    /// <see cref="ResultSetBoundary.NoteGameListShown"/>). Lets two small result sets in a row be told apart.
    /// </summary>
    public void NoteGameListShown() => this.boundary.NoteGameListShown();

    // Whether a page-1 result set is a new search or a return to page 1 (#80).
    private readonly SearchStart searchStart = new();

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
    /// <param name="gamePageIndex">
    /// The game's zero-based page index when the listing arrived (#74), or <c>null</c> if unknown.
    /// Only the value at the first listing of a result set is used.
    /// </param>
    public bool Decide(
        int batchNumber,
        ulong listingId,
        string? description,
        bool currentlyVisible,
        bool gameListEmpty = false,
        int? gamePageIndex = null)
    {
        // Track result-set boundaries for every listing, including ones another plugin hid.
        if (this.boundary.IsNewResultSet(batchNumber, listingId, gameListEmpty))
        {
            var page = this.Pages.PageFor(gamePageIndex);
            var newSearch = this.searchStart.TakeIsNewSearch() && page <= 1;
            this.StartNewSet(page, newSearch);
            this.memory.StartPage(page, newSearch); // a new search clears what earlier searches showed
        }

        var set = this.history.First!.Value;
        set.Received++;

        // Never un-hide another plugin's decision, and don't let a listing the
        // player won't see become the kept copy of a group or count as seen on this page.
        if (!currentlyVisible)
        {
            set.HiddenByOthers++;
            return false;
        }

        // Earlier pages win: a listing whose text was already on an earlier page is hidden entirely,
        // including the copy that would otherwise be kept here with an [N] (#75). Except the very same
        // listing shown there: neighbouring pages overlap when results change or the game's Previous
        // lands early, and the game would show it again, so it's shown and not counted (#84).
        var repeat = this.memory.ShownOnEarlierPage(set.Page, listingId);
        if (!repeat && this.memory.SeenOnEarlierPage(set.Page, description))
        {
            set.HiddenFromEarlierPages++;
            return false;
        }

        this.memory.Record(set.Page, description);
        var visible = set.Tracker.Observe(listingId, description);
        if (visible)
        {
            set.ShownIds.Add(listingId);
            this.memory.RecordShown(set.Page, listingId);
        }

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
        var bestOverlap = displayed.Count == 0 ? 0 : best.ShownIds.Count(displayed.Contains);

        foreach (var set in this.history.Skip(1))
        {
            var overlap = set.ShownIds.Count(displayed.Contains);
            if (overlap > bestOverlap)
            {
                best = set;
                bestOverlap = overlap;
            }
        }

        return new DisplayedResultSet(
            best.Tracker, best.Number, best == this.history.First!.Value, bestOverlap, best.Page, best.HiddenFromEarlierPages);
    }

    /// <summary>A summary of the newest result set, for the Debug log (#11).</summary>
    public ResultSetSummary NewestSummary
    {
        get
        {
            var set = this.history.First!.Value;
            return new ResultSetSummary(
                set.Number, set.Received, set.HiddenByOthers, set.Tracker, set.Page, set.HiddenFromEarlierPages, set.NewSearch);
        }
    }

    private void StartNewSet(int page = 1, bool newSearch = true)
    {
        this.history.AddFirst(new ResultSet(this.nextSetNumber++, page, newSearch));
        while (this.history.Count > HistorySize)
            this.history.RemoveLast();
    }

    private sealed class ResultSet(int number, int page, bool newSearch)
    {
        public int Number { get; } = number;

        // One-based page this result set belongs to (#74).
        public int Page { get; } = page;

        // Whether it started a new search (only ever true on page 1) (#80).
        public bool NewSearch { get; } = newSearch;

        public DuplicateTracker Tracker { get; } = new();

        // Listings this result set left visible (kept copies and listings without duplicates).
        public HashSet<ulong> ShownIds { get; } = new();

        // All listings received in this result set, and how many another plugin had already hidden.
        public int Received { get; set; }

        public int HiddenByOthers { get; set; }

        // Listings hidden because their match key was on an earlier page (#75).
        public int HiddenFromEarlierPages { get; set; }
    }
}

/// <summary>The result set chosen to describe what's on screen.</summary>
/// <param name="Tracker">Its duplicate groups and counts.</param>
/// <param name="ResultSetNumber">Sequence number of the result set since load (1 = first page received), for the debug log.</param>
/// <param name="IsNewest">Whether it's the most recently received result set.</param>
/// <param name="MatchedListings">How many displayed listings it matched.</param>
/// <param name="Page">The one-based page assigned to the result set (#74).</param>
/// <param name="HiddenFromEarlierPages">Listings hidden because they appeared on an earlier page (#75).</param>
public sealed record DisplayedResultSet(
    DuplicateTracker Tracker,
    int ResultSetNumber,
    bool IsNewest,
    int MatchedListings,
    int Page = 1,
    int HiddenFromEarlierPages = 0)
{
    /// <summary>
    /// Listings hidden on this page: extra copies within the page plus listings already seen on an
    /// earlier page. The number in the title (#75). The visible copy of each group isn't counted.
    /// </summary>
    public int HiddenTotal => this.Tracker.HiddenCount + this.HiddenFromEarlierPages;
}

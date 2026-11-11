namespace RemoveDuplicatePF.Core;

/// <summary>
/// Decides the visibility of each Party Finder listing as it arrives.
/// The plugin calls <see cref="Decide"/> from Dalamud's <c>ReceiveListing</c>
/// event and writes the result back to <c>args.Visible</c>.
/// </summary>
/// <remarks>
/// Each result set (search, refresh, category or page) gets its own
/// <see cref="DuplicateTracker"/>, so counts are always per page. The last
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
    public bool Decide(int batchNumber, ulong listingId, string? description, bool currentlyVisible)
    {
        // Track result-set boundaries for every listing, including ones another plugin hid.
        if (this.boundary.IsNewResultSet(batchNumber, listingId))
            this.StartNewSet();

        var set = this.history.First!.Value;
        set.Received++;

        // Never un-hide another plugin's decision, and don't let a listing the
        // player won't see become the kept copy of a group.
        if (!currentlyVisible)
        {
            set.HiddenByOthers++;
            return false;
        }
        var visible = set.Tracker.Observe(listingId, description);
        if (visible)
            set.ShownIds.Add(listingId);

        return visible;
    }

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

        return new DisplayedResultSet(best.Tracker, best.Number, best == this.history.First!.Value, bestOverlap);
    }

    /// <summary>A summary of the newest result set, for the Debug log (#11).</summary>
    public ResultSetSummary NewestSummary
    {
        get
        {
            var set = this.history.First!.Value;
            return new ResultSetSummary(set.Number, set.Received, set.HiddenByOthers, set.Tracker);
        }
    }

    private void StartNewSet()
    {
        this.history.AddFirst(new ResultSet(this.nextSetNumber++));
        while (this.history.Count > HistorySize)
            this.history.RemoveLast();
    }

    private sealed class ResultSet(int number)
    {
        public int Number { get; } = number;

        public DuplicateTracker Tracker { get; } = new();

        // Listings this result set left visible (kept copies and listings without duplicates).
        public HashSet<ulong> ShownIds { get; } = new();

        // All listings received in this result set, and how many another plugin had already hidden.
        public int Received { get; set; }

        public int HiddenByOthers { get; set; }
    }
}

/// <summary>The result set chosen to describe what's on screen.</summary>
/// <param name="Tracker">Its duplicate groups and counts.</param>
/// <param name="ResultSetNumber">Sequence number of the result set since load (1 = first page received), for the debug log.</param>
/// <param name="IsNewest">Whether it's the most recently received result set.</param>
/// <param name="MatchedListings">How many displayed listings it matched.</param>
public sealed record DisplayedResultSet(DuplicateTracker Tracker, int ResultSetNumber, bool IsNewest, int MatchedListings);

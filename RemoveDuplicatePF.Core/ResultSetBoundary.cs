namespace RemoveDuplicatePF.Core;

/// <summary>
/// Detects when a new Party Finder result set starts (new search, refresh,
/// category change or page), so duplicate tracking can be reset.
/// </summary>
/// <remarks>
/// The server sends results as segments of up to <see cref="MaxListingsPerSegment"/>
/// listings. Segment numbers (Dalamud's <c>BatchNumber</c>) start at 1, count up,
/// and the last segment is 0 (see docs/party-finder-notes.md). A new set starts when:
/// <list type="bullet">
/// <item>it's the first listing ever seen;</item>
/// <item>the batch number changes to 1, or changes away from 0 (the previous set ended);</item>
/// <item>a listing ID repeats within the current set (the same listings were sent again);</item>
/// <item>more listings arrive with the same batch number than one segment can hold
/// (two back-to-back segments with the same number, e.g. two single-segment sets);</item>
/// <item>the game's displayed list has just become empty (#72). The game empties its list
/// whenever it requests new results, so the first listing to see an empty list, after the
/// list was seen with listings, starts a new set. This catches a small result set sent
/// as a lone segment 0 straight after a set that also ended with segment 0, which none of
/// the rules above can tell apart from the end of the previous set.</item>
/// </list>
/// Listings in the same segment all see the same list state: Dalamud raises every listing
/// of a segment before the game processes it. So only the first of them is a transition.
/// "Seen with listings" comes from either the previous listing seeing a non-empty list, or
/// <see cref="NoteGameListShown"/> (called each frame the window shows listings). The latter is
/// needed for two small sets in a row: every listing of both sees an empty list, but the window
/// showed the first set in between (#75).
/// </remarks>
public sealed class ResultSetBoundary
{
    /// <summary>Listing slots per segment packet.</summary>
    public const int MaxListingsPerSegment = 4;

    private readonly HashSet<ulong> idsInCurrentSet = new();
    private int? lastBatch;
    private int listingsInCurrentSegment;
    private bool lastSawEmptyList;
    private bool listShownSinceLastListing;

    /// <summary>
    /// Records that the game's displayed list had listings on screen (for example, checked each
    /// frame while the Party Finder window is open). The next listing that finds the list empty
    /// then starts a new set.
    /// </summary>
    public void NoteGameListShown() => this.listShownSinceLastListing = true;

    /// <summary>
    /// Records a listing and returns <c>true</c> if it is the first listing of a new result set.
    /// </summary>
    /// <param name="batchNumber">Dalamud's <c>args.BatchNumber</c> (the segment number).</param>
    /// <param name="listingId">Server listing ID.</param>
    /// <param name="gameListEmpty">
    /// Whether the game's displayed listing list was empty when this listing arrived
    /// (<c>AgentLookingForGroup.NumberOfListingsDisplayed == 0</c>). Leave <c>false</c> when unknown.
    /// </param>
    public bool IsNewResultSet(int batchNumber, ulong listingId, bool gameListEmpty = false)
    {
        var listJustEmptied = gameListEmpty && (!this.lastSawEmptyList || this.listShownSinceLastListing);
        this.lastSawEmptyList = gameListEmpty;
        this.listShownSinceLastListing = false;

        var newSet = this.lastBatch is not { } last
                     || (batchNumber != last && (batchNumber == 1 || last == 0))
                     || this.idsInCurrentSet.Contains(listingId)
                     || (batchNumber == last && this.listingsInCurrentSegment >= MaxListingsPerSegment)
                     || listJustEmptied;

        if (newSet)
        {
            this.idsInCurrentSet.Clear();
            this.listingsInCurrentSegment = 0;
        }
        else if (batchNumber != this.lastBatch)
        {
            this.listingsInCurrentSegment = 0;
        }

        this.idsInCurrentSet.Add(listingId);
        this.listingsInCurrentSegment++;
        this.lastBatch = batchNumber;
        return newSet;
    }
}

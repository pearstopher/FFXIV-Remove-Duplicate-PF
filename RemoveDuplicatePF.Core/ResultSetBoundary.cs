namespace RemoveDuplicatePF.Core;

/// <summary>
/// Detects when a new Party Finder result set (one page of results: a search, refresh,
/// category change or page) starts.
/// </summary>
/// <remarks>
/// Decided only from what the plugin itself receives, never from the window, because the window
/// shows what's left after the plugin's own hiding (#94). The server sends results as packets of up
/// to <see cref="MaxListingsPerSegment"/> listings. Packet numbers (Dalamud's <c>BatchNumber</c>)
/// start at 1, count up, and the last packet is 0 (see docs/party-finder-notes.md). Dalamud raises
/// all of a packet's listings together, before the game processes it. A new set starts when:
/// <list type="bullet">
/// <item>it's the first listing ever seen;</item>
/// <item><b>the previous set is complete:</b> its last packet (0) has arrived and a frame has passed
/// since (<see cref="NoteFrame"/>). A new request takes at least a tenth of a second, so its results
/// never arrive in the same frame. This is what separates a set that fits in one packet (4 listings
/// or fewer, a lone packet 0) from the set before it, which also ended with packet 0;</item>
/// <item>the packet number changes to 1, or changes away from 0;</item>
/// <item>a listing ID repeats within the current set (the same listings were sent again);</item>
/// <item>more listings arrive with the same packet number than one packet can hold.</item>
/// </list>
/// The last two are fallbacks from before the frame rule; they need no window state either.
/// </remarks>
public sealed class ResultSetBoundary
{
    /// <summary>Listing slots per segment packet.</summary>
    public const int MaxListingsPerSegment = 4;

    private readonly HashSet<ulong> idsInCurrentSet = new();
    private int? lastBatch;
    private int listingsInCurrentSegment;
    private bool previousSetComplete;

    /// <summary>
    /// Call once per frame. After the last packet of a set (number 0), the first frame marks the set
    /// complete, so the next listing starts a new set.
    /// </summary>
    public void NoteFrame()
    {
        if (this.lastBatch == 0)
            this.previousSetComplete = true;
    }

    /// <summary>
    /// Records a listing and returns <c>true</c> if it is the first listing of a new result set.
    /// </summary>
    /// <param name="batchNumber">Dalamud's <c>args.BatchNumber</c> (the packet number).</param>
    /// <param name="listingId">Server listing ID.</param>
    public bool IsNewResultSet(int batchNumber, ulong listingId)
    {
        var newSet = this.lastBatch is not { } last
                     || this.previousSetComplete
                     || (batchNumber != last && (batchNumber == 1 || last == 0))
                     || this.idsInCurrentSet.Contains(listingId)
                     || (batchNumber == last && this.listingsInCurrentSegment >= MaxListingsPerSegment);

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
        this.previousSetComplete = false;
        return newSet;
    }
}

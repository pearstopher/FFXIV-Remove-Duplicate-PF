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
/// (two back-to-back segments with the same number, e.g. two single-segment sets).</item>
/// </list>
/// </remarks>
public sealed class ResultSetBoundary
{
    /// <summary>Listing slots per segment packet.</summary>
    public const int MaxListingsPerSegment = 4;

    private readonly HashSet<ulong> idsInCurrentSet = new();
    private int? lastBatch;
    private int listingsInCurrentSegment;

    /// <summary>
    /// Records a listing and returns <c>true</c> if it is the first listing of a new result set.
    /// </summary>
    public bool IsNewResultSet(int batchNumber, ulong listingId)
    {
        var newSet = this.lastBatch is not { } last
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
        return newSet;
    }
}

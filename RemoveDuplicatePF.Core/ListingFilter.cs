namespace RemoveDuplicatePF.Core;

/// <summary>
/// Decides the visibility of each Party Finder listing as it arrives.
/// The plugin calls <see cref="Decide"/> from Dalamud's <c>ReceiveListing</c>
/// event and writes the result back to <c>args.Visible</c>.
/// </summary>
public sealed class ListingFilter
{
    /// <summary>The grouping state for the current result set.</summary>
    public DuplicateTracker Tracker { get; } = new();

    /// <summary>
    /// Returns the visibility to write back for a listing.
    /// </summary>
    /// <param name="listingId">Server listing ID.</param>
    /// <param name="description">Listing description text as shown to players.</param>
    /// <param name="currentlyVisible">
    /// The current <c>args.Visible</c> value. It is shared with other plugins,
    /// so a listing another plugin already hid stays hidden and is not counted.
    /// </param>
    public bool Decide(ulong listingId, string? description, bool currentlyVisible)
    {
        // Never un-hide another plugin's decision, and don't let a listing the
        // player won't see become the kept copy of a group.
        if (!currentlyVisible)
            return false;

        return this.Tracker.Observe(listingId, description);
    }
}

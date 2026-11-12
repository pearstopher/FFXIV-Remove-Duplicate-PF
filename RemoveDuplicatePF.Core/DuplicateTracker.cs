namespace RemoveDuplicatePF.Core;

/// <summary>
/// Groups Party Finder listings whose descriptions match (see <see cref="DescriptionMatch"/>)
/// and decides which listing in each group stays visible.
/// </summary>
/// <remarks>
/// Listings must be passed to <see cref="Observe"/> in the order the game
/// displays them. The first listing in each group is kept; later ones are
/// hidden. Display order is trusted as-is: no age signal is used.
/// Listings with an empty or whitespace-only description are never combined.
/// <see cref="ListingFilter"/> keeps one for the whole search (#88) and calls <see cref="Reset"/>
/// when a new search starts, so the first copy seen on any page is kept and a listing sent again
/// on another page gets the same decision.
/// </remarks>
public sealed class DuplicateTracker
{
    // match key -> ID of the kept (first) listing in that group
    private readonly Dictionary<string, ulong> keptIdByKey = new(StringComparer.Ordinal);

    // kept listing ID -> number of listings in its group (including itself)
    private readonly Dictionary<ulong, int> groupSizeByKeptId = new();

    // every listing ID seen since the last reset -> whether it was shown
    private readonly Dictionary<ulong, bool> visibilityById = new();

    /// <summary>
    /// Records a listing and returns whether it should be shown.
    /// Observing the same listing ID again returns the original decision
    /// without changing any counts.
    /// </summary>
    public bool Observe(ulong listingId, string? description)
    {
        if (this.visibilityById.TryGetValue(listingId, out var previous))
            return previous;

        bool visible;
        if (string.IsNullOrWhiteSpace(description))
        {
            visible = true;
        }
        else
        {
            var key = DescriptionMatch.Key(description);
            if (this.keptIdByKey.TryGetValue(key, out var keptId))
            {
                this.groupSizeByKeptId[keptId]++;
                visible = false;
            }
            else
            {
                this.keptIdByKey[key] = listingId;
                this.groupSizeByKeptId[listingId] = 1;
                visible = true;
            }
        }

        this.visibilityById[listingId] = visible;
        return visible;
    }

    /// <summary>
    /// Number of listings in the group kept as <paramref name="listingId"/>,
    /// including itself. Returns 1 for a kept listing with no duplicates and
    /// 0 for listings that are hidden, have empty text, or were never seen.
    /// </summary>
    public int GroupSize(ulong listingId) =>
        this.groupSizeByKeptId.TryGetValue(listingId, out var size) ? size : 0;

    /// <summary>Clears all state, ready for a new search.</summary>
    public void Reset()
    {
        this.keptIdByKey.Clear();
        this.groupSizeByKeptId.Clear();
        this.visibilityById.Clear();
    }
}

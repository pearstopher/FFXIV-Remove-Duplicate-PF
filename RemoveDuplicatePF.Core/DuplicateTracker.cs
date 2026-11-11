namespace RemoveDuplicatePF.Core;

/// <summary>
/// Groups Party Finder listings whose description text matches exactly and
/// decides which listing in each group stays visible.
/// </summary>
/// <remarks>
/// Listings must be passed to <see cref="Observe"/> in the order the game
/// displays them. The first listing in each group is kept; later ones are
/// hidden. Display order is trusted as-is: no age signal is used.
/// Listings with an empty or whitespace-only description are never combined.
/// Call <see cref="Reset"/> when a new result set starts.
/// </remarks>
public sealed class DuplicateTracker
{
    // description -> listing ID of the kept (first) listing in that group
    private readonly Dictionary<string, ulong> keptByDescription = new(StringComparer.Ordinal);

    // kept listing ID -> number of listings in its group (including itself)
    private readonly Dictionary<ulong, int> groupSizeByKeptId = new();

    // every listing ID seen since the last reset -> whether it was shown
    private readonly Dictionary<ulong, bool> visibilityById = new();

    /// <summary>Total number of listings hidden as duplicates since the last reset.</summary>
    public int HiddenCount { get; private set; }

    /// <summary>Number of groups that have at least one hidden duplicate.</summary>
    public int DuplicateGroupCount => this.groupSizeByKeptId.Count(g => g.Value > 1);

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
        else if (this.keptByDescription.TryGetValue(description, out var keptId))
        {
            this.groupSizeByKeptId[keptId]++;
            this.HiddenCount++;
            visible = false;
        }
        else
        {
            this.keptByDescription[description] = listingId;
            this.groupSizeByKeptId[listingId] = 1;
            visible = true;
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

    /// <summary>Kept listings that have duplicates, with their group sizes.</summary>
    public IReadOnlyDictionary<ulong, int> DuplicateGroups =>
        this.groupSizeByKeptId.Where(g => g.Value > 1).ToDictionary(g => g.Key, g => g.Value);

    /// <summary>Clears all state, ready for a new result set.</summary>
    public void Reset()
    {
        this.keptByDescription.Clear();
        this.groupSizeByKeptId.Clear();
        this.visibilityById.Clear();
        this.HiddenCount = 0;
    }
}

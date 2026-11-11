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
/// Call <see cref="Reset"/> when a new result set starts.
/// </remarks>
public sealed class DuplicateTracker
{
    // match key -> the kept (first) listing in that group
    private readonly Dictionary<string, (ulong Id, string Description)> keptByKey = new(StringComparer.Ordinal);

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
        else
        {
            var key = DescriptionMatch.Key(description);
            if (this.keptByKey.TryGetValue(key, out var kept))
            {
                this.groupSizeByKeptId[kept.Id]++;
                this.HiddenCount++;
                visible = false;
            }
            else
            {
                this.keptByKey[key] = (listingId, description);
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

    /// <summary>Kept listings that have duplicates, with their group sizes.</summary>
    public IReadOnlyDictionary<ulong, int> DuplicateGroups =>
        this.groupSizeByKeptId.Where(g => g.Value > 1).ToDictionary(g => g.Key, g => g.Value);

    /// <summary>
    /// Full descriptions of the kept listings of groups that have duplicates,
    /// with their group sizes. This is the text shown on screen for each group (#9).
    /// </summary>
    public IReadOnlyDictionary<string, int> DuplicateGroupsByDescription =>
        this.keptByKey.Values
            .Select(k => (k.Description, Size: this.groupSizeByKeptId[k.Id]))
            .Where(g => g.Size > 1)
            .ToDictionary(g => g.Description, g => g.Size, StringComparer.Ordinal);

    /// <summary>Clears all state, ready for a new result set.</summary>
    public void Reset()
    {
        this.keptByKey.Clear();
        this.groupSizeByKeptId.Clear();
        this.visibilityById.Clear();
        this.HiddenCount = 0;
    }
}

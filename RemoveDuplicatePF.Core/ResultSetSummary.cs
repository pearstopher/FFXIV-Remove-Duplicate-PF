using System.Globalization;

namespace RemoveDuplicatePF.Core;

/// <summary>
/// One Debug log line per result set (#11), replacing the old line per listing.
/// </summary>
/// <remarks>
/// Only duplicate groups are named, by their match key (<see cref="DescriptionMatch.Key"/>,
/// at most 30 characters), so the log no longer records the text of every listing, but
/// listings that were combined can still be checked against the 30-character rule (#42).
/// </remarks>
/// <param name="ResultSetNumber">Sequence number of the result set since load.</param>
/// <param name="Received">Listings received in the result set.</param>
/// <param name="HiddenByOthers">Listings another plugin had already hidden (not counted as duplicates).</param>
/// <param name="Tracker">The result set's duplicate groups.</param>
/// <param name="Page">The one-based page assigned to the result set (#74).</param>
public sealed record ResultSetSummary(int ResultSetNumber, int Received, int HiddenByOthers, DuplicateTracker Tracker, int Page = 1)
{
    /// <summary>
    /// For example: <c>result set #7 (page 2): 50 listings, 5 hidden as duplicates in 4 groups, 1 hidden by
    /// other plugins | "BUNN$BEAT club night" ×3, "Dancers wanted" ×2, ...</c>
    /// Groups are listed largest first, then by key.
    /// </summary>
    public string Describe()
    {
        var groups = this.Tracker.DuplicateGroupsByDescription
            .Select(g => (Key: DescriptionMatch.Key(g.Key), Size: g.Value))
            .OrderByDescending(g => g.Size)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => string.Create(CultureInfo.InvariantCulture, $"\"{g.Key}\" ×{g.Size}"));

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"result set #{this.ResultSetNumber} (page {this.Page}): {this.Received} listings, {this.Tracker.HiddenCount} hidden as duplicates in {this.Tracker.DuplicateGroupCount} groups, {this.HiddenByOthers} hidden by other plugins");

        var list = string.Join(", ", groups);
        return list.Length == 0 ? text : $"{text} | {list}";
    }
}

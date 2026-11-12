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
/// <param name="Hidden">Listings hidden as copies of a listing shown earlier in the search (#88).</param>
/// <param name="Groups">The <c>[N]</c> for each listing it showed that has copies, by full description.</param>
/// <param name="NewSearch">Whether the result set started a new search (#80).</param>
public sealed record ResultSetSummary(
    int ResultSetNumber,
    int Received,
    int HiddenByOthers,
    int Hidden,
    IReadOnlyDictionary<string, int> Groups,
    bool NewSearch = true)
{
    /// <summary>
    /// For example: <c>result set #7 (same search): 50 listings, 5 duplicates hidden, 1 hidden by other
    /// plugins | "BUNN$BEAT club night" ×3, "Dancers wanted" ×2</c>. Groups are the shown listings
    /// with copies, largest first, then by key; each count includes copies found on other pages.
    /// </summary>
    public string Describe()
    {
        var groups = this.Groups
            .Select(g => (Key: DescriptionMatch.Key(g.Key), Size: g.Value))
            .OrderByDescending(g => g.Size)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => string.Create(CultureInfo.InvariantCulture, $"\"{g.Key}\" ×{g.Size}"));

        var search = this.NewSearch ? "new search" : "same search";
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"result set #{this.ResultSetNumber} ({search}): {this.Received} listings, {this.Hidden} duplicates hidden, {this.HiddenByOthers} hidden by other plugins");

        var list = string.Join(", ", groups);
        return list.Length == 0 ? text : $"{text} | {list}";
    }
}

using System.Globalization;
using System.Text.RegularExpressions;

namespace RemoveDuplicatePF.Core;

/// <summary>
/// Builds the Party Finder window title with the total number of duplicate listings,
/// e.g. <c>Party Finder (7 duplicates)</c>, and recovers the game's own title from it.
/// </summary>
/// <remarks>
/// The number is the total of all listings in duplicate groups, including the one kept
/// in each group, so it equals the sum of the <c>[N]</c> prefixes on the page (#64). Use
/// <see cref="DuplicateTracker.DuplicateListingCount"/> for the page on screen (#51).
/// The plugin shows the suffix in the prefix colour; this class only deals with plain text.
/// </remarks>
public static class TitleText
{
    // Current suffix " (7 duplicates)", and v1.0.0's " (4 duplicates combined)" so a title
    // left over from the old version is cleaned up rather than stacked.
    private static readonly Regex OwnSuffix = new(@" \(\d+ duplicates?( combined)?\)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The suffix for <paramref name="duplicateListings"/> listings in duplicate groups, or <c>null</c>
    /// when there are none. A duplicate group always has at least 2 listings, so 1 counts as none.
    /// </summary>
    public static string? Suffix(int duplicateListings) => duplicateListings < 2
        ? null
        : string.Create(CultureInfo.InvariantCulture, $" ({duplicateListings} duplicates)");

    /// <summary>
    /// The title to show as plain text: the game's title plus the suffix, or the game's title alone.
    /// Any suffix already on <paramref name="currentTitle"/> is replaced, never stacked.
    /// </summary>
    public static string Compose(string currentTitle, int duplicateListings) =>
        Original(currentTitle) + (Suffix(duplicateListings) ?? string.Empty);

    /// <summary>The game's own title, with the plugin's suffix removed if present.</summary>
    public static string Original(string currentTitle) =>
        OwnSuffix.Replace(currentTitle ?? string.Empty, string.Empty);
}

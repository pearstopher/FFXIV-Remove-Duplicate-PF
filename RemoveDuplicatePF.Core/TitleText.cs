using System.Globalization;
using System.Text.RegularExpressions;

namespace RemoveDuplicatePF.Core;

/// <summary>
/// Builds the Party Finder window title with the number of combined duplicates,
/// e.g. <c>Party Finder (12 duplicates combined)</c>, and recovers the game's
/// own title from it.
/// </summary>
/// <remarks>
/// The count is for the page currently shown (duplicates are tracked per page; see #8).
/// </remarks>
public static class TitleText
{
    private static readonly Regex OwnSuffix = new(@" \(\d+ duplicates? combined\)$", RegexOptions.CultureInvariant);

    /// <summary>The suffix for <paramref name="hiddenCount"/> combined listings, or <c>null</c> for none.</summary>
    public static string? Suffix(int hiddenCount) => hiddenCount switch
    {
        <= 0 => null,
        1 => " (1 duplicate combined)",
        _ => string.Create(CultureInfo.InvariantCulture, $" ({hiddenCount} duplicates combined)"),
    };

    /// <summary>
    /// The title to show: the game's title plus the suffix, or the game's title alone when nothing is combined.
    /// Any suffix already on <paramref name="currentTitle"/> is replaced, never stacked.
    /// </summary>
    public static string Compose(string currentTitle, int hiddenCount) =>
        Original(currentTitle) + (Suffix(hiddenCount) ?? string.Empty);

    /// <summary>The game's own title, with the plugin's suffix removed if present.</summary>
    public static string Original(string currentTitle) =>
        OwnSuffix.Replace(currentTitle ?? string.Empty, string.Empty);
}

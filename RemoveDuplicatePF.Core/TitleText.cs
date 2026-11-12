using System.Globalization;
using System.Text.RegularExpressions;

namespace RemoveDuplicatePF.Core;

/// <summary>
/// Builds the Party Finder window title with the number of hidden listings,
/// e.g. <c>Party Finder (7 duplicates hidden)</c>, and recovers the game's own title from it.
/// </summary>
/// <remarks>
/// The number counts hidden listings only (owner's decision on #71): the copies hidden on the
/// page, whether the shown copy of their ad is on this page or another (#88). The visible copy
/// isn't counted. Use <see cref="DisplayedResultSet.HiddenTotal"/> for the page on
/// screen (#51). The plugin shows the suffix in the prefix colour; this class only deals with
/// plain text.
/// </remarks>
public static class TitleText
{
    // Current suffix " (7 duplicates hidden)" / " (1 duplicate hidden)", plus the older
    // " (7 duplicates)" (v1.1.x) and " (4 duplicates combined)" (v1.0.0), so a title left over
    // from an older version is cleaned up rather than stacked.
    private static readonly Regex OwnSuffix = new(@" \(\d+ duplicates?( hidden| combined)?\)$", RegexOptions.CultureInvariant);

    /// <summary>The suffix for <paramref name="hidden"/> hidden listings, or <c>null</c> when none are hidden.</summary>
    public static string? Suffix(int hidden) => hidden switch
    {
        < 1 => null,
        1 => " (1 duplicate hidden)",
        _ => string.Create(CultureInfo.InvariantCulture, $" ({hidden} duplicates hidden)"),
    };

    /// <summary>
    /// The title to show as plain text: the game's title plus the suffix, or the game's title alone.
    /// Any suffix already on <paramref name="currentTitle"/> is replaced, never stacked.
    /// </summary>
    public static string Compose(string currentTitle, int hidden) =>
        Original(currentTitle) + (Suffix(hidden) ?? string.Empty);

    /// <summary>The game's own title, with the plugin's suffix removed if present.</summary>
    public static string Original(string currentTitle) =>
        OwnSuffix.Replace(currentTitle ?? string.Empty, string.Empty);
}

using System.Globalization;
using System.Text.RegularExpressions;

namespace RemoveDuplicatePF.Core;

/// <summary>
/// Parses the Party Finder window's range text (bottom right), e.g. <c>51-93</c>.
/// </summary>
public static class PageRange
{
    /// <summary>Listings per Party Finder page.</summary>
    public const int PageSize = 50;

    private static readonly Regex Range = new(@"^\s*(\d+)\s*-\s*(\d+)\s*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The one-based page shown by range text like <c>51-93</c> (page 2), or <c>null</c> if the
    /// text isn't a range (empty while loading, or unrecognised).
    /// </summary>
    public static int? PageOf(string? rangeText)
    {
        var match = Range.Match(rangeText ?? string.Empty);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var start) || start < 1)
            return null;

        return ((start - 1) / PageSize) + 1;
    }
}

/// <summary>
/// Works out which page a result set belongs to (#74).
/// </summary>
/// <remarks>
/// The game's page index (zero-based, <c>InfoProxyCrossRealm</c> + 0x0484) is already correct
/// when a result set's first listing arrives (#73). It isn't a documented field, so a game patch
/// could move it. After each result set, <see cref="Verify"/> compares it with the window's range
/// text. Two disagreements in a row (one could be a single stale frame) mark the index
/// unreliable for the rest of the session, after which every result set counts as page 1:
/// the safe default, which hides nothing because of other pages.
/// </remarks>
public sealed class PageTracker
{
    /// <summary>Page indexes at or above this are treated as invalid.</summary>
    public const int MaxPlausiblePages = 50;

    private int consecutiveMismatches;

    /// <summary>Whether the game's page index has disagreed with the window and is being ignored.</summary>
    public bool Unreliable { get; private set; }

    /// <summary>
    /// The one-based page for a result set starting now, from the game's zero-based page index.
    /// Unknown, implausible or unreliable values give page 1.
    /// </summary>
    public int PageFor(int? gamePageIndex) =>
        !this.Unreliable && gamePageIndex is >= 0 and < MaxPlausiblePages ? gamePageIndex.Value + 1 : 1;

    /// <summary>
    /// Compares the page assigned to the result set on screen with the window's range text.
    /// Returns <c>true</c> on the call that marks the page index unreliable, so the caller can warn once.
    /// </summary>
    /// <param name="assignedPage">The page this tracker assigned to the result set on screen.</param>
    /// <param name="rangeText">The window's range text, e.g. <c>51-93</c>.</param>
    public bool Verify(int assignedPage, string? rangeText)
    {
        if (this.Unreliable || PageRange.PageOf(rangeText) is not { } shownPage)
            return false;

        if (shownPage == assignedPage)
        {
            this.consecutiveMismatches = 0;
            return false;
        }

        this.consecutiveMismatches++;
        if (this.consecutiveMismatches < 2)
            return false;

        this.Unreliable = true;
        return true;
    }
}

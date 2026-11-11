using System.Text.RegularExpressions;

namespace RemoveDuplicatePF.Core;

/// <summary>What to do with one Party Finder row's description text.</summary>
/// <param name="Description">The duplicate group's description this row belongs to.</param>
/// <param name="Prefix">The prefix the row should show.</param>
/// <param name="State">Whether the row's text is original, correctly prefixed, or has a stale prefix.</param>
public sealed record RowPrefixDecision(string Description, CountPrefix Prefix, RowPrefixState State);

/// <summary>The current state of a row's text relative to the plugin's prefix.</summary>
public enum RowPrefixState
{
    /// <summary>The game's original text: the prefix should be added.</summary>
    Original,

    /// <summary>Already shows the correct prefix: nothing to do.</summary>
    Prefixed,

    /// <summary>Shows the plugin's prefix with an out-of-date count: rebuild from the original text.</summary>
    StalePrefix,
}

/// <summary>
/// Matches the text shown in a Party Finder row to a duplicate group by its description.
/// Only one listing per duplicate description is left visible, so a row whose text
/// is that description belongs to that group, whichever row it's in.
/// </summary>
/// <remarks>
/// A row matches when its text (as plain text) is:
/// <list type="bullet">
/// <item>exactly the description;</item>
/// <item>the description preceded only by bracketed game tags, e.g. <c>[Practice] desc</c>;</item>
/// <item>a shortened form ending in <c>...</c> or <c>…</c> that only one group's description starts with;</item>
/// <item>any of the above preceded by the plugin's own <c>[N] </c> prefix.</item>
/// </list>
/// </remarks>
public static class RowPrefixMatcher
{
    private static readonly Regex OwnPrefix = new(@"^\[(\d+)\] ", RegexOptions.CultureInvariant);
    private static readonly Regex LeadingTags = new(@"^(\s*\[[^\]\r\n]*\])+\s*", RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns what to do with a row, or <c>null</c> if the row isn't a duplicate group's kept listing.
    /// </summary>
    /// <param name="rowText">The row's description text as plain text.</param>
    /// <param name="groupsByDescription">Duplicate group sizes keyed by description (sizes of 2 or more).</param>
    public static RowPrefixDecision? Decide(string rowText, IReadOnlyDictionary<string, int> groupsByDescription)
    {
        if (string.IsNullOrWhiteSpace(rowText) || groupsByDescription.Count == 0)
            return null;

        // 1. Exact description first, so one that itself starts with "[2] " still matches as original text.
        if (groupsByDescription.TryGetValue(rowText, out var exactSize))
            return Build(rowText, exactSize, RowPrefixState.Original);

        // 2. Our own "[N] " prefix next. This must come before the game-tag rule, which would
        //    otherwise read "[3]" as a tag and prefix the row again on every scan.
        var own = OwnPrefix.Match(rowText);
        if (own.Success && FindGroup(rowText[own.Length..], groupsByDescription) is { } prefixed)
        {
            var shownCount = int.TryParse(own.Groups[1].Value, out var n) ? n : -1;
            var state = shownCount == prefixed.Size ? RowPrefixState.Prefixed : RowPrefixState.StalePrefix;
            return Build(prefixed.Description, prefixed.Size, state);
        }

        // 3. Game tags in front, or shortened text.
        return FindGroup(rowText, groupsByDescription) is { } match
            ? Build(match.Description, match.Size, RowPrefixState.Original)
            : null;
    }

    /// <summary>
    /// If <paramref name="rowText"/> shows the plugin's prefix in front of a known description,
    /// returns that description; otherwise <c>null</c>. Used to restore rows on unload.
    /// </summary>
    public static string? DescriptionBehindOwnPrefix(string rowText, IEnumerable<string> knownDescriptions)
    {
        var own = OwnPrefix.Match(rowText ?? string.Empty);
        if (!own.Success)
            return null;

        var groups = knownDescriptions.Distinct(StringComparer.Ordinal).ToDictionary(d => d, _ => 2, StringComparer.Ordinal);
        return FindGroup(rowText![own.Length..], groups)?.Description;
    }

    /// <summary>Leading characters a row must share with a description to count as a near miss.</summary>
    public const int NearMissLength = 10;

    /// <summary>
    /// Diagnostics (#54): if <paramref name="rowText"/> doesn't match any group but starts like one
    /// (after removing the plugin's prefix and game tags), returns that group's description.
    /// Otherwise returns <c>null</c>. Used to log rows that should probably have been prefixed.
    /// </summary>
    public static string? NearMiss(string rowText, IReadOnlyDictionary<string, int> groupsByDescription)
    {
        if (string.IsNullOrWhiteSpace(rowText) || Decide(rowText, groupsByDescription) is not null)
            return null;

        var text = rowText;
        var own = OwnPrefix.Match(text);
        if (own.Success)
            text = text[own.Length..];

        var tags = LeadingTags.Match(text);
        if (tags.Success && tags.Length < text.Length)
            text = text[tags.Length..];

        foreach (var description in groupsByDescription.Keys)
        {
            var needed = Math.Min(NearMissLength, description.Length);
            if (text.Length >= needed && string.CompareOrdinal(text, 0, description, 0, needed) == 0)
                return description;
        }

        return null;
    }

    private static RowPrefixDecision? Build(string description, int size, RowPrefixState state) =>
        CountPrefix.For(size) is { } prefix ? new RowPrefixDecision(description, prefix, state) : null;

    private static (string Description, int Size)? FindGroup(string text, IReadOnlyDictionary<string, int> groups)
    {
        if (groups.TryGetValue(text, out var size))
            return (text, size);

        var tags = LeadingTags.Match(text);
        if (tags.Success && tags.Length < text.Length && groups.TryGetValue(text[tags.Length..], out size))
            return (text[tags.Length..], size);

        return FindTruncated(text, groups) ?? (tags.Success && tags.Length < text.Length
            ? FindTruncated(text[tags.Length..], groups)
            : null);
    }

    private static (string Description, int Size)? FindTruncated(string text, IReadOnlyDictionary<string, int> groups)
    {
        string stem;
        if (text.EndsWith("...", StringComparison.Ordinal))
            stem = text[..^3];
        else if (text.EndsWith('…'))
            stem = text[..^1];
        else
            return null;

        if (stem.Length == 0)
            return null;

        (string, int)? found = null;
        foreach (var (description, size) in groups)
        {
            if (!description.StartsWith(stem, StringComparison.Ordinal) || description.Length == stem.Length)
                continue;

            if (found is not null)
                return null; // ambiguous: more than one group could be behind this shortened text

            found = (description, size);
        }

        return found;
    }
}

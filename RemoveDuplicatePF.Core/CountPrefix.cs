namespace RemoveDuplicatePF.Core;

/// <summary>
/// The coloured <c>[N]</c> prefix shown before a listing that has duplicates.
/// </summary>
/// <param name="Text">The prefix text, e.g. <c>"[3] "</c>, including the trailing space.</param>
/// <param name="ColorId">
/// Row ID in the game's <c>UIColor</c> sheet. The plugin applies it with a
/// <c>UIForeground</c> payload; Core stays free of game types.
/// </param>
public sealed record CountPrefix(string Text, ushort ColorId)
{
    /// <summary>
    /// <c>UIColor</c> row used for the prefix: an orange tone that stands out
    /// from the regular white Party Finder description text.
    /// </summary>
    public const ushort DefaultColorId = 500;

    /// <summary>
    /// Builds the prefix for a group of <paramref name="groupSize"/> listings.
    /// Returns <c>null</c> when there is nothing to show (a group of 1 or less).
    /// </summary>
    public static CountPrefix? For(int groupSize) =>
        groupSize >= 2 ? new CountPrefix($"[{groupSize}] ", DefaultColorId) : null;
}

namespace RemoveDuplicatePF.Core;

/// <summary>
/// The rule for which listing descriptions count as duplicates.
/// </summary>
/// <remarks>
/// <para>
/// Descriptions are compared by their first <see cref="Length"/> characters
/// (ordinal, case-sensitive). Shorter descriptions must match in full.
/// </para>
/// <para>
/// Design decision (#42): the length matches what the Debug log prints, and the log
/// prints this same <see cref="Key"/>, so listings that look identical in the log are
/// always combined and test results can be checked from the log alone.
/// </para>
/// <para>
/// Accepted consequence: ads that share their first 30 characters are merged even if
/// the rest of their text differs. Such ads are treated as boilerplate (a shared header,
/// a copied template, a repost with a different time) and aren't unique enough for their
/// own row. This is intended, not a bug. See docs/party-finder-notes.md,
/// "Design decision: match on the first 30 characters".
/// </para>
/// </remarks>
public static class DescriptionMatch
{
    /// <summary>Number of leading characters compared.</summary>
    public const int Length = 30;

    /// <summary>
    /// Returns the part of <paramref name="description"/> used for matching:
    /// its first <see cref="Length"/> characters, or all of it if shorter.
    /// </summary>
    public static string Key(string description) =>
        description.Length > Length ? description[..Length] : description;
}

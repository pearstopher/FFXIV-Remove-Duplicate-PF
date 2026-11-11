namespace RemoveDuplicatePF.Core;

/// <summary>
/// The rule for which listing descriptions count as duplicates.
/// </summary>
/// <remarks>
/// Descriptions are compared by their first <see cref="Length"/> characters
/// (ordinal, case-sensitive). Shorter descriptions must match in full.
/// The Debug log prints the same <see cref="Key"/>, so listings that look
/// identical in the log are always combined.
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

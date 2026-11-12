using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>Feeds listings to a <see cref="ListingFilter"/> the way the game sends them.</summary>
internal static class GameFeed
{
    /// <summary>
    /// Feeds one page and returns each listing's visibility. Listings arrive in segments of 4
    /// numbered 1, 2, ..., with the last one numbered 0; the game's list is empty only while the
    /// first segment is processed. If any listing is shown, the window then shows the page
    /// (<see cref="ListingFilter.NoteGameListShown"/>) before the player moves on.
    /// </summary>
    public static bool[] Page(ListingFilter filter, IEnumerable<(ulong Id, string? Description)> listings)
    {
        var result = new List<bool>();
        var segments = listings.Chunk(ResultSetBoundary.MaxListingsPerSegment).ToArray();
        for (var s = 0; s < segments.Length; s++)
        {
            var batch = s == segments.Length - 1 ? 0 : s + 1;
            foreach (var (id, description) in segments[s])
                result.Add(filter.Decide(batch, id, description, true, gameListEmpty: s == 0));
        }

        if (result.Any(visible => visible))
            filter.NoteGameListShown();

        return result.ToArray();
    }
}

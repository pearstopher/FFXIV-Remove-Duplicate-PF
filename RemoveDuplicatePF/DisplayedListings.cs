using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Reads which listings the Party Finder window is showing, and picks the
/// matching result set for the title and <c>[N]</c> prefixes (#51).
/// </summary>
internal static unsafe class DisplayedListings
{
    private static int lastLoggedSetNumber = -1;

    /// <summary>
    /// The result set that matches the page on screen. The game can fetch another
    /// page in the background, so the newest result set isn't always the one shown.
    /// </summary>
    public static DisplayedResultSet ResultSetOnScreen(ListingFilter filter)
    {
        // Only the first NumberOfListingsDisplayed slots are on screen; the rest can still hold
        // the previous page's IDs (#74).
        var agent = AgentLookingForGroup.Instance();
        var shown = agent == null
            ? filter.ForDisplayed(Array.Empty<ulong>())
            : filter.ForDisplayed(agent->Listings.ListingIds.ToArray(), agent->NumberOfListingsDisplayed);

        // Debug log when the result set in use changes, so mismatches can be traced in dalamud.log.
        if (shown.ResultSetNumber != lastLoggedSetNumber)
        {
            lastLoggedSetNumber = shown.ResultSetNumber;
            Plugin.Log.Debug(
                "PF on screen: result set #{Set} ({Which}, {Matched} listings matched, {Hidden} hidden)",
                shown.ResultSetNumber,
                shown.IsNewest ? "newest" : "older",
                shown.MatchedListings,
                shown.HiddenTotal);
        }

        return shown;
    }

    /// <summary>
    /// Whether the game's displayed listing list is empty right now. The game empties it whenever
    /// it requests new results (page, refresh, category, opening the window), so a listing that
    /// arrives while it's empty belongs to a new result set (#72). <c>false</c> if unknown.
    /// </summary>
    public static bool GameListIsEmpty()
    {
        var agent = AgentLookingForGroup.Instance();
        return agent != null && agent->NumberOfListingsDisplayed == 0;
    }

    /// <summary>Whether the game's displayed listing list has listings right now (#75).</summary>
    public static bool GameListHasListings()
    {
        var agent = AgentLookingForGroup.Instance();
        return agent != null && agent->NumberOfListingsDisplayed > 0;
    }
}

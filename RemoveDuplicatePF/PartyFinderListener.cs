using System;
using Dalamud.Game.Gui.PartyFinder.Types;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Connects Dalamud's Party Finder listing event to <see cref="ListingFilter"/>.
/// It only changes <c>args.Visible</c> on incoming Party Finder listings.
/// </summary>
internal sealed class PartyFinderListener : IDisposable
{
    private readonly ListingFilter filter;

    public PartyFinderListener(ListingFilter filter)
    {
        this.filter = filter;
        Plugin.PartyFinderGui.ReceiveListing += this.OnReceiveListing;
    }

    public void Dispose()
    {
        Plugin.PartyFinderGui.ReceiveListing -= this.OnReceiveListing;
    }

    private void OnReceiveListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args)
    {
        try
        {
            var description = listing.Description.TextValue;
            var visible = this.filter.Decide(args.BatchNumber, listing.Id, description, args.Visible);
            args.Visible = visible;

            // Debug level: shown in /xllog only when its log level is set to Debug.
            // Answers the in-game questions in docs/party-finder-notes.md.
            Plugin.Log.Debug(
                "PF batch={Batch} id={Id} secs={Secs} visible={Visible} text={Text}",
                args.BatchNumber,
                listing.Id,
                listing.SecondsRemaining,
                visible,
                description.Length > 30 ? description[..30] : description);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error handling Party Finder listing");
        }
    }
}

using System;
using System.Threading;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Plugin.Services;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Connects Dalamud's Party Finder listing event to <see cref="ListingFilter"/>.
/// It only changes <c>args.Visible</c> on incoming Party Finder listings. It also tells the filter
/// each frame, so result sets are split from what the plugin received rather than the window (#94).
/// </summary>
internal sealed class PartyFinderListener : IDisposable
{
    private readonly ListingFilter filter;

    // Cancels a pending summary log if the plugin unloads first.
    private readonly CancellationTokenSource disposed = new();

    // Result set whose summary is already scheduled, so it's logged once.
    private int summaryScheduledFor = -1;

    public PartyFinderListener(ListingFilter filter)
    {
        this.filter = filter;
        Plugin.PartyFinderGui.ReceiveListing += this.OnReceiveListing;
        Plugin.Framework.Update += this.OnFrame;
    }

    public void Dispose()
    {
        Plugin.PartyFinderGui.ReceiveListing -= this.OnReceiveListing;
        Plugin.Framework.Update -= this.OnFrame;
        this.disposed.Cancel();
        this.disposed.Dispose();
    }

    private void OnReceiveListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args)
    {
        try
        {
            args.Visible = this.filter.Decide(
                args.BatchNumber,
                listing.Id,
                listing.Description.TextValue,
                args.Visible);

            // Batch 0 is the result set's final segment. Log one summary line a frame later,
            // after the whole segment is processed, instead of a line per listing (#11).
            var set = this.filter.NewestResultSetNumber;
            if (args.BatchNumber == 0 && this.summaryScheduledFor != set)
            {
                this.summaryScheduledFor = set;
                Plugin.Framework.RunOnTick(this.LogSummary, delayTicks: 1, cancellationToken: this.disposed.Token);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error handling Party Finder listing");
        }
    }

    // Every frame: lets the filter tell that a result set is complete once its last packet has
    // arrived and a frame has passed (#94). One field write.
    private void OnFrame(IFramework framework) => this.filter.NoteFrame();

    private void LogSummary()
    {
        // Debug level: shown in /xllog and dalamud.log only when the log level is Debug+.
        Plugin.Log.Debug("PF {Summary}", this.filter.NewestSummary.Describe());
    }
}

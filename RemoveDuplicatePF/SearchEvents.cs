using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Passes the Party Finder window's button clicks and window opens to <see cref="ListingFilter"/>,
/// so it can tell a new search's page 1 from going back to page 1 with Previous (#80).
/// </summary>
/// <remarks>
/// Only reads events; never changes or blocks them. Clicks arrive about a tenth of a second
/// before the results they request, so they're noted before the result set starts.
/// </remarks>
internal sealed class SearchEvents : IDisposable
{
    private const string AddonName = "LookingForGroup";

    private readonly ListingFilter filter;

    public SearchEvents(ListingFilter filter)
    {
        this.filter = filter;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, AddonName, this.OnEvent);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, this.OnSetup);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, AddonName, this.OnEvent);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, AddonName, this.OnSetup);
    }

    private void OnSetup(AddonEvent type, AddonArgs args) => this.filter.NoteWindowOpened();

    private void OnEvent(AddonEvent type, AddonArgs args)
    {
        try
        {
            if (args is AddonReceiveEventArgs e && (AtkEventType)e.AtkEventType == AtkEventType.ButtonClick)
                this.filter.NoteButtonClick(e.EventParam);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error reading a Party Finder button click");
        }
    }
}

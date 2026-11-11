using System;
using System.Diagnostics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Shows the number of combined duplicates in the Party Finder window title,
/// e.g. <c>Party Finder (12 duplicates combined)</c>, and restores the game's
/// title on unload.
/// </summary>
/// <remarks>
/// The title is the window component's title text node (<c>AtkComponentWindow</c>,
/// node index 0). The count changes as listings arrive and the game may rewrite
/// the title, so it's checked after list updates and otherwise every <see cref="CheckInterval"/>.
/// When the window closes, the game rebuilds the title from scratch next time it opens.
/// </remarks>
internal sealed unsafe class TitleUpdater : IDisposable
{
    private const string AddonName = "LookingForGroup";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(250);

    private readonly ListingFilter filter;
    private readonly Stopwatch sinceLastCheck = Stopwatch.StartNew();
    private bool loggedMissingTitle;

    public TitleUpdater(ListingFilter filter)
    {
        this.filter = filter;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, this.OnListChanged);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, this.OnListChanged);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, this.OnUpdate);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, this.OnListChanged);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, AddonName, this.OnListChanged);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, AddonName, this.OnUpdate);

        try
        {
            var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName).Address;
            if (addon != null)
                SetTitle(addon, TitleText.Original);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error restoring Party Finder title");
        }
    }

    private void OnListChanged(AddonEvent type, AddonArgs args) => this.Update(args);

    private void OnUpdate(AddonEvent type, AddonArgs args)
    {
        if (this.sinceLastCheck.Elapsed >= CheckInterval)
            this.Update(args);
    }

    private void Update(AddonArgs args)
    {
        this.sinceLastCheck.Restart();
        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (addon == null || !addon->IsVisible)
                return;

            // Use the result set for the page on screen, not just the newest one (#51).
            var hidden = DisplayedListings.ResultSetOnScreen(this.filter).Tracker.HiddenCount;
            if (!SetTitle(addon, current => TitleText.Compose(current, hidden)) && !this.loggedMissingTitle)
            {
                this.loggedMissingTitle = true;
                Plugin.Log.Warning("Party Finder title text node not found; title count disabled");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error updating Party Finder title");
        }
    }

    // Applies 'change' to the title text; only writes when it actually changes.
    // Returns false if the title node couldn't be found.
    private static bool SetTitle(AtkUnitBase* addon, Func<string, string> change)
    {
        var node = FindTitleNode(addon);
        if (node == null)
            return false;

        var current = SeString.Parse(node->NodeText.AsSpan()).TextValue;
        var desired = change(current);
        if (desired != current)
        {
            node->SetText(desired);
            Plugin.Log.Debug("PF title: {Title}", desired);
        }

        return true;
    }

    private static AtkTextNode* FindTitleNode(AtkUnitBase* addon)
    {
        var windowNode = addon->WindowNode;
        if (windowNode == null || windowNode->Component == null)
            return null;

        var window = (AtkComponentWindow*)windowNode->Component;
        var titleNodeId = window->NodeIds[0];
        return titleNodeId > 0 ? window->GetTextNodeById((uint)titleNodeId) : null;
    }
}

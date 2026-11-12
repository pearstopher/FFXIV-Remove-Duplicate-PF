using System;
using System.Diagnostics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Shows the total number of duplicate listings in the Party Finder window title,
/// e.g. <c>Party Finder (7 duplicates)</c> with the count in the <c>[N]</c> prefix colour,
/// and restores the game's title on unload. The total equals the sum of the <c>[N]</c>
/// prefixes on the page (#64).
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
                SetTitle(addon, total: null);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error restoring Party Finder title");
        }
    }

    private void OnListChanged(AddonEvent type, AddonArgs args) => this.Update(args);

    private void OnUpdate(AddonEvent type, AddonArgs args)
    {
        // RESEARCH ONLY (#71): check the window's page text every frame to catch exactly when it changes.
        PageDiagnostics.LogIfWindowChanged();

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

            // Use the result set for the page on screen, not just the newest one (#51). The total
            // includes the kept listing of each group, so it equals the sum of the [N] prefixes (#64).
            var total = DisplayedListings.ResultSetOnScreen(this.filter).Tracker.DuplicateListingCount;
            if (!SetTitle(addon, total) && !this.loggedMissingTitle)
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

    // Sets the title to the game's title plus " (N duplicates)" in the [N] prefix colour, or the
    // game's title alone when total is under 2 (or null, to restore on unload). Only writes when
    // the encoded text actually differs, so a title the game reset to plain text gets recoloured.
    // Returns false if the title node couldn't be found.
    private static bool SetTitle(AtkUnitBase* addon, int? total)
    {
        var node = FindTitleNode(addon);
        if (node == null)
            return false;

        var currentBytes = node->NodeText.AsSpan();
        var original = TitleText.Original(SeString.Parse(currentBytes).TextValue);
        var suffix = total is { } n ? TitleText.Suffix(n) : null;

        var builder = new SeStringBuilder().AddText(original);
        if (suffix is not null)
        {
            builder
                .AddText(" ")
                .AddUiForeground(CountPrefix.DefaultColorId)
                .AddText(suffix.TrimStart())
                .AddUiForegroundOff();
        }

        var desired = builder.Encode();
        if (!currentBytes.SequenceEqual(desired))
        {
            node->SetText(desired);
            Plugin.Log.Debug("PF title: {Title}", original + suffix);
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

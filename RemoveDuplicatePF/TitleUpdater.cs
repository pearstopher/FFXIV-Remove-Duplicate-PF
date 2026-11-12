using System;
using System.Diagnostics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Shows the number of hidden listings in the Party Finder window title,
/// e.g. <c>Party Finder (7 duplicates hidden)</c> with the count in the <c>[N]</c> prefix colour,
/// and restores the game's title on unload. It counts extra copies within the page plus
/// listings already seen on an earlier page; the visible copy of each group isn't counted (#75).
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

    // The title node doesn't exist yet on the window's first frame, so only warn when it stays
    // missing across this many checks in a row (about 5 seconds).
    private const int MissingTitleChecksBeforeWarning = 20;
    private int missingTitleChecks;

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
                SetTitle(addon, hidden: null);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error restoring Party Finder title");
        }
    }

    private void OnListChanged(AddonEvent type, AddonArgs args) => this.Update(args);

    private void OnUpdate(AddonEvent type, AddonArgs args)
    {
        // Every frame: note when the window has listings on screen, so two small result sets in a
        // row are told apart (#75; see ResultSetBoundary.NoteGameListShown). A cheap field read.
        if (DisplayedListings.GameListHasListings())
            this.filter.NoteGameListShown();

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

            // Use the result set for the page on screen, not just the newest one (#51). The number
            // counts hidden listings only: extra copies within the page plus listings already seen
            // on an earlier page (#75).
            var shown = DisplayedListings.ResultSetOnScreen(this.filter);
            this.VerifyPage((AddonLookingForGroup*)addon, shown);

            if (SetTitle(addon, shown.HiddenTotal))
            {
                this.missingTitleChecks = 0;
            }
            else if (++this.missingTitleChecks >= MissingTitleChecksBeforeWarning && !this.loggedMissingTitle)
            {
                this.loggedMissingTitle = true;
                Plugin.Log.Warning("Party Finder title text node not found; the title count can't be shown");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error updating Party Finder title");
        }
    }

    // Cross-checks the page assigned to the result set on screen against the window's range text
    // (e.g. "51-93" = page 2), because the game's page index isn't a documented field (#74). Runs
    // here so no extra window listener is needed. Only checks when the window is showing that
    // result set's listings. Two mismatches in a row stop trusting the index for this session.
    private void VerifyPage(AddonLookingForGroup* addon, DisplayedResultSet shown)
    {
        if (shown.MatchedListings == 0 || addon->CurrentPageTextNode == null)
            return;

        var rangeText = SeString.Parse(addon->CurrentPageTextNode->NodeText.AsSpan()).TextValue;
        if (this.filter.Pages.Verify(shown.Page, rangeText, DisplayedListings.DisplayedCount()))
        {
            Plugin.Log.Warning(
                "Party Finder page index disagrees with the window (assigned page {Page}, window shows \"{Range}\"); counting every page as page 1 for this session",
                shown.Page,
                rangeText);
        }
    }

    // Sets the title to the game's title plus " (N duplicates hidden)" in the [N] prefix colour, or the
    // game's title alone when nothing is hidden (or null, to restore on unload). Only writes when
    // the encoded text actually differs, so a title the game reset to plain text gets recoloured.
    // Returns false if the title node couldn't be found.
    private static bool SetTitle(AtkUnitBase* addon, int? hidden)
    {
        var node = FindTitleNode(addon);
        if (node == null)
            return false;

        var currentBytes = node->NodeText.AsSpan();
        var original = TitleText.Original(SeString.Parse(currentBytes).TextValue);
        var suffix = hidden is { } n ? TitleText.Suffix(n) : null;

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

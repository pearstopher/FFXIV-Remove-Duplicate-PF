using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Adds the coloured <c>[N]</c> prefix to the rows of the Party Finder window
/// (<c>LookingForGroup</c>) that are the kept copy of a duplicate group, and
/// restores the original text on unload.
/// </summary>
/// <remarks>
/// Rows are matched to groups by their description text (see <see cref="RowPrefixMatcher"/>),
/// so this doesn't depend on the row layout or listing order. The window reuses rows
/// as you scroll and rewrites their text, so the scan runs after list updates and
/// otherwise at most every <see cref="ScanInterval"/>.
/// </remarks>
internal sealed unsafe class ListingPrefixer : IDisposable
{
    private const string AddonName = "LookingForGroup";
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(250);

    private readonly ListingFilter filter;

    // Original row text (encoded SeString) per description, captured before the first prefix is added.
    private readonly Dictionary<string, byte[]> originalTextByDescription = new(StringComparer.Ordinal);
    private readonly Stopwatch sinceLastScan = Stopwatch.StartNew();
    private string lastLoggedSummary = string.Empty;

    public ListingPrefixer(ListingFilter filter)
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
            this.RestoreOriginalText();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error restoring Party Finder text");
        }
    }

    private void OnListChanged(AddonEvent type, AddonArgs args) => this.Scan(args);

    private void OnUpdate(AddonEvent type, AddonArgs args)
    {
        if (this.sinceLastScan.Elapsed >= ScanInterval)
            this.Scan(args);
    }

    private void Scan(AddonArgs args)
    {
        this.sinceLastScan.Restart();
        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (addon == null || !addon->IsVisible)
                return;

            var groups = this.filter.Tracker.DuplicateGroupsByDescription;
            if (groups.Count == 0)
                return;

            var matched = 0;
            ForEachTextNode(&addon->UldManager, node =>
            {
                if (this.ApplyPrefix(node, groups))
                    matched++;
            });

            this.LogSummary(groups.Count, matched);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error adding Party Finder prefixes");
        }
    }

    // Returns true if the node belongs to a duplicate group (whether or not it needed changing).
    private bool ApplyPrefix(AtkTextNode* node, IReadOnlyDictionary<string, int> groups)
    {
        var bytes = node->NodeText.AsSpan();
        if (bytes.Length == 0)
            return false;

        var current = SeString.Parse(bytes);
        var decision = RowPrefixMatcher.Decide(current.TextValue, groups);
        if (decision is null)
            return false;

        switch (decision.State)
        {
            case RowPrefixState.Prefixed:
                return true;
            case RowPrefixState.Original:
                this.originalTextByDescription[decision.Description] = bytes.ToArray();
                break;
            case RowPrefixState.StalePrefix when !this.originalTextByDescription.ContainsKey(decision.Description):
                return true; // can't rebuild without the original; leave the row as it is
        }

        var original = SeString.Parse(this.originalTextByDescription[decision.Description]);
        var prefixed = new SeStringBuilder()
            .AddUiForeground(decision.Prefix.ColorId)
            .AddText(decision.Prefix.Text)
            .AddUiForegroundOff()
            .Append(original)
            .Encode();
        node->SetText(prefixed);
        return true;
    }

    private void RestoreOriginalText()
    {
        if (this.originalTextByDescription.Count == 0)
            return;

        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName).Address;
        if (addon == null)
            return;

        var restored = 0;
        ForEachTextNode(&addon->UldManager, node =>
        {
            var bytes = node->NodeText.AsSpan();
            if (bytes.Length == 0)
                return;

            var text = SeString.Parse(bytes).TextValue;
            var description = RowPrefixMatcher.DescriptionBehindOwnPrefix(text, this.originalTextByDescription.Keys);
            if (description is not null && this.originalTextByDescription.TryGetValue(description, out var original))
            {
                node->SetText(original);
                restored++;
            }
        });

        Plugin.Log.Debug("Restored original text on {Count} Party Finder rows", restored);
    }

    // Debug summary, logged only when it changes. Shows whether rows are being matched in game.
    private void LogSummary(int groupCount, int matchedRows)
    {
        var summary = $"{matchedRows} rows matched for {groupCount} duplicate groups";
        if (summary == this.lastLoggedSummary)
            return;

        this.lastLoggedSummary = summary;
        Plugin.Log.Debug("PF prefix: {Summary}", summary);
    }

    private static void ForEachTextNode(AtkUldManager* manager, TextNodeVisitor visit) =>
        ForEachTextNodeCore(manager, visit, 0);

    private delegate void TextNodeVisitor(AtkTextNode* node);

    private static void ForEachTextNodeCore(AtkUldManager* manager, TextNodeVisitor visit, int depth)
    {
        // Components can nest; the depth limit guards against a malformed tree.
        if (manager == null || manager->NodeList == null || depth > 8)
            return;

        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null)
                continue;

            if (node->Type == NodeType.Text)
            {
                visit((AtkTextNode*)node);
            }
            else if ((ushort)node->Type >= 1000)
            {
                var component = ((AtkComponentNode*)node)->Component;
                if (component != null)
                    ForEachTextNodeCore(&component->UldManager, visit, depth + 1);
            }
        }
    }
}

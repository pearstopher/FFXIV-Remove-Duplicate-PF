using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
/// otherwise at most every <see cref="ScanInterval"/>. A node is only parsed again when its text
/// or the <c>[N]</c> counts have changed since it was last handled (#95).
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

    // Row texts already reported as near misses, so each is logged once (#54 diagnostics).
    private readonly HashSet<string> loggedNearMisses = new(StringComparer.Ordinal);

    // Text node -> the text it had after it was last handled, and the group it belongs to (or null).
    // Handling depends only on the text and the [N] counts, so an unchanged node is skipped while
    // the counts are unchanged. Cleared when the counts or the window change.
    private readonly Dictionary<nint, (byte[] Text, string? Description)> handledNodes = new();
    private Dictionary<string, int> handledGroups = new(StringComparer.Ordinal);
    private nint handledAddon;

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

            // Use the result set for the page on screen, not just the newest one (#51). Each [N] counts
            // every copy found so far in the search, including copies on other pages (#88).
            var groups = DisplayedListings.ResultSetOnScreen(this.filter).Prefixes;
            if (groups.Count == 0)
                return;

            if ((nint)addon != this.handledAddon || !SameGroups(groups, this.handledGroups))
            {
                this.handledNodes.Clear();
                this.handledGroups = new Dictionary<string, int>(groups, StringComparer.Ordinal);
                this.handledAddon = (nint)addon;
            }

            // The summary and near-miss checks are only for the Debug log.
            var debug = Plugin.DebugLogging;
            var matched = debug ? new HashSet<string>(StringComparer.Ordinal) : null;
            ForEachTextNode(&addon->UldManager, node =>
            {
                if (this.HandleNode(node, groups, debug) is { } description)
                    matched?.Add(description);
            });

            if (matched is not null)
                this.LogSummary(groups, matched);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error adding Party Finder prefixes");
        }
    }

    // Returns the group's description if the node belongs to a duplicate group (whether or not
    // it needed changing), otherwise null. Skips nodes already handled with the same text.
    private string? HandleNode(AtkTextNode* node, IReadOnlyDictionary<string, int> groups, bool debug)
    {
        var bytes = node->NodeText.AsSpan();
        if (bytes.Length == 0)
            return null;

        if (this.handledNodes.TryGetValue((nint)node, out var handled) && bytes.SequenceEqual(handled.Text))
            return handled.Description;

        var description = this.ApplyPrefix(node, bytes, groups, debug);
        this.handledNodes[(nint)node] = (node->NodeText.AsSpan().ToArray(), description);
        return description;
    }

    private static bool SameGroups(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b) =>
        a.Count == b.Count && a.All(g => b.TryGetValue(g.Key, out var size) && size == g.Value);

    // Returns the group's description if the node belongs to a duplicate group (whether or not
    // it needed changing), otherwise null.
    private string? ApplyPrefix(AtkTextNode* node, ReadOnlySpan<byte> bytes, IReadOnlyDictionary<string, int> groups, bool debug)
    {
        var current = SeString.Parse(bytes);
        var rowText = current.TextValue;
        var decision = RowPrefixMatcher.Decide(rowText, groups);
        if (decision is null)
        {
            if (debug)
                this.LogNearMiss(rowText, groups);
            return null;
        }

        switch (decision.State)
        {
            case RowPrefixState.Prefixed:
                return decision.Description;
            case RowPrefixState.Original:
                this.originalTextByDescription[decision.Description] = bytes.ToArray();
                break;
            case RowPrefixState.StalePrefix when !this.originalTextByDescription.ContainsKey(decision.Description):
                // Can't rebuild without the original; leave the row as it is (#54 diagnostics).
                Plugin.Log.Debug("PF prefix: stale prefix with no stored original: {Row}", Short(rowText));
                return decision.Description;
        }

        var original = SeString.Parse(this.originalTextByDescription[decision.Description]);
        var prefixed = new SeStringBuilder()
            .AddUiForeground(decision.Prefix.ColorId)
            .AddText(decision.Prefix.Text)
            .AddUiForegroundOff()
            .Append(original)
            .Encode();
        node->SetText(prefixed);
        return decision.Description;
    }

    // Diagnostics (#54): a row that starts like a duplicate group but didn't match it is
    // probably a kept listing that should have been prefixed. Logged once per row text.
    private void LogNearMiss(string rowText, IReadOnlyDictionary<string, int> groups)
    {
        var description = RowPrefixMatcher.NearMiss(rowText, groups);
        if (description is null || !this.loggedNearMisses.Add(rowText))
            return;

        Plugin.Log.Debug(
            "PF prefix NEAR MISS: row {Row} vs group {Group} (row {RowLength} chars, group {GroupLength} chars)",
            Quote(rowText),
            Quote(description),
            rowText.Length,
            description.Length);
    }

    // Shows hidden characters (line breaks, tabs) so mismatches are visible in the log.
    private static string Quote(string text) =>
        "\"" + text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";

    private static string Short(string text) => text.Length > 40 ? text[..40] + "…" : text;

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

    // Debug summary, logged only when it changes. Names the groups whose kept row is (and isn't)
    // prefixed on screen right now, so a missing [N] can be traced to its group (#54).
    // Groups scrolled out of view are expected under "not on screen".
    private void LogSummary(IReadOnlyDictionary<string, int> groups, HashSet<string> matched)
    {
        // Each group with its [N], which counts copies on every page of the search (#88).
        string Describe(string d) => $"{Short(d)} ×{groups[d]}";
        var prefixed = groups.Keys.Where(matched.Contains).Select(Describe).OrderBy(d => d, StringComparer.Ordinal);
        var missing = groups.Keys.Where(d => !matched.Contains(d)).Select(Describe).OrderBy(d => d, StringComparer.Ordinal);
        var summary =
            $"{matched.Count} of {groups.Count} groups prefixed on screen | prefixed: [{string.Join(" ; ", prefixed)}] | not on screen: [{string.Join(" ; ", missing)}]";
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

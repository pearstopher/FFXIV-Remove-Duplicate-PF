using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RemoveDuplicatePF;

/// <summary>
/// RESEARCH ONLY (#71, #73), not for merging. Logs the Party Finder window's page text, and at
/// each result-set start, every byte of <c>AgentLookingForGroup</c> and <c>InfoProxyCrossRealm</c>
/// that changed since the previous result-set start, to find where the game keeps the page.
/// </summary>
internal static unsafe class PageDiagnostics
{
    private const int AgentSize = 0x36D8;   // sizeof(AgentLookingForGroup)
    private const int ProxySize = 0x1A58;   // sizeof(InfoProxyCrossRealm)
    private const int MaxChangesLogged = 400;

    private static string lastWindowState = string.Empty;
    private static byte[]? lastAgent;
    private static byte[]? lastProxy;

    /// <summary>Logs the window state and changed struct bytes when a result set starts arriving.</summary>
    public static void LogResultSetStart(int resultSetNumber, int batchNumber)
    {
        Plugin.Log.Debug(
            "PF page diag: result set #{Set} STARTED (first batch={Batch}) | {State}",
            resultSetNumber,
            batchNumber,
            Describe());

        var agent = AgentLookingForGroup.Instance();
        if (agent == null)
            return;

        var agentNow = new ReadOnlySpan<byte>(agent, AgentSize).ToArray();
        var proxy = agent->InfoProxyCrossRealm;
        var proxyNow = proxy == null ? null : new ReadOnlySpan<byte>(proxy, ProxySize).ToArray();

        Plugin.Log.Debug("PF page diag3: set #{Set} agent changes: {Changes}", resultSetNumber, Diff(lastAgent, agentNow));
        Plugin.Log.Debug("PF page diag3: set #{Set} proxy changes: {Changes}", resultSetNumber, Diff(lastProxy, proxyNow));

        lastAgent = agentNow;
        lastProxy = proxyNow;
    }

    /// <summary>Logs the window state whenever it changes (called from the window's update listeners).</summary>
    public static void LogIfWindowChanged()
    {
        var state = Describe();
        if (state == lastWindowState)
            return;

        lastWindowState = state;
        Plugin.Log.Debug("PF page diag: window now | {State}", state);
    }

    // "0x1234=05>06 ..." for each changed byte, or "(first)" when there's nothing to compare yet.
    private static string Diff(byte[]? before, byte[]? now)
    {
        if (now == null)
            return "(missing)";
        if (before == null || before.Length != now.Length)
            return "(first)";

        var changes = new List<string>();
        for (var i = 0; i < now.Length; i++)
        {
            if (before[i] == now[i])
                continue;

            if (changes.Count == MaxChangesLogged)
            {
                changes.Add("…");
                break;
            }

            changes.Add($"0x{i:X4}={before[i]:X2}>{now[i]:X2}");
        }

        return changes.Count == 0 ? "(none)" : string.Join(" ", changes);
    }

    private static string Describe()
    {
        var addon = (AddonLookingForGroup*)Plugin.GameGui.GetAddonByName("LookingForGroup").Address;
        var agent = AgentLookingForGroup.Instance();

        var page = addon == null ? "(no window)" : Text(addon->CurrentPageTextNode);
        var results = addon == null ? "(no window)" : Text(addon->ResultsCountTextNode);
        var agentInfo = agent == null
            ? "(no agent)"
            : $"displayed={agent->NumberOfListingsDisplayed} category={agent->CategoryTab} area={agent->SearchAreaTab}";

        return $"pageText=\"{page}\" resultsText=\"{results}\" {agentInfo}";
    }

    private static string Text(AtkTextNode* node) =>
        node == null ? "(null)" : SeString.Parse(node->NodeText.AsSpan()).TextValue.Replace("\n", "\\n");
}

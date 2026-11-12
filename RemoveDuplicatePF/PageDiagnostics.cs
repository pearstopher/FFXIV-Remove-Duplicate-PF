using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RemoveDuplicatePF;

/// <summary>
/// RESEARCH ONLY (#71), not for merging: logs the Party Finder window's page/range texts so
/// we can see whether they already show the new page when a result set starts arriving.
/// </summary>
internal static unsafe class PageDiagnostics
{
    private static string lastWindowState = string.Empty;

    /// <summary>Logs the window state at the moment a result set starts arriving.</summary>
    public static void LogResultSetStart(int resultSetNumber, int batchNumber)
    {
        Plugin.Log.Debug(
            "PF page diag: result set #{Set} STARTED (first batch={Batch}) | {State}",
            resultSetNumber,
            batchNumber,
            Describe());
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

    private static string Describe()
    {
        var addon = (AddonLookingForGroup*)Plugin.GameGui.GetAddonByName("LookingForGroup").Address;
        var agent = AgentLookingForGroup.Instance();

        var page = addon == null ? "(no window)" : Text(addon->CurrentPageTextNode);
        var results = addon == null ? "(no window)" : Text(addon->ResultsCountTextNode);
        var agentInfo = agent == null
            ? "(no agent)"
            : $"displayed={agent->NumberOfListingsDisplayed} category={agent->CategoryTab} area={agent->SearchAreaTab} firstId={agent->Listings.ListingIds[0]}";

        return $"pageText=\"{page}\" resultsText=\"{results}\" {agentInfo}";
    }

    private static string Text(AtkTextNode* node) =>
        node == null ? "(null)" : SeString.Parse(node->NodeText.AsSpan()).TextValue.Replace("\n", "\\n");
}

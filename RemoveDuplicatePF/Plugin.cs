using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Entry point. The plugin has no settings, windows or commands; it only
/// acts on the Party Finder listings while that window is open.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IPartyFinderGui PartyFinderGui { get; private set; } = null!;

#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    private readonly PartyFinderListener listener;

    public Plugin()
    {
        this.listener = new PartyFinderListener();

        // Logs which build is running, so a stale or wrong-configuration build is obvious in /xllog.
        var dll = PluginInterface.AssemblyLocation;
        var build = BuildInfo.Describe(BuildConfiguration, dll.LastWriteTime, dll.FullName);
        Log.Information($"{PluginInterface.Manifest.Name} loaded ({build})");
    }

    public void Dispose()
    {
        this.listener.Dispose();
        Log.Information($"{PluginInterface.Manifest.Name} unloaded");
    }
}

using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace RemoveDuplicatePF;

/// <summary>
/// Entry point. The plugin has no settings, windows or commands; it only
/// acts on the Party Finder listings while that window is open.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Plugin()
    {
        Log.Information($"{PluginInterface.Manifest.Name} loaded");
    }

    public void Dispose()
    {
        Log.Information($"{PluginInterface.Manifest.Name} unloaded");
    }
}

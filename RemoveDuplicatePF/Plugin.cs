using System;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF;

/// <summary>
/// Entry point. The plugin has no settings, windows or commands; it only
/// acts on the Party Finder listings while that window is open.
/// </summary>
/// <remarks>
/// Everything it registers, and nothing else:
/// <list type="bullet">
/// <item>one <c>IPartyFinderGui.ReceiveListing</c> handler (<see cref="PartyFinderListener"/>);</item>
/// <item>three <c>IAddonLifecycle</c> listeners on <c>LookingForGroup</c> each in
/// <see cref="ListingPrefixer"/> and <see cref="TitleUpdater"/>, and two (button clicks and
/// window open, read only) in <see cref="SearchEvents"/>.</item>
/// </list>
/// No hooks, slash commands, windows, or config/main UI callbacks. All of it is
/// released in <see cref="Dispose"/>, which also restores the window's text (#11).
/// </remarks>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IPartyFinderGui PartyFinderGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    /// <summary>
    /// Whether Debug messages are recorded (the <c>/xllog</c> level is Debug or lower). Dalamud's
    /// global logger applies that level, so text built only for the Debug log can be skipped when
    /// this is false.
    /// </summary>
    internal static bool DebugLogging => Serilog.Log.IsEnabled(Serilog.Events.LogEventLevel.Debug);

#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    private readonly PartyFinderListener listener;
    private readonly ListingPrefixer prefixer;
    private readonly TitleUpdater titleUpdater;
    private readonly SearchEvents searchEvents;

    public Plugin()
    {
        var filter = new ListingFilter();
        this.listener = new PartyFinderListener(filter);
        this.prefixer = new ListingPrefixer(filter);
        this.titleUpdater = new TitleUpdater(filter);
        this.searchEvents = new SearchEvents(filter);

        // Logs which build is running, so a stale or wrong-configuration build is obvious in /xllog.
        var dll = PluginInterface.AssemblyLocation;
        var build = BuildInfo.Describe(BuildConfiguration, BuildInfo.ReadBuildTime(dll.FullName), dll.FullName);
        Log.Information($"{PluginInterface.Manifest.Name} loaded ({build})");
    }

    public void Dispose()
    {
        // Stop changing listings first, then put the window's text back. Each part is disposed
        // even if an earlier one fails, so nothing is left registered (#11).
        DisposeSafely(this.listener, "Party Finder listener");
        DisposeSafely(this.prefixer, "listing prefixes");
        DisposeSafely(this.titleUpdater, "window title");
        DisposeSafely(this.searchEvents, "button listener");
        Log.Information($"{PluginInterface.Manifest.Name} unloaded");
    }

    private static void DisposeSafely(IDisposable part, string name)
    {
        try
        {
            part.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unloading {Part}", name);
        }
    }
}

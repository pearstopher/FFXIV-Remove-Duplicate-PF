using System.Globalization;

namespace RemoveDuplicatePF.Core;

/// <summary>
/// Formats the build details the plugin logs when it loads, so a stale build
/// (wrong configuration, or not rebuilt) is obvious in <c>/xllog</c>.
/// </summary>
public static class BuildInfo
{
    /// <summary>
    /// Returns text like <c>Debug build, built 2026-11-11 10:36:02, from C:\...\RemoveDuplicatePF.dll</c>.
    /// </summary>
    /// <param name="configuration">Build configuration, e.g. <c>Debug</c> or <c>Release</c>.</param>
    /// <param name="builtAt">When the DLL was written, in local time.</param>
    /// <param name="dllPath">Full path of the loaded DLL, if known.</param>
    public static string Describe(string configuration, DateTime builtAt, string? dllPath)
    {
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{configuration} build, built {builtAt:yyyy-MM-dd HH:mm:ss}");

        return string.IsNullOrWhiteSpace(dllPath) ? text : $"{text}, from {dllPath}";
    }
}

using System.Globalization;
using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class BuildInfoTests
{
    private static readonly DateTime BuiltAt = new(2026, 11, 11, 10, 36, 2);

    [Fact]
    public void Describes_configuration_time_and_path()
    {
        Assert.Equal(
            @"Debug build, built 2026-11-11 10:36:02, from C:\repo\RemoveDuplicatePF\bin\x64\Debug\RemoveDuplicatePF.dll",
            BuildInfo.Describe("Debug", BuiltAt, @"C:\repo\RemoveDuplicatePF\bin\x64\Debug\RemoveDuplicatePF.dll"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Omits_the_path_when_unknown(string? path)
    {
        Assert.Equal("Release build, built 2026-11-11 10:36:02", BuildInfo.Describe("Release", BuiltAt, path));
    }

    [Fact]
    public void Build_time_is_read_fresh_after_the_file_changes()
    {
        // Regression (#38): a reused FileInfo kept reporting the first build's time after a hot reload.
        var path = Path.GetTempFileName();
        try
        {
            var first = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Local);
            var second = new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Local);

            File.SetLastWriteTime(path, first);
            var cached = new FileInfo(path);
            Assert.Equal(first, cached.LastWriteTime);

            File.SetLastWriteTime(path, second);

            Assert.Equal(first, cached.LastWriteTime); // what the plugin used to read
            Assert.Equal(second, BuildInfo.ReadBuildTime(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Time_format_does_not_depend_on_the_current_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            Assert.Equal("Debug build, built 2026-11-11 10:36:02", BuildInfo.Describe("Debug", BuiltAt, null));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}

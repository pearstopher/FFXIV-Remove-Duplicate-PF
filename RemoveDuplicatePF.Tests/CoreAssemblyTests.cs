using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class CoreAssemblyTests
{
    // Core must stay testable without the game: no Dalamud, game structs or Lumina.
    [Theory]
    [InlineData("Dalamud")]
    [InlineData("FFXIVClientStructs")]
    [InlineData("Lumina")]
    [InlineData("InteropGenerator")]
    public void Core_does_not_reference_game_assemblies(string forbiddenPrefix)
    {
        var references = typeof(CoreAssembly).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, r => r.Name!.StartsWith(forbiddenPrefix, StringComparison.Ordinal));
    }
}

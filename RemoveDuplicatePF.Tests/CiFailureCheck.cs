namespace RemoveDuplicatePF.Tests;

// TEMPORARY: proves CI fails on a failing test. Removed before merge.
public class CiFailureCheck
{
    [Fact]
    public void Deliberately_fails() => Assert.Fail("Deliberate failure to verify CI goes red.");
}

using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class CountPrefixTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void No_prefix_for_groups_of_one_or_less(int groupSize)
    {
        Assert.Null(CountPrefix.For(groupSize));
    }

    [Theory]
    [InlineData(2, "[2] ")]
    [InlineData(3, "[3] ")]
    [InlineData(10, "[10] ")]
    [InlineData(50, "[50] ")]
    [InlineData(1234, "[1234] ")]
    [InlineData(int.MaxValue, "[2147483647] ")]
    public void Prefix_text_shows_the_group_size(int groupSize, string expected)
    {
        Assert.Equal(expected, CountPrefix.For(groupSize)!.Text);
    }

    [Fact]
    public void Prefix_uses_the_default_colour()
    {
        Assert.Equal(CountPrefix.DefaultColorId, CountPrefix.For(2)!.ColorId);
    }

    [Fact]
    public void Prefix_colour_is_not_the_plain_text_colour()
    {
        // UIColor row 0 means "no colour override" (regular text).
        Assert.NotEqual(0, CountPrefix.DefaultColorId);
    }

    [Fact]
    public void Prefix_works_with_tracker_group_sizes()
    {
        var tracker = new DuplicateTracker();
        tracker.Observe(1, "Venue");
        tracker.Observe(2, "Venue");
        tracker.Observe(3, "Venue");
        tracker.Observe(4, "Solo");

        Assert.Equal("[3] ", CountPrefix.For(tracker.GroupSize(1))?.Text);
        Assert.Null(CountPrefix.For(tracker.GroupSize(4)));
        Assert.Null(CountPrefix.For(tracker.GroupSize(2))); // hidden duplicate
    }
}

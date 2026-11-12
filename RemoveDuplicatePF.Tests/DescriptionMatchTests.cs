using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class DescriptionMatchTests
{
    private readonly DuplicateTracker tracker = new();

    private static readonly string Thirty = new('x', 30);

    [Fact]
    public void Match_length_is_30()
    {
        Assert.Equal(30, DescriptionMatch.Length);
    }

    [Theory]
    [InlineData("Short", "Short")]
    [InlineData("", "")]
    [InlineData("Come to my venue tonight! Door", "Come to my venue tonight! Door")]
    [InlineData("Come to my venue tonight! Doors 9pm ST", "Come to my venue tonight! Door")]
    public void Key_is_the_first_30_characters_or_the_whole_text(string description, string expected)
    {
        Assert.Equal(expected, DescriptionMatch.Key(description));
    }

    [Fact]
    public void Long_descriptions_with_the_same_first_30_characters_are_combined()
    {
        Assert.True(this.tracker.Observe(1, "Come to my venue tonight! Doors 9pm ST"));
        Assert.False(this.tracker.Observe(2, "Come to my venue tonight! Doors 10pm ST"));
        Assert.False(this.tracker.Observe(3, "Come to my venue tonight! Door prizes!!"));

        Assert.Equal(3, this.tracker.GroupSize(1));
    }

    [Fact]
    public void A_difference_inside_the_first_30_characters_keeps_listings_separate()
    {
        Assert.True(this.tracker.Observe(1, "Come to my venue tonight! Doors 9pm ST"));
        Assert.True(this.tracker.Observe(2, "Come to my VENUE tonight! Doors 9pm ST"));

        Assert.Equal(1, this.tracker.GroupSize(1));
    }

    [Fact]
    public void Descriptions_shorter_than_30_characters_must_match_exactly()
    {
        // The shorter text is a prefix of the longer one, but its key is the whole (shorter) text.
        Assert.True(this.tracker.Observe(1, "Join our FC!"));
        Assert.True(this.tracker.Observe(2, "Join our FC! Discord: abcdefghij"));
        Assert.True(this.tracker.Observe(3, "Join our FC! "));
        Assert.False(this.tracker.Observe(4, "Join our FC!"));

        Assert.Equal(2, this.tracker.GroupSize(1));
        Assert.Equal(1, this.tracker.GroupSize(2));
        Assert.Equal(1, this.tracker.GroupSize(3));
    }

    [Fact]
    public void Exactly_30_characters_matches_a_longer_description_with_the_same_start()
    {
        Assert.True(this.tracker.Observe(1, Thirty));
        Assert.False(this.tracker.Observe(2, Thirty + " and more"));

        Assert.Equal(2, this.tracker.GroupSize(1));
    }

    [Fact]
    public void Groups_on_screen_use_the_kept_listings_full_description()
    {
        var filter = new ListingFilter();
        filter.Decide(1, 1, "Come to my venue tonight! Doors 9pm ST", true);
        filter.Decide(0, 2, "Come to my venue tonight! Doors 10pm ST", true);

        var groups = filter.ForDisplayed(Array.Empty<ulong>()).Prefixes;

        Assert.Equal(new Dictionary<string, int> { ["Come to my venue tonight! Doors 9pm ST"] = 2 }, groups);
        Assert.Equal("[2] ", RowPrefixMatcher.Decide("Come to my venue tonight! Doors 9pm ST", groups)?.Prefix.Text);
    }

    [Fact]
    public void Whitespace_only_descriptions_are_still_never_combined()
    {
        var spaces = new string(' ', 40);

        Assert.True(this.tracker.Observe(1, spaces));
        Assert.True(this.tracker.Observe(2, spaces));
        Assert.Equal(0, this.tracker.GroupSize(1));
    }
}

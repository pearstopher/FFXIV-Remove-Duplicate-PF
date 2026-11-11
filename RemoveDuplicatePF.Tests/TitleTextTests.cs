using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class TitleTextTests
{
    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, null)]
    [InlineData(1, " (1 duplicate combined)")]
    [InlineData(2, " (2 duplicates combined)")]
    [InlineData(12, " (12 duplicates combined)")]
    [InlineData(1234, " (1234 duplicates combined)")]
    public void Suffix_reflects_the_count(int hidden, string? expected)
    {
        Assert.Equal(expected, TitleText.Suffix(hidden));
    }

    [Fact]
    public void Title_shows_the_count_after_the_games_title()
    {
        Assert.Equal("Party Finder (12 duplicates combined)", TitleText.Compose("Party Finder", 12));
    }

    [Fact]
    public void Title_is_unchanged_when_nothing_is_combined()
    {
        Assert.Equal("Party Finder", TitleText.Compose("Party Finder", 0));
    }

    [Fact]
    public void Updating_the_count_replaces_the_old_suffix_instead_of_stacking()
    {
        var first = TitleText.Compose("Party Finder", 3);

        Assert.Equal("Party Finder (8 duplicates combined)", TitleText.Compose(first, 8));
        Assert.Equal("Party Finder (1 duplicate combined)", TitleText.Compose(first, 1));
        Assert.Equal("Party Finder", TitleText.Compose(first, 0));
    }

    [Theory]
    [InlineData("Party Finder (12 duplicates combined)", "Party Finder")]
    [InlineData("Party Finder (1 duplicate combined)", "Party Finder")]
    [InlineData("Party Finder", "Party Finder")]
    [InlineData("", "")]
    public void Original_title_is_recovered(string current, string expected)
    {
        Assert.Equal(expected, TitleText.Original(current));
    }

    [Theory]
    [InlineData("Party Finder (beta)")]
    [InlineData("Party Finder (12 duplicates combined) extra")]
    [InlineData("Party Finder (twelve duplicates combined)")]
    public void Other_text_in_brackets_is_not_treated_as_the_plugins_suffix(string title)
    {
        Assert.Equal(title, TitleText.Original(title));
    }

    [Fact]
    public void Works_with_the_trackers_hidden_count()
    {
        var tracker = new DuplicateTracker();
        tracker.Observe(1, "Venue");
        tracker.Observe(2, "Venue");
        tracker.Observe(3, "Venue");

        Assert.Equal("Party Finder (2 duplicates combined)", TitleText.Compose("Party Finder", tracker.HiddenCount));
    }
}

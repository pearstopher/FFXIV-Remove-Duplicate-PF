using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class TitleTextTests
{
    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, null)]
    [InlineData(1, null)] // a duplicate group always has at least 2 listings
    [InlineData(2, " (2 duplicates)")]
    [InlineData(7, " (7 duplicates)")]
    [InlineData(1234, " (1234 duplicates)")]
    public void Suffix_reflects_the_total(int total, string? expected)
    {
        Assert.Equal(expected, TitleText.Suffix(total));
    }

    [Fact]
    public void Title_shows_the_total_after_the_games_title()
    {
        Assert.Equal("Party Finder (7 duplicates)", TitleText.Compose("Party Finder", 7));
    }

    [Fact]
    public void Title_is_unchanged_when_there_are_no_duplicates()
    {
        Assert.Equal("Party Finder", TitleText.Compose("Party Finder", 0));
    }

    [Fact]
    public void Updating_the_total_replaces_the_old_suffix_instead_of_stacking()
    {
        var first = TitleText.Compose("Party Finder", 3);

        Assert.Equal("Party Finder (8 duplicates)", TitleText.Compose(first, 8));
        Assert.Equal("Party Finder", TitleText.Compose(first, 0));
    }

    [Theory]
    [InlineData("Party Finder (7 duplicates)", "Party Finder")]
    [InlineData("Party Finder (12 duplicates combined)", "Party Finder")] // v1.0.0 wording
    [InlineData("Party Finder (1 duplicate combined)", "Party Finder")]   // v1.0.0 wording
    [InlineData("Party Finder", "Party Finder")]
    [InlineData("", "")]
    public void Original_title_is_recovered(string current, string expected)
    {
        Assert.Equal(expected, TitleText.Original(current));
    }

    [Fact]
    public void Old_wording_is_replaced_not_stacked()
    {
        Assert.Equal("Party Finder (7 duplicates)", TitleText.Compose("Party Finder (4 duplicates combined)", 7));
    }

    [Theory]
    [InlineData("Party Finder (beta)")]
    [InlineData("Party Finder (12 duplicates) extra")]
    [InlineData("Party Finder (twelve duplicates)")]
    public void Other_text_in_brackets_is_not_treated_as_the_plugins_suffix(string title)
    {
        Assert.Equal(title, TitleText.Original(title));
    }

    [Fact]
    public void Title_total_equals_the_sum_of_the_prefixes()
    {
        // Owner's example: [3] [2] [2] on screen -> 7 (previously "4 duplicates combined").
        var tracker = new DuplicateTracker();
        var listings = new (ulong Id, string Text)[]
        {
            (1, "Venue A"), (2, "Venue A"), (3, "Venue A"),
            (4, "Venue B"), (5, "Venue B"),
            (6, "Venue C"), (7, "Venue C"),
            (8, "Solo listing"),
        };
        foreach (var (id, text) in listings)
            tracker.Observe(id, text);

        var prefixTotal = tracker.DuplicateGroupsByDescription.Values.Sum();

        Assert.Equal(7, prefixTotal);
        Assert.Equal(prefixTotal, tracker.DuplicateListingCount);
        Assert.Equal(tracker.HiddenCount + tracker.DuplicateGroupCount, tracker.DuplicateListingCount);
        Assert.Equal("Party Finder (7 duplicates)", TitleText.Compose("Party Finder", tracker.DuplicateListingCount));
    }

    [Fact]
    public void No_duplicates_means_no_suffix()
    {
        var tracker = new DuplicateTracker();
        tracker.Observe(1, "A");
        tracker.Observe(2, "B");

        Assert.Equal(0, tracker.DuplicateListingCount);
        Assert.Equal("Party Finder", TitleText.Compose("Party Finder", tracker.DuplicateListingCount));
    }
}

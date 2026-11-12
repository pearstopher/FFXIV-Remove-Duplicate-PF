using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class TitleTextTests
{
    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, null)]
    [InlineData(1, " (1 duplicate hidden)")]
    [InlineData(2, " (2 duplicates hidden)")]
    [InlineData(7, " (7 duplicates hidden)")]
    [InlineData(1234, " (1234 duplicates hidden)")]
    public void Suffix_reflects_the_hidden_count(int hidden, string? expected)
    {
        Assert.Equal(expected, TitleText.Suffix(hidden));
    }

    [Fact]
    public void Title_shows_the_hidden_count_after_the_games_title()
    {
        Assert.Equal("Party Finder (7 duplicates hidden)", TitleText.Compose("Party Finder", 7));
    }

    [Fact]
    public void Title_is_unchanged_when_nothing_is_hidden()
    {
        Assert.Equal("Party Finder", TitleText.Compose("Party Finder", 0));
    }

    [Fact]
    public void Updating_the_count_replaces_the_old_suffix_instead_of_stacking()
    {
        var first = TitleText.Compose("Party Finder", 3);

        Assert.Equal("Party Finder (8 duplicates hidden)", TitleText.Compose(first, 8));
        Assert.Equal("Party Finder (1 duplicate hidden)", TitleText.Compose(first, 1));
        Assert.Equal("Party Finder", TitleText.Compose(first, 0));
    }

    [Theory]
    [InlineData("Party Finder (7 duplicates hidden)", "Party Finder")]
    [InlineData("Party Finder (1 duplicate hidden)", "Party Finder")]
    [InlineData("Party Finder (7 duplicates)", "Party Finder")]           // v1.1.x wording
    [InlineData("Party Finder (12 duplicates combined)", "Party Finder")] // v1.0.0 wording
    [InlineData("Party Finder (1 duplicate combined)", "Party Finder")]   // v1.0.0 wording
    [InlineData("Party Finder", "Party Finder")]
    [InlineData("", "")]
    public void Original_title_is_recovered(string current, string expected)
    {
        Assert.Equal(expected, TitleText.Original(current));
    }

    [Theory]
    [InlineData("Party Finder (4 duplicates combined)")]
    [InlineData("Party Finder (9 duplicates)")]
    public void Older_wording_is_replaced_not_stacked(string oldTitle)
    {
        Assert.Equal("Party Finder (7 duplicates hidden)", TitleText.Compose(oldTitle, 7));
    }

    [Theory]
    [InlineData("Party Finder (beta)")]
    [InlineData("Party Finder (12 duplicates hidden) extra")]
    [InlineData("Party Finder (twelve duplicates hidden)")]
    public void Other_text_in_brackets_is_not_treated_as_the_plugins_suffix(string title)
    {
        Assert.Equal(title, TitleText.Original(title));
    }

    [Fact]
    public void Title_counts_hidden_listings_not_the_visible_copies()
    {
        // [3] [2] [2] on the page: 2 + 1 + 1 extra copies hidden (the 3 visible copies aren't counted).
        var filter = new ListingFilter();
        var listings = new (ulong Id, string Text)[]
        {
            (1, "Venue A"), (2, "Venue A"), (3, "Venue A"),
            (4, "Venue B"), (5, "Venue B"),
            (6, "Venue C"), (7, "Venue C"),
            (8, "Solo listing"),
        };
        foreach (var (id, text) in listings)
            filter.Decide(id <= 4 ? 1 : 0, id, text, true); // segment 1 (4 listings), then the final segment 0

        var shown = filter.ForDisplayed(Array.Empty<ulong>());

        Assert.Equal(4, shown.HiddenTotal);
        Assert.Equal("Party Finder (4 duplicates hidden)", TitleText.Compose("Party Finder", shown.HiddenTotal));
    }

    [Fact]
    public void No_duplicates_means_no_suffix()
    {
        var filter = new ListingFilter();
        filter.Decide(1, 1, "A", true);
        filter.Decide(1, 2, "B", true);

        var shown = filter.ForDisplayed(Array.Empty<ulong>());

        Assert.Equal(0, shown.HiddenTotal);
        Assert.Equal("Party Finder", TitleText.Compose("Party Finder", shown.HiddenTotal));
    }
}

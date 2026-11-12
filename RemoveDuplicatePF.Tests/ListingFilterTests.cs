using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>
/// Basic <see cref="ListingFilter.Decide"/> behaviour. No window open or button clicks are noted
/// here, so every result set starts a new search (see <see cref="SearchStart"/>); searches that
/// span several pages are covered by <see cref="SearchWideTests"/>.
/// </summary>
public class ListingFilterTests
{
    private readonly ListingFilter filter = new();

    // Listings hidden in the newest result set.
    private int Hidden => this.filter.NewestSummary.Hidden;

    [Fact]
    public void First_listing_shown_and_later_duplicates_hidden()
    {
        Assert.True(this.filter.Decide(1, 1, "Venue", currentlyVisible: true));
        Assert.False(this.filter.Decide(1, 2, "Venue", currentlyVisible: true));
        Assert.True(this.filter.Decide(1, 3, "Other", currentlyVisible: true));

        Assert.Equal(1, this.Hidden);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Listings_without_a_description_are_never_hidden(string? description)
    {
        Assert.True(this.filter.Decide(1, 1, description, currentlyVisible: true));
        Assert.True(this.filter.Decide(1, 2, description, currentlyVisible: true));
    }

    [Fact]
    public void Never_unhides_a_listing_another_plugin_hid()
    {
        Assert.False(this.filter.Decide(1, 1, "Unique", currentlyVisible: false));
        Assert.False(this.filter.Decide(1, 2, "", currentlyVisible: false));
    }

    [Fact]
    public void Listing_hidden_by_another_plugin_is_not_counted_or_kept()
    {
        // Another plugin hid the first copy, so the next visible copy is kept.
        Assert.False(this.filter.Decide(1, 1, "Venue", currentlyVisible: false));
        Assert.True(this.filter.Decide(1, 2, "Venue", currentlyVisible: true));
        Assert.False(this.filter.Decide(1, 3, "Venue", currentlyVisible: true));

        Assert.Equal(0, this.filter.Search.GroupSize(1));
        Assert.Equal(2, this.filter.Search.GroupSize(2));
        Assert.Equal(1, this.Hidden);
        Assert.Equal(1, this.filter.NewestSummary.HiddenByOthers);
    }

    [Fact]
    public void Refresh_does_not_carry_over_old_counts()
    {
        this.filter.Decide(1, 1, "Venue", true);
        this.filter.Decide(1, 2, "Venue", true);
        this.filter.Decide(0, 3, "Venue", true);
        Assert.Equal(2, this.Hidden);

        // Refresh: the same listings come back, starting at segment 1.
        Assert.True(this.filter.Decide(1, 1, "Venue", true));
        Assert.Equal(0, this.Hidden);
        Assert.Equal(1, this.filter.Search.GroupSize(1));
    }

    [Fact]
    public void After_a_refresh_a_listing_hidden_before_can_become_the_kept_one()
    {
        // Before: 1 kept, 2 hidden. After a refresh where 2 is listed first, 2 is kept.
        this.filter.Decide(1, 1, "Venue", true);
        this.filter.Decide(1, 2, "Venue", true);

        Assert.True(this.filter.Decide(1, 2, "Venue", true));
        Assert.False(this.filter.Decide(1, 1, "Venue", true));
    }

    [Fact]
    public void Without_any_clicks_each_result_set_is_a_new_search()
    {
        // Clicks may not be reaching the plugin, so nothing carries over between result sets (#80).
        this.filter.Decide(1, 1, "Venue", true);
        this.filter.Decide(0, 2, "Venue", true);

        Assert.True(this.filter.Decide(1, 51, "Venue", true));
        Assert.Equal(0, this.Hidden);
        Assert.True(this.filter.NewestSummary.NewSearch);
    }

    [Fact]
    public void Listings_hidden_by_other_plugins_still_mark_result_set_boundaries()
    {
        this.filter.Decide(1, 1, "Venue", true);
        this.filter.Decide(0, 2, "Venue", true);

        // New set starts with a listing another plugin hid; tracking still resets.
        Assert.False(this.filter.Decide(1, 9, "Other", currentlyVisible: false));
        Assert.Equal(0, this.Hidden);
        Assert.True(this.filter.Decide(1, 2, "Venue", true));
    }
}

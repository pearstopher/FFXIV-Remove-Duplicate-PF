using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class ListingFilterTests
{
    private readonly ListingFilter filter = new();

    [Fact]
    public void First_listing_shown_and_later_duplicates_hidden()
    {
        Assert.True(this.filter.Decide(1, "Venue", currentlyVisible: true));
        Assert.False(this.filter.Decide(2, "Venue", currentlyVisible: true));
        Assert.True(this.filter.Decide(3, "Other", currentlyVisible: true));

        Assert.Equal(1, this.filter.Tracker.HiddenCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Listings_without_a_description_are_never_hidden(string? description)
    {
        Assert.True(this.filter.Decide(1, description, currentlyVisible: true));
        Assert.True(this.filter.Decide(2, description, currentlyVisible: true));
    }

    [Fact]
    public void Never_unhides_a_listing_another_plugin_hid()
    {
        Assert.False(this.filter.Decide(1, "Unique", currentlyVisible: false));
        Assert.False(this.filter.Decide(2, "", currentlyVisible: false));
    }

    [Fact]
    public void Listing_hidden_by_another_plugin_is_not_counted_or_kept()
    {
        // Another plugin hid the first copy, so the next visible copy is kept.
        Assert.False(this.filter.Decide(1, "Venue", currentlyVisible: false));
        Assert.True(this.filter.Decide(2, "Venue", currentlyVisible: true));
        Assert.False(this.filter.Decide(3, "Venue", currentlyVisible: true));

        Assert.Equal(0, this.filter.Tracker.GroupSize(1));
        Assert.Equal(2, this.filter.Tracker.GroupSize(2));
        Assert.Equal(1, this.filter.Tracker.HiddenCount);
    }
}

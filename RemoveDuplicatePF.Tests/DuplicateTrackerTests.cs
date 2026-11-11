using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class DuplicateTrackerTests
{
    private readonly DuplicateTracker tracker = new();

    [Fact]
    public void Single_listing_is_shown_and_not_counted()
    {
        Assert.True(this.tracker.Observe(1, "Come to my venue"));

        Assert.Equal(1, this.tracker.GroupSize(1));
        Assert.Equal(0, this.tracker.HiddenCount);
        Assert.Equal(0, this.tracker.DuplicateGroupCount);
        Assert.Empty(this.tracker.DuplicateGroups);
    }

    [Fact]
    public void Exact_duplicates_are_grouped_and_later_ones_hidden()
    {
        Assert.True(this.tracker.Observe(1, "Come to my venue"));
        Assert.False(this.tracker.Observe(2, "Come to my venue"));
        Assert.False(this.tracker.Observe(3, "Come to my venue"));

        Assert.Equal(3, this.tracker.GroupSize(1));
        Assert.Equal(2, this.tracker.HiddenCount);
        Assert.Equal(1, this.tracker.DuplicateGroupCount);
    }

    [Fact]
    public void First_listing_in_display_order_wins_regardless_of_id()
    {
        // A higher ID arriving first is still the one kept.
        Assert.True(this.tracker.Observe(900, "Venue"));
        Assert.False(this.tracker.Observe(5, "Venue"));

        Assert.Equal(2, this.tracker.GroupSize(900));
        Assert.Equal(0, this.tracker.GroupSize(5));
    }

    [Fact]
    public void Mixed_set_tracks_each_group_separately()
    {
        var shown = new[]
        {
            this.tracker.Observe(1, "A"),
            this.tracker.Observe(2, "B"),
            this.tracker.Observe(3, "A"),
            this.tracker.Observe(4, "C"),
            this.tracker.Observe(5, "B"),
            this.tracker.Observe(6, "A"),
        };

        Assert.Equal(new[] { true, true, false, true, false, false }, shown);
        Assert.Equal(3, this.tracker.GroupSize(1));
        Assert.Equal(2, this.tracker.GroupSize(2));
        Assert.Equal(1, this.tracker.GroupSize(4));
        Assert.Equal(3, this.tracker.HiddenCount);
        Assert.Equal(2, this.tracker.DuplicateGroupCount);
        Assert.Equal(new Dictionary<ulong, int> { [1] = 3, [2] = 2 }, this.tracker.DuplicateGroups);
    }

    [Theory]
    [InlineData("Venue", "venue")]
    [InlineData("Venue", "Venue ")]
    [InlineData("Venue", " Venue")]
    [InlineData("Come to my venue", "Come to my  venue")]
    public void Only_exact_text_matches(string first, string second)
    {
        Assert.True(this.tracker.Observe(1, first));
        Assert.True(this.tracker.Observe(2, second));

        Assert.Equal(0, this.tracker.HiddenCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n ")]
    public void Empty_or_whitespace_descriptions_are_always_shown_and_never_counted(string? description)
    {
        Assert.True(this.tracker.Observe(1, description));
        Assert.True(this.tracker.Observe(2, description));
        Assert.True(this.tracker.Observe(3, description));

        Assert.Equal(0, this.tracker.GroupSize(1));
        Assert.Equal(0, this.tracker.HiddenCount);
        Assert.Equal(0, this.tracker.DuplicateGroupCount);
    }

    [Fact]
    public void Observing_the_same_listing_again_keeps_its_decision_and_counts()
    {
        this.tracker.Observe(1, "Venue");
        this.tracker.Observe(2, "Venue");

        Assert.True(this.tracker.Observe(1, "Venue"));
        Assert.False(this.tracker.Observe(2, "Venue"));
        Assert.Equal(2, this.tracker.GroupSize(1));
        Assert.Equal(1, this.tracker.HiddenCount);
    }

    [Fact]
    public void Duplicate_groups_by_description_lists_only_groups_with_duplicates()
    {
        this.tracker.Observe(1, "A");
        this.tracker.Observe(2, "A");
        this.tracker.Observe(3, "B");
        this.tracker.Observe(4, "");

        Assert.Equal(new Dictionary<string, int> { ["A"] = 2 }, this.tracker.DuplicateGroupsByDescription);

        this.tracker.Reset();
        Assert.Empty(this.tracker.DuplicateGroupsByDescription);
    }

    [Fact]
    public void Unknown_listing_has_group_size_zero()
    {
        Assert.Equal(0, this.tracker.GroupSize(42));
    }

    [Fact]
    public void Reset_clears_all_state()
    {
        this.tracker.Observe(1, "Venue");
        this.tracker.Observe(2, "Venue");

        this.tracker.Reset();

        Assert.Equal(0, this.tracker.HiddenCount);
        Assert.Equal(0, this.tracker.DuplicateGroupCount);
        Assert.Equal(0, this.tracker.GroupSize(1));
        Assert.Empty(this.tracker.DuplicateGroups);

        // After a reset the same listings start fresh: the old duplicate is now first.
        Assert.True(this.tracker.Observe(2, "Venue"));
        Assert.False(this.tracker.Observe(1, "Venue"));
        Assert.Equal(2, this.tracker.GroupSize(2));
    }
}

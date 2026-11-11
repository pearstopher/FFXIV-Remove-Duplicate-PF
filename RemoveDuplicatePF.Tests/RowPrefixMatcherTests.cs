using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class RowPrefixMatcherTests
{
    private static readonly Dictionary<string, int> Groups = new(StringComparer.Ordinal)
    {
        ["Come to my venue"] = 3,
        ["Weekly static LF healer"] = 2,
    };

    [Fact]
    public void Exact_description_gets_the_prefix()
    {
        var d = RowPrefixMatcher.Decide("Come to my venue", Groups);

        Assert.NotNull(d);
        Assert.Equal("Come to my venue", d.Description);
        Assert.Equal("[3] ", d.Prefix.Text);
        Assert.Equal(RowPrefixState.Original, d.State);
    }

    [Theory]
    [InlineData("Some other listing")]
    [InlineData("Come to my venue!")]
    [InlineData("come to my venue")]
    [InlineData("Free stuff: Come to my venue")]
    [InlineData("")]
    [InlineData("   ")]
    public void Rows_that_are_not_a_duplicate_group_are_left_alone(string row)
    {
        Assert.Null(RowPrefixMatcher.Decide(row, Groups));
    }

    [Fact]
    public void Nothing_matches_when_there_are_no_groups()
    {
        Assert.Null(RowPrefixMatcher.Decide("Come to my venue", new Dictionary<string, int>()));
    }

    [Fact]
    public void Group_of_one_gets_no_prefix()
    {
        var single = new Dictionary<string, int> { ["Solo"] = 1 };

        Assert.Null(RowPrefixMatcher.Decide("Solo", single));
    }

    [Theory]
    [InlineData("[Practice] Come to my venue")]
    [InlineData("[Loot][Practice] Come to my venue")]
    [InlineData("[Duty Completion]  Come to my venue")]
    public void Description_after_game_tags_matches(string row)
    {
        var d = RowPrefixMatcher.Decide(row, Groups);

        Assert.Equal("Come to my venue", d?.Description);
        Assert.Equal(RowPrefixState.Original, d?.State);
    }

    [Fact]
    public void Already_prefixed_row_with_the_right_count_needs_nothing()
    {
        var d = RowPrefixMatcher.Decide("[3] Come to my venue", Groups);

        Assert.Equal(RowPrefixState.Prefixed, d?.State);
    }

    [Fact]
    public void Already_prefixed_row_with_game_tags_needs_nothing()
    {
        var d = RowPrefixMatcher.Decide("[3] [Practice] Come to my venue", Groups);

        Assert.Equal(RowPrefixState.Prefixed, d?.State);
    }

    [Theory]
    [InlineData("Come to my venue")]
    [InlineData("[Practice] Come to my venue")]
    [InlineData("Come to my…")]
    public void Prefix_is_applied_once_and_not_stacked_on_later_scans(string row)
    {
        // Regression: "[3] ..." was read as a game tag and prefixed again on every scan.
        var first = RowPrefixMatcher.Decide(row, Groups);
        Assert.Equal(RowPrefixState.Original, first?.State);

        var afterPrefix = first!.Prefix.Text + row;
        Assert.Equal(RowPrefixState.Prefixed, RowPrefixMatcher.Decide(afterPrefix, Groups)?.State);
    }

    [Fact]
    public void Prefix_with_an_old_count_is_stale()
    {
        // More copies arrived after the row was first prefixed.
        var d = RowPrefixMatcher.Decide("[2] Come to my venue", Groups);

        Assert.Equal(RowPrefixState.StalePrefix, d?.State);
        Assert.Equal("[3] ", d?.Prefix.Text);
    }

    [Fact]
    public void Description_that_really_starts_with_brackets_matches_exactly_first()
    {
        var groups = new Dictionary<string, int> { ["[2] Raid night"] = 4 };

        var d = RowPrefixMatcher.Decide("[2] Raid night", groups);

        Assert.Equal(RowPrefixState.Original, d?.State);
        Assert.Equal("[4] ", d?.Prefix.Text);
    }

    [Theory]
    [InlineData("Come to my ...")]
    [InlineData("Come to my…")]
    [InlineData("[Practice] Come to...")]
    public void Shortened_text_matches_when_only_one_group_fits(string row)
    {
        Assert.Equal("Come to my venue", RowPrefixMatcher.Decide(row, Groups)?.Description);
    }

    [Fact]
    public void Shortened_text_is_ignored_when_ambiguous()
    {
        var groups = new Dictionary<string, int> { ["Come to my venue"] = 2, ["Come to my party"] = 2 };

        Assert.Null(RowPrefixMatcher.Decide("Come to my...", groups));
    }

    [Theory]
    [InlineData("...")]
    [InlineData("…")]
    public void Ellipsis_alone_matches_nothing(string row)
    {
        Assert.Null(RowPrefixMatcher.Decide(row, Groups));
    }

    [Fact]
    public void Description_behind_own_prefix_is_found_for_restoring()
    {
        var known = new[] { "Come to my venue", "Other" };

        Assert.Equal("Come to my venue", RowPrefixMatcher.DescriptionBehindOwnPrefix("[3] Come to my venue", known));
        Assert.Equal("Come to my venue", RowPrefixMatcher.DescriptionBehindOwnPrefix("[3] [Practice] Come to my venue", known));
        Assert.Null(RowPrefixMatcher.DescriptionBehindOwnPrefix("Come to my venue", known));
        Assert.Null(RowPrefixMatcher.DescriptionBehindOwnPrefix("[3] Unknown", known));
    }

    [Fact]
    public void Works_with_tracker_groups()
    {
        var tracker = new DuplicateTracker();
        tracker.Observe(1, "Venue");
        tracker.Observe(2, "Venue");
        tracker.Observe(3, "Solo");

        var groups = tracker.DuplicateGroupsByDescription;

        Assert.Equal("[2] ", RowPrefixMatcher.Decide("Venue", groups)?.Prefix.Text);
        Assert.Null(RowPrefixMatcher.Decide("Solo", groups));
    }
}

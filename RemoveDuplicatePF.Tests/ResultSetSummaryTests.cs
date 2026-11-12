using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class ResultSetSummaryTests
{
    private readonly ListingFilter filter = new();

    [Fact]
    public void Summary_counts_listings_duplicates_and_other_plugins()
    {
        this.filter.Decide(1, 1, "Come to my venue tonight! Doors 9pm ST", true);
        this.filter.Decide(1, 2, "Come to my venue tonight! Doors 10pm ST", true);
        this.filter.Decide(1, 3, "Come to my venue tonight! Door prizes", true);
        this.filter.Decide(1, 4, "Dancers wanted", true);
        this.filter.Decide(2, 5, "Dancers wanted", true);
        this.filter.Decide(2, 6, "Unique listing", true);
        this.filter.Decide(0, 7, "Spam ad", currentlyVisible: false);

        Assert.Equal(
            "result set #1 (new search): 7 listings, 3 duplicates hidden, 1 hidden by other plugins | \"Come to my venue tonight! Door\" ×3, \"Dancers wanted\" ×2",
            this.filter.NewestSummary.Describe());
    }

    [Fact]
    public void Summary_without_duplicates_has_no_group_list()
    {
        this.filter.Decide(1, 1, "A", true);
        this.filter.Decide(0, 2, "B", true);

        Assert.Equal(
            "result set #1 (new search): 2 listings, 0 duplicates hidden, 0 hidden by other plugins",
            this.filter.NewestSummary.Describe());
    }

    [Fact]
    public void Summary_names_groups_by_match_key_not_full_text()
    {
        var longText = "Come to my venue tonight! Doors 9pm ST, free drinks, live DJ, all welcome";
        this.filter.Decide(1, 1, longText, true);
        this.filter.Decide(0, 2, longText, true);

        var summary = this.filter.NewestSummary.Describe();

        Assert.Contains("\"Come to my venue tonight! Door\" ×2", summary);
        Assert.DoesNotContain("free drinks", summary);
    }

    [Fact]
    public void Summary_follows_the_newest_result_set()
    {
        this.filter.Decide(1, 1, "A", true);
        this.filter.Decide(0, 2, "A", true);
        this.filter.Decide(1, 3, "B", true);

        var summary = this.filter.NewestSummary;

        Assert.Equal(2, summary.ResultSetNumber);
        Assert.Equal(1, summary.Received);
        Assert.Equal(0, summary.Hidden);
    }

    [Fact]
    public void Groups_are_listed_largest_first_then_by_key()
    {
        // Real segments hold at most 4 listings: 4 in segment 1, then 3 in the final segment 0.
        foreach (var (id, text) in new (ulong, string)[] { (1, "B"), (2, "B"), (3, "A"), (4, "A") })
            this.filter.Decide(1, id, text, true);
        foreach (var (id, text) in new (ulong, string)[] { (5, "C"), (6, "C"), (7, "C") })
            this.filter.Decide(0, id, text, true);

        Assert.EndsWith("| \"C\" ×3, \"A\" ×2, \"B\" ×2", this.filter.NewestSummary.Describe());
    }
}

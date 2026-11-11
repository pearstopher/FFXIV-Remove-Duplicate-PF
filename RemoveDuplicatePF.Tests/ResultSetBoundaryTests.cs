using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class ResultSetBoundaryTests
{
    private readonly ResultSetBoundary boundary = new();

    // Feeds (batch, id) pairs and returns which ones started a new result set.
    private bool[] Feed(params (int Batch, ulong Id)[] listings) =>
        listings.Select(l => this.boundary.IsNewResultSet(l.Batch, l.Id)).ToArray();

    [Fact]
    public void First_listing_ever_starts_a_new_set()
    {
        Assert.True(this.boundary.IsNewResultSet(1, 100));
    }

    [Fact]
    public void A_normal_page_is_one_set()
    {
        // Segments 1, 2, 3 then the final segment 0, up to 4 listings each.
        var result = this.Feed(
            (1, 1), (1, 2), (1, 3), (1, 4),
            (2, 5), (2, 6), (2, 7), (2, 8),
            (3, 9),
            (0, 10), (0, 11));

        Assert.True(result[0]);
        Assert.All(result.Skip(1), Assert.False);
    }

    [Fact]
    public void Refresh_starting_at_segment_1_starts_a_new_set()
    {
        this.Feed((1, 1), (1, 2), (0, 3));

        Assert.True(this.boundary.IsNewResultSet(1, 50));
    }

    [Fact]
    public void Interrupted_page_restarting_at_1_starts_a_new_set()
    {
        // Page change while the previous page was still arriving (no final 0 seen).
        this.Feed((1, 1), (2, 2), (3, 3));

        Assert.True(this.boundary.IsNewResultSet(1, 10));
    }

    [Fact]
    public void Any_segment_after_the_final_segment_starts_a_new_set()
    {
        this.Feed((1, 1), (0, 2));

        Assert.True(this.boundary.IsNewResultSet(2, 3));
    }

    [Fact]
    public void Repeated_listing_id_starts_a_new_set()
    {
        // Two single-segment result sets numbered 0, with the same listings sent again.
        var result = this.Feed((0, 1), (0, 2), (0, 1), (0, 2));

        Assert.Equal(new[] { true, false, true, false }, result);
    }

    [Fact]
    public void More_listings_than_one_segment_holds_starts_a_new_set()
    {
        // Two back-to-back single-segment sets with different listings: 4 then 2.
        var result = this.Feed((0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (0, 6));

        Assert.Equal(new[] { true, false, false, false, true, false }, result);
    }

    [Fact]
    public void Segment_count_restarts_when_the_batch_number_changes()
    {
        // 4 in segment 1 then 4 in segment 2 is still one set.
        var result = this.Feed((1, 1), (1, 2), (1, 3), (1, 4), (2, 5), (2, 6), (2, 7), (2, 8));

        Assert.True(result[0]);
        Assert.All(result.Skip(1), Assert.False);
    }
}

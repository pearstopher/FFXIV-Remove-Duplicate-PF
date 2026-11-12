using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>
/// #72: a small result set sent as a lone segment 0, straight after a set that also ended
/// with segment 0, was merged into the previous set. The game's list being empty when the
/// listings arrive marks the new set.
/// </summary>
public class SmallResultSetTests
{
    private readonly ResultSetBoundary boundary = new();

    // Feeds one segment: every listing in it sees the same game-list state, as in game.
    private bool[] Segment(int batch, bool gameListEmpty, params ulong[] ids) =>
        ids.Select(id => this.boundary.IsNewResultSet(batch, id, gameListEmpty)).ToArray();

    // A normal page: segment 1 arrives while the game's list is empty, later segments while it fills.
    private void FullPage(ulong firstId)
    {
        this.Segment(1, gameListEmpty: true, firstId, firstId + 1, firstId + 2, firstId + 3);
        this.Segment(2, gameListEmpty: false, firstId + 4, firstId + 5, firstId + 6, firstId + 7);
        this.Segment(0, gameListEmpty: false, firstId + 8, firstId + 9);
    }

    [Fact]
    public void Lone_segment_0_after_a_page_ending_in_segment_0_is_a_new_set()
    {
        // The owner's 19:26:17 case: Others page, then a category with only 2 listings.
        this.FullPage(100);

        var result = this.Segment(0, gameListEmpty: true, 900, 901);

        Assert.Equal(new[] { true, false }, result);
    }

    [Fact]
    public void Without_the_game_list_signal_the_old_rules_still_miss_it()
    {
        // Documents why #72 needs the signal: by batch numbers and IDs alone it looks like
        // the rest of the previous set.
        this.FullPage(100);

        Assert.Equal(new[] { false, false }, this.Segment(0, gameListEmpty: false, 900, 901));
    }

    [Fact]
    public void A_whole_lone_segment_is_one_set()
    {
        this.FullPage(100);

        Assert.Equal(new[] { true, false, false, false }, this.Segment(0, gameListEmpty: true, 900, 901, 902, 903));
    }

    [Fact]
    public void Two_small_sets_in_a_row_are_separate()
    {
        this.FullPage(100);
        this.Segment(0, gameListEmpty: true, 900, 901);

        // The window showed those 2 (noted each frame), then the game emptied its list for the
        // next request. Every listing of both small sets sees an empty list.
        this.boundary.NoteGameListShown();
        Assert.Equal(new[] { true, false, false }, this.Segment(0, gameListEmpty: true, 950, 951, 952));
    }

    [Fact]
    public void Without_noting_the_shown_list_two_small_sets_would_merge()
    {
        // Documents why NoteGameListShown is needed (#75): no listing ever sees the list non-empty.
        this.FullPage(100);
        this.Segment(0, gameListEmpty: true, 900, 901);

        // The second small set's first listing isn't recognised as a new set (the old overflow rule
        // only splits later, at the wrong listing).
        Assert.False(this.Segment(0, gameListEmpty: true, 950, 951, 952)[0]);
    }

    [Fact]
    public void Noting_the_shown_list_mid_page_does_not_split_a_page_whose_first_segment_was_all_hidden()
    {
        // If segment 1 was entirely hidden, the window has nothing to show, so it's never noted:
        // segment 2 still belongs to the same set.
        var result = new List<bool>();
        result.AddRange(this.Segment(1, gameListEmpty: true, 1, 2, 3, 4));
        result.AddRange(this.Segment(2, gameListEmpty: true, 5, 6, 7, 8));
        result.AddRange(this.Segment(0, gameListEmpty: false, 9));

        Assert.True(result[0]);
        Assert.All(result.Skip(1), Assert.False);
    }

    [Fact]
    public void Final_segment_0_within_a_page_is_not_split()
    {
        var result = new List<bool>();
        result.AddRange(this.Segment(1, gameListEmpty: true, 1, 2, 3, 4));
        result.AddRange(this.Segment(2, gameListEmpty: false, 5, 6, 7, 8));
        result.AddRange(this.Segment(0, gameListEmpty: false, 9, 10));

        Assert.True(result[0]);
        Assert.All(result.Skip(1), Assert.False);
    }

    [Fact]
    public void Empty_list_that_stays_empty_is_not_a_new_set()
    {
        // If every listing in segment 1 were hidden, the game's list would still be empty when
        // segment 2 arrives. That isn't a transition, so the page isn't split.
        var result = new List<bool>();
        result.AddRange(this.Segment(1, gameListEmpty: true, 1, 2, 3, 4));
        result.AddRange(this.Segment(2, gameListEmpty: true, 5, 6, 7, 8));
        result.AddRange(this.Segment(0, gameListEmpty: false, 9));

        Assert.True(result[0]);
        Assert.All(result.Skip(1), Assert.False);
    }

    [Fact]
    public void Merged_small_set_no_longer_inherits_the_previous_pages_groups()
    {
        var filter = new ListingFilter();

        // Previous page: a pair of "Venue" ads.
        filter.Decide(1, 1, "Venue", true, gameListEmpty: true);
        filter.Decide(1, 2, "Venue", true, gameListEmpty: true);
        filter.Decide(0, 3, "Other", true, gameListEmpty: false);

        // New category with 2 listings, one of them the same venue ad.
        Assert.True(filter.Decide(0, 900, "Venue", true, gameListEmpty: true));
        Assert.True(filter.Decide(0, 901, "Something else", true, gameListEmpty: true));

        var summary = filter.NewestSummary;
        Assert.Equal(2, summary.ResultSetNumber);
        Assert.Equal(2, summary.Received);
        Assert.Equal(0, summary.Hidden);
    }
}

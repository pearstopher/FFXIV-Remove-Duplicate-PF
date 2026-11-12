using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

public class PageTrackerTests
{
    [Theory]
    [InlineData("1-42", 1)]
    [InlineData("1-2", 1)]
    [InlineData("51-93", 2)]
    [InlineData("101-147", 3)]
    [InlineData("151-162", 4)]
    [InlineData(" 51 - 93 ", 2)]
    public void Range_text_gives_the_page(string text, int page)
    {
        Assert.Equal(page, PageRange.PageOf(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("(null)")]
    [InlineData("42(306)")]
    [InlineData("0-50")]
    [InlineData("Page 2")]
    public void Text_that_isnt_a_range_gives_no_page(string? text)
    {
        Assert.Null(PageRange.PageOf(text));
    }

    [Fact]
    public void Owners_walkthrough_maps_page_indexes_to_pages()
    {
        // Round 3 (#73): byte 0x0484 at each result-set start for Next x3, Previous x3, Next, Refresh.
        var tracker = new PageTracker();
        var indexes = new int?[] { 0, 1, 2, 3, 2, 1, 0, 1, 0 };

        Assert.Equal(new[] { 1, 2, 3, 4, 3, 2, 1, 2, 1 }, indexes.Select(tracker.PageFor).ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(PageTracker.MaxPlausiblePages)]
    [InlineData(255)]
    public void Unknown_or_implausible_indexes_count_as_page_1(int? index)
    {
        Assert.Equal(1, new PageTracker().PageFor(index));
    }

    [Fact]
    public void Matching_range_text_keeps_the_index_trusted()
    {
        var tracker = new PageTracker();

        Assert.False(tracker.Verify(2, "51-93"));
        Assert.False(tracker.Verify(2, "51-93"));
        Assert.False(tracker.Unreliable);
        Assert.Equal(3, tracker.PageFor(2));
    }

    [Fact]
    public void One_mismatch_is_tolerated_as_a_stale_frame()
    {
        var tracker = new PageTracker();

        Assert.False(tracker.Verify(2, "1-42")); // window not redrawn yet
        Assert.False(tracker.Verify(2, "51-93"));
        Assert.False(tracker.Verify(3, "51-93")); // another single stale frame later
        Assert.False(tracker.Unreliable);
    }

    [Fact]
    public void Two_mismatches_in_a_row_make_the_index_unreliable_and_fall_back_to_page_1()
    {
        var tracker = new PageTracker();

        Assert.False(tracker.Verify(3, "51-93"));
        Assert.True(tracker.Verify(3, "51-93")); // reported once
        Assert.True(tracker.Unreliable);
        Assert.False(tracker.Verify(3, "51-93")); // not reported again

        Assert.Equal(1, tracker.PageFor(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("(null)")]
    public void Range_text_that_isnt_shown_yet_is_not_a_mismatch(string text)
    {
        var tracker = new PageTracker();

        Assert.False(tracker.Verify(2, text));
        Assert.False(tracker.Verify(2, text));
        Assert.False(tracker.Unreliable);
    }

    [Theory]
    [InlineData("51-93", 43)]
    [InlineData("1-50", 50)]
    [InlineData("251-252", 2)]
    [InlineData("101-101", 1)]
    public void Range_text_gives_the_count(string text, int count) =>
        Assert.Equal(count, PageRange.CountOf(text));

    [Theory]
    [InlineData("")]
    [InlineData("93-51")]
    [InlineData("0-0")]
    public void Text_that_isnt_a_range_gives_no_count(string text) =>
        Assert.Null(PageRange.CountOf(text));

    [Fact]
    public void Range_text_left_over_from_the_previous_page_is_skipped_not_a_mismatch()
    {
        // #84, 20:37:41: page 6 (2 listings) arrived while the window still showed page 5's "201-238".
        var tracker = new PageTracker();

        Assert.False(tracker.Verify(6, "201-238", displayedCount: 2));
        Assert.False(tracker.Verify(6, "201-238", displayedCount: 2));
        Assert.False(tracker.Unreliable);

        // Once redrawn it's checked as usual.
        Assert.False(tracker.Verify(6, "251-252", displayedCount: 2));
        Assert.False(tracker.Unreliable);
    }

    [Fact]
    public void A_real_mismatch_with_matching_counts_is_still_caught()
    {
        var tracker = new PageTracker();

        Assert.False(tracker.Verify(3, "51-93", displayedCount: 43));
        Assert.True(tracker.Verify(3, "51-93", displayedCount: 43));
    }

    [Fact]
    public void Result_sets_record_their_page()
    {
        var filter = new ListingFilter();

        filter.Decide(1, 1, "A", true, gameListEmpty: true, gamePageIndex: 0);
        filter.Decide(0, 2, "B", true, gameListEmpty: false, gamePageIndex: 0);
        Assert.Equal(1, filter.NewestSummary.Page);

        filter.Decide(1, 3, "C", true, gameListEmpty: true, gamePageIndex: 1);
        Assert.Equal(2, filter.NewestSummary.Page);
        Assert.StartsWith("result set #2 (page 2):", filter.NewestSummary.Describe());
    }

    [Fact]
    public void Only_the_first_listing_decides_the_page()
    {
        var filter = new ListingFilter();

        filter.Decide(1, 1, "A", true, gameListEmpty: true, gamePageIndex: 1);
        filter.Decide(1, 2, "B", true, gameListEmpty: true, gamePageIndex: 7); // ignored: same set

        Assert.Equal(2, filter.NewestSummary.Page);
    }

    [Fact]
    public void Short_last_page_is_found_despite_stale_slots_from_the_previous_page()
    {
        // Owner's 19:40:31 walk-through: page 3 had 50 listings, page 4 only 18. The game's 50
        // displayed-listing slots kept page 3's IDs after the first 18, which outnumbered page 4's
        // and made page 3 look like it was on screen.
        var filter = new ListingFilter();
        var page3 = Enumerable.Range(300, 50).Select(i => (ulong)i).ToArray();
        var page4 = Enumerable.Range(400, 18).Select(i => (ulong)i).ToArray();

        FeedPage(filter, page3, gamePageIndex: 2);
        FeedPage(filter, page4, gamePageIndex: 3);

        var slots = page4.Concat(page3.Skip(18)).ToArray(); // 18 new IDs, then 32 stale ones

        Assert.Equal(3, filter.ForDisplayed(slots).Page);                // reading all 50 slots: wrong page
        Assert.Equal(4, filter.ForDisplayed(slots, displayedCount: 18).Page); // only the shown slots: right
        Assert.Equal(18, filter.ForDisplayed(slots, displayedCount: 18).MatchedListings);
    }

    // Feeds a page the way the game sends it: segments of 4 numbered 1, 2, ..., with the last
    // one numbered 0. The game's list is empty only while the first segment is processed.
    private static void FeedPage(ListingFilter filter, ulong[] ids, int gamePageIndex)
    {
        var segments = ids.Chunk(ResultSetBoundary.MaxListingsPerSegment).ToArray();
        for (var s = 0; s < segments.Length; s++)
        {
            var batch = s == segments.Length - 1 ? 0 : s + 1;
            foreach (var id in segments[s])
                filter.Decide(batch, id, $"Listing {id}", true, gameListEmpty: s == 0, gamePageIndex: gamePageIndex);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void No_displayed_slots_fall_back_to_the_newest(int count)
    {
        var filter = new ListingFilter();
        filter.Decide(1, 1, "A", true, gameListEmpty: true, gamePageIndex: 0);

        var shown = filter.ForDisplayed(new ulong[] { 1, 2, 3 }, count);

        Assert.True(shown.IsNewest);
        Assert.Equal(0, shown.MatchedListings);
    }

    [Fact]
    public void Displayed_result_set_reports_its_page()
    {
        var filter = new ListingFilter();
        filter.Decide(1, 1, "A", true, gameListEmpty: true, gamePageIndex: 0);
        filter.Decide(0, 10, "A2", true, gameListEmpty: false, gamePageIndex: 0); // page 1 shown
        filter.Decide(1, 2, "B", true, gameListEmpty: true, gamePageIndex: 1);   // Next: page 2 starts

        Assert.Equal(1, filter.ForDisplayed(new ulong[] { 1 }).Page);
        Assert.Equal(2, filter.ForDisplayed(new ulong[] { 2 }).Page);
    }
}

using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>
/// #51: the game can re-fetch a page in the background while another page stays on screen.
/// The title and prefixes must follow the page on screen, not the newest result set.
/// </summary>
public class DisplayedResultSetTests
{
    private readonly ListingFilter filter = new();

    // Feeds one page the way the game sends it and returns the IDs it left visible.
    private List<ulong> Feed(params (ulong Id, string? Text)[] listings)
    {
        var visible = GameFeed.Page(this.filter, listings);
        return listings.Where((_, i) => visible[i]).Select(l => l.Id).ToList();
    }

    // Shape of the owner's 13:34:52 page 1: five pairs among other listings.
    private List<ulong> Page1() => this.Feed(
        (14825, "[RP] Utterly Delicious - Hingan"), (15994, "Lewd lounge NSFW open now 18+ !!"),
        (12999, "[21+ Garlean RP] LGBTQIA+ Friendly"), (15792, "Dancers 18+ wanted tonight, apply"),
        (16035, "BUNN$BEAT club night every Friday"), (100, "Single A"), (101, "Single B"),
        (15009, "[RP] Utterly Delicious - Hingan"), (14050, "Lewd lounge NSFW open now 18+ !!"),
        (16036, "[21+ Garlean RP] LGBTQIA+ Friendly"), (16076, "Dancers 18+ wanted tonight, apply"),
        (16085, "BUNN$BEAT club night every Friday"));

    // Shape of the owner's 13:35:02 page 2: one triple and two pairs.
    private List<ulong> Page2() => this.Feed(
        (16117, "BUNN$BEAT club night every Friday"), (16132, "The taste that lingers, come try"),
        (16135, "The taste that lingers, come try"), (15662, "[FYLC] 20M BOOSTED BINGO! | BL"),
        (14917, "[FYLC] 20M BOOSTED BINGO! | BL"), (14782, "BUNN$BEAT club night every Friday"),
        (14783, "BUNN$BEAT club night every Friday"), (200, "Single C"));

    // Open the window on page 1, then Next to page 2. Returns page 2's shown listings.
    private List<ulong> OpenAndGoToPage2()
    {
        this.filter.NoteWindowOpened();
        this.Page1();
        this.filter.NoteButtonClick(SearchStart.NextButton);
        return this.Page2();
    }

    [Fact]
    public void Background_refetch_of_page_1_does_not_change_the_count_for_page_2_on_screen()
    {
        var page2OnScreen = this.OpenAndGoToPage2();
        this.Page1(); // background re-fetch while page 2 stays on screen (no click)

        var shown = this.filter.ForDisplayed(page2OnScreen);

        // Page 2 hides its 3 BUNN$BEAT copies (page 1 showed that ad) plus one copy each of two ads.
        Assert.Equal(5, shown.HiddenTotal);
        Assert.False(shown.IsNewest);
        Assert.Equal(2, shown.ResultSetNumber); // the second result set received
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["The taste that lingers, come try"] = 2,
                ["[FYLC] 20M BOOSTED BINGO! | BL"] = 2,
            },
            shown.Prefixes);
    }

    [Fact]
    public void Page_on_screen_that_is_also_the_newest_uses_the_newest()
    {
        var page2 = this.OpenAndGoToPage2();

        var shown = this.filter.ForDisplayed(page2);

        Assert.True(shown.IsNewest);
        Assert.Equal(5, shown.HiddenTotal);
    }

    [Fact]
    public void Going_back_to_page_1_uses_page_1_with_page_2s_copies_counted()
    {
        this.OpenAndGoToPage2();
        this.filter.NoteButtonClick(SearchStart.PreviousButton);
        var page1 = this.Page1();

        var shown = this.filter.ForDisplayed(page1);

        Assert.Equal(5, shown.HiddenTotal);
        Assert.Equal(5, shown.Prefixes["BUNN$BEAT club night every Friday"]); // 2 on page 1 + 3 on page 2
        Assert.Equal(2, shown.Prefixes["[RP] Utterly Delicious - Hingan"]);
    }

    [Fact]
    public void With_nothing_displayed_the_newest_set_is_used()
    {
        this.OpenAndGoToPage2();

        var shown = this.filter.ForDisplayed(Array.Empty<ulong>());

        Assert.True(shown.IsNewest);
        Assert.Equal(5, shown.HiddenTotal);
    }

    [Fact]
    public void Zero_ids_in_the_displayed_list_are_ignored()
    {
        this.OpenAndGoToPage2();

        Assert.True(this.filter.ForDisplayed(new ulong[] { 0, 0, 0 }).IsNewest);
    }

    [Fact]
    public void Only_the_last_few_result_sets_are_kept()
    {
        var oldest = this.Page1();

        // Each filler page has 5 listings, so it arrives as segments 1 then 0 like a real page.
        for (var page = 0; page < ListingFilter.HistorySize; page++)
        {
            var listings = Enumerable.Range(0, 5)
                .Select(i => ((ulong)(1000 + (page * 10) + i), (string?)$"Filler {page}-{i}"))
                .ToArray();
            this.Feed(listings);
        }

        // The first page has dropped out of history, so the newest set is used.
        var shown = this.filter.ForDisplayed(oldest);
        Assert.True(shown.IsNewest);
        Assert.Equal(0, shown.MatchedListings);
    }

    [Fact]
    public void Prefixes_are_built_once_when_first_read()
    {
        var page2 = this.OpenAndGoToPage2();
        var shown = this.filter.ForDisplayed(page2);

        Assert.Same(shown.Prefixes, shown.Prefixes);
    }

    [Fact]
    public void Hidden_duplicates_are_not_counted_as_shown_listings()
    {
        var page2 = this.OpenAndGoToPage2();

        // 16135 is a hidden copy on page 2; the window never shows it.
        Assert.DoesNotContain(16135UL, page2);
    }

    [Fact]
    public void Short_last_page_is_found_despite_stale_slots_from_the_previous_page()
    {
        // Owner's 19:40:31 walk-through: page 3 had 50 listings, page 4 only 18. The game's 50
        // displayed-listing slots kept page 3's IDs after the first 18, which outnumbered page 4's
        // and made page 3 look like it was on screen (#74).
        var page3 = Enumerable.Range(300, 50).Select(i => ((ulong)i, (string?)$"Listing {i}")).ToArray();
        var page4 = Enumerable.Range(400, 18).Select(i => ((ulong)i, (string?)$"Listing {i}")).ToArray();
        this.Feed(page3);
        this.Feed(page4);

        var slots = page4.Concat(page3.Skip(18)).Select(l => l.Item1).ToArray(); // 18 new IDs, then 32 stale

        Assert.Equal(1, this.filter.ForDisplayed(slots).ResultSetNumber);                    // all 50 slots: wrong
        Assert.Equal(2, this.filter.ForDisplayed(slots, displayedCount: 18).ResultSetNumber); // shown slots: right
        Assert.Equal(18, this.filter.ForDisplayed(slots, displayedCount: 18).MatchedListings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void No_displayed_slots_fall_back_to_the_newest(int count)
    {
        this.filter.Decide(1, 1, "A", true);

        var shown = this.filter.ForDisplayed(new ulong[] { 1, 2, 3 }, count);

        Assert.True(shown.IsNewest);
        Assert.Equal(0, shown.MatchedListings);
    }
}

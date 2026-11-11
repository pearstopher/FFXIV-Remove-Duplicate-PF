using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>
/// #51: the game can re-fetch a page in the background while another page stays on screen.
/// The title and prefixes must follow the page on screen, not the newest result set.
/// </summary>
public class DisplayedResultSetTests
{
    private readonly ListingFilter filter = new();

    // Feeds a result set as segments of up to 4 listings: 1, 2, ..., then a final segment 0.
    private List<ulong> Feed(params (ulong Id, string Text)[] listings)
    {
        var shown = new List<ulong>();
        var segments = listings.Chunk(ResultSetBoundary.MaxListingsPerSegment).ToArray();
        for (var s = 0; s < segments.Length; s++)
        {
            var batch = s == segments.Length - 1 ? 0 : s + 1;
            foreach (var (id, text) in segments[s])
            {
                if (this.filter.Decide(batch, id, text, currentlyVisible: true))
                    shown.Add(id);
            }
        }

        return shown;
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

    [Fact]
    public void Background_refetch_of_page_1_does_not_change_the_count_for_page_2_on_screen()
    {
        this.Page1();
        var page2OnScreen = this.Page2();
        this.Page1(); // background re-fetch while page 2 stays on screen

        var shown = this.filter.ForDisplayed(page2OnScreen);

        Assert.Equal(4, shown.Tracker.HiddenCount);
        Assert.False(shown.IsNewest);
        Assert.Equal(2, shown.ResultSetNumber); // the second result set received
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["BUNN$BEAT club night every Friday"] = 3,
                ["The taste that lingers, come try"] = 2,
                ["[FYLC] 20M BOOSTED BINGO! | BL"] = 2,
            },
            shown.Tracker.DuplicateGroupsByDescription);
    }

    [Fact]
    public void Page_on_screen_that_is_also_the_newest_uses_the_newest()
    {
        this.Page1();
        var page2 = this.Page2();

        var shown = this.filter.ForDisplayed(page2);

        Assert.True(shown.IsNewest);
        Assert.Equal(4, shown.Tracker.HiddenCount);
    }

    [Fact]
    public void Going_back_to_page_1_uses_page_1()
    {
        var page1 = this.Page1();
        this.Page2();
        this.Page1();

        Assert.Equal(5, this.filter.ForDisplayed(page1).Tracker.HiddenCount);
    }

    [Fact]
    public void With_nothing_displayed_the_newest_set_is_used()
    {
        this.Page1();
        this.Page2();

        var shown = this.filter.ForDisplayed(Array.Empty<ulong>());

        Assert.True(shown.IsNewest);
        Assert.Equal(4, shown.Tracker.HiddenCount);
    }

    [Fact]
    public void Zero_ids_in_the_displayed_list_are_ignored()
    {
        this.Page1();
        this.Page2();

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
                .Select(i => ((ulong)(1000 + (page * 10) + i), $"Filler {page}-{i}"))
                .ToArray();
            this.Feed(listings);
        }

        // The first page has dropped out of history, so the newest set is used.
        var shown = this.filter.ForDisplayed(oldest);
        Assert.True(shown.IsNewest);
        Assert.Equal(0, shown.MatchedListings);
    }

    [Fact]
    public void Hidden_duplicates_are_not_counted_as_shown_listings()
    {
        this.Page1();
        var page2 = this.Page2();

        // 16135 is a hidden copy on page 2; the window never shows it.
        Assert.DoesNotContain(16135UL, page2);
    }
}

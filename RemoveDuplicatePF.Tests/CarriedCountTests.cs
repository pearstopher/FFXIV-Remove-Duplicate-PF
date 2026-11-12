using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>
/// #81: going back to an earlier page, each [N] includes the copies hidden on later pages,
/// counting each listing once. The title still counts what's hidden on the page on screen.
/// </summary>
public class CarriedCountTests
{
    private readonly ListingFilter filter = new();
    private ulong nextId = 1;

    // Feeds one page the way the game sends it (see CrossPageHidingTests.Page). Each listing is
    // (ID, description), so a revisit can resend the same listings.
    private void Page(int gamePageIndex, params (ulong Id, string Description)[] listings)
    {
        var anyVisible = false;
        var segments = listings.Chunk(ResultSetBoundary.MaxListingsPerSegment).ToArray();
        for (var s = 0; s < segments.Length; s++)
        {
            var batch = s == segments.Length - 1 ? 0 : s + 1;
            foreach (var (id, description) in segments[s])
                anyVisible |= this.filter.Decide(batch, id, description, true, gameListEmpty: s == 0, gamePageIndex: gamePageIndex);
        }

        if (anyVisible)
            this.filter.NoteGameListShown();
    }

    private (ulong, string) New(string description) => (this.nextId++, description);

    private DisplayedResultSet OnScreen() => this.filter.ForDisplayed(Array.Empty<ulong>());

    private void Next() => this.filter.NoteButtonClick(SearchStart.NextButton);

    private void Previous() => this.filter.NoteButtonClick(SearchStart.PreviousButton);

    [Fact]
    public void Owners_example_venue_becomes_5_on_page_1_and_titles_stay_the_same()
    {
        this.filter.NoteWindowOpened();
        (ulong, string)[] page1 = [this.New("Venue"), this.New("Venue"), this.New("Other")];
        this.Page(0, page1);
        Assert.Equal(1, this.OnScreen().HiddenTotal);

        this.Next();
        (ulong, string)[] page2 = [this.New("Venue"), this.New("Venue"), this.New("Venue"), this.New("Page 2")];
        this.Page(1, page2);
        Assert.Equal(3, this.OnScreen().HiddenTotal);

        this.Previous();
        this.Page(0, page1);

        var shown = this.OnScreen();
        Assert.Equal(5, shown.Prefixes["Venue"]);
        Assert.Equal(1, shown.HiddenTotal); // title for page 1 unchanged
        Assert.Equal(new Dictionary<string, int> { ["Venue"] = 2 }, shown.Tracker.DuplicateGroupsByDescription);

        // Next and Previous again: the same listings aren't added twice.
        this.Next();
        this.Page(1, page2);
        Assert.Equal(3, this.OnScreen().HiddenTotal);
        this.Previous();
        this.Page(0, page1);
        Assert.Equal(5, this.OnScreen().Prefixes["Venue"]);
    }

    [Fact]
    public void A_single_listing_on_page_1_becomes_2()
    {
        this.filter.NoteWindowOpened();
        (ulong, string)[] page1 = [this.New("Solo ad"), this.New("Other")];
        this.Page(0, page1);
        Assert.False(this.OnScreen().Prefixes.ContainsKey("Solo ad"));

        this.Next();
        this.Page(1, this.New("Solo ad"), this.New("Page 2"));

        // Page 2 shows no prefix for it (it's hidden there).
        Assert.False(this.OnScreen().Prefixes.ContainsKey("Solo ad"));

        this.Previous();
        this.Page(0, page1);
        Assert.Equal(2, this.OnScreen().Prefixes["Solo ad"]);
    }

    [Fact]
    public void Copies_go_to_the_earliest_page_that_shows_the_listing()
    {
        this.filter.NoteWindowOpened();
        (ulong, string)[] page1 = [this.New("Page 1")];
        this.Page(0, page1);

        this.Next();
        (ulong, string)[] page2 = [this.New("First on page 2"), this.New("First on page 2")];
        this.Page(1, page2);
        Assert.Equal(2, this.OnScreen().Prefixes["First on page 2"]);

        this.Next();
        this.Page(2, this.New("First on page 2"), this.New("First on page 2"), this.New("Page 3"));

        this.Previous();
        this.Page(1, page2);
        Assert.Equal(4, this.OnScreen().Prefixes["First on page 2"]);
        Assert.Equal(1, this.OnScreen().HiddenTotal);

        this.Previous();
        this.Page(0, page1);
        Assert.Empty(this.OnScreen().Prefixes);
    }

    [Fact]
    public void Pages_1_to_3_paged_back_and_forth_count_each_listing_once()
    {
        this.filter.NoteWindowOpened();
        (ulong, string)[] page1 = [this.New("Ad")];
        (ulong, string)[] page2 = [this.New("Ad"), this.New("P2")];
        (ulong, string)[] page3 = [this.New("Ad"), this.New("Ad"), this.New("P3")];

        this.Page(0, page1);
        for (var round = 0; round < 3; round++)
        {
            this.Next();
            this.Page(1, page2);
            this.Next();
            this.Page(2, page3);
            this.Previous();
            this.Page(1, page2);
            this.Previous();
            this.Page(0, page1);
        }

        Assert.Equal(4, this.OnScreen().Prefixes["Ad"]);
    }

    [Fact]
    public void A_listing_that_moved_up_to_page_1_isnt_counted_twice()
    {
        this.filter.NoteWindowOpened();
        var kept = this.New("Ad");
        this.Page(0, kept, this.New("Other"));

        this.Next();
        var moving = this.New("Ad");
        this.Page(1, moving, this.New("P2"));

        // Back on page 1, the page-2 copy has moved up: 2 distinct listings, not 3.
        this.Previous();
        this.Page(0, kept, moving);
        Assert.Equal(2, this.OnScreen().Prefixes["Ad"]);
    }

    [Theory]
    [InlineData(SearchStart.RefreshButton)]
    [InlineData(22)] // a category tab
    public void A_new_search_resets_the_counts(int button)
    {
        this.filter.NoteWindowOpened();
        (ulong, string)[] page1 = [this.New("Venue")];
        this.Page(0, page1);
        this.Next();
        // "Page 2" keeps a listing on screen, so the game's list is seen with listings and the
        // page-1 resend below is a new result set (ResultSetBoundary, #72).
        this.Page(1, this.New("Venue"), this.New("Venue"), this.New("Page 2"));

        this.filter.NoteButtonClick(button);
        this.Page(0, page1);

        Assert.Empty(this.OnScreen().Prefixes);
    }

    [Fact]
    public void A_repeated_listing_on_a_later_page_counts_itself_plus_carried_copies()
    {
        // #84 shows the very same listing again on page 2; its [N] there counts it and the
        // copies hidden on later pages, but not page 1's other copy, which page 2 never sent.
        this.filter.NoteWindowOpened();
        var repeated = this.New("Venue");
        (ulong, string)[] page1 = [repeated, this.New("Venue"), this.New("Other")];
        this.Page(0, page1);

        this.Next();
        this.Page(1, repeated, this.New("Venue"), this.New("Page 2"));
        Assert.Equal(2, this.OnScreen().Prefixes["Venue"]);
        Assert.Equal(1, this.OnScreen().HiddenTotal); // only the new copy is hidden

        this.Previous();
        this.Page(0, page1);
        Assert.Equal(3, this.OnScreen().Prefixes["Venue"]);
    }

    [Fact]
    public void Without_carried_copies_prefixes_are_the_pages_own_groups()
    {
        this.Page(0, this.New("A"), this.New("A"), this.New("B"));

        Assert.Equal(this.OnScreen().Tracker.DuplicateGroupsByDescription, this.OnScreen().Prefixes);
    }
}

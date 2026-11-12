using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>
/// #88: duplicates are tracked by listing ID across the whole search. The first copy of an ad seen
/// in the search is shown, wherever it is, with <c>[N]</c> = every copy found so far, including
/// copies only found on later pages. Every other copy is hidden on every page. A new search starts
/// over. Also covers the shifting-page scenarios of #85.
/// </summary>
public class SearchWideTests
{
    private const int CategoryTab = 22;

    private readonly ListingFilter filter = new();
    private ulong nextId = 1;

    public SearchWideTests() => this.filter.NoteWindowOpened();

    // Feeds one page the way the game sends it (segments of 4: 1, 2, ..., last one 0; the game's
    // list empty only during the first segment) and returns each listing's visibility.
    private bool[] Page(params (ulong Id, string? Description)[] listings)
    {
        var result = new List<bool>();
        var segments = listings.Chunk(ResultSetBoundary.MaxListingsPerSegment).ToArray();
        for (var s = 0; s < segments.Length; s++)
        {
            var batch = s == segments.Length - 1 ? 0 : s + 1;
            foreach (var (id, description) in segments[s])
                result.Add(this.filter.Decide(batch, id, description, true, gameListEmpty: s == 0));
        }

        // The window shows the page before the player moves on.
        if (result.Any(visible => visible))
            this.filter.NoteGameListShown();

        return result.ToArray();
    }

    // A new listing (new ID) with this description.
    private (ulong, string?) New(string? description) => (this.nextId++, description);

    // Listings with IDs first..last and distinct texts.
    private static (ulong, string?)[] Listings(ulong first, ulong last) =>
        Enumerable.Range((int)first, (int)(last - first + 1)).Select(i => ((ulong)i, (string?)$"Listing {i}")).ToArray();

    private DisplayedResultSet OnScreen() => this.filter.ForDisplayed(Array.Empty<ulong>());

    private void Next() => this.filter.NoteButtonClick(SearchStart.NextButton);

    private void Previous() => this.filter.NoteButtonClick(SearchStart.PreviousButton);

    [Fact]
    public void Owners_example_venue_becomes_5_on_page_1_and_titles_count_each_page()
    {
        (ulong, string?)[] page1 = [this.New("Venue"), this.New("Venue"), this.New("Other")];
        Assert.Equal(new[] { true, false, true }, this.Page(page1));
        Assert.Equal(2, this.OnScreen().Prefixes["Venue"]);
        Assert.Equal(1, this.OnScreen().HiddenTotal);

        // Page 2: three more copies, all hidden, no [N] there.
        this.Next();
        (ulong, string?)[] page2 = [this.New("Venue"), this.New("Venue"), this.New("Venue"), this.New("Page 2")];
        Assert.Equal(new[] { false, false, false, true }, this.Page(page2));
        Assert.False(this.OnScreen().Prefixes.ContainsKey("Venue"));
        Assert.Equal(3, this.OnScreen().HiddenTotal);

        // Back to page 1: the first copy shows all 5.
        this.Previous();
        this.Page(page1);
        Assert.Equal(5, this.OnScreen().Prefixes["Venue"]);
        Assert.Equal(1, this.OnScreen().HiddenTotal);

        // Next and Previous again: the same listings aren't counted twice.
        this.Next();
        this.Page(page2);
        this.Previous();
        this.Page(page1);
        Assert.Equal(5, this.OnScreen().Prefixes["Venue"]);
    }

    [Fact]
    public void A_single_listing_on_page_1_becomes_2()
    {
        (ulong, string?)[] page1 = [this.New("Solo ad"), this.New("Other")];
        this.Page(page1);
        Assert.False(this.OnScreen().Prefixes.ContainsKey("Solo ad"));

        this.Next();
        Assert.Equal(new[] { false, true }, this.Page(this.New("Solo ad"), this.New("Page 2")));

        this.Previous();
        this.Page(page1);
        Assert.Equal(2, this.OnScreen().Prefixes["Solo ad"]);
    }

    [Fact]
    public void An_ad_first_seen_on_page_2_carries_its_count_there()
    {
        (ulong, string?)[] page1 = [this.New("Page 1")];
        this.Page(page1);

        this.Next();
        (ulong, string?)[] page2 = [this.New("First on page 2"), this.New("First on page 2")];
        this.Page(page2);
        Assert.Equal(2, this.OnScreen().Prefixes["First on page 2"]);

        this.Next();
        Assert.Equal(new[] { false, false, true }, this.Page(this.New("First on page 2"), this.New("First on page 2"), this.New("Page 3")));

        this.Previous();
        this.Page(page2);
        Assert.Equal(4, this.OnScreen().Prefixes["First on page 2"]);
        Assert.Equal(1, this.OnScreen().HiddenTotal);

        this.Previous();
        this.Page(page1);
        Assert.Empty(this.OnScreen().Prefixes);
    }

    [Fact]
    public void The_first_copy_sent_again_on_a_later_page_is_itself_with_its_count()
    {
        // #85 scenario 1: pages overlap, so the game sends listing 1 again on page 2.
        this.Page((1, "Venue"), (2, "Venue"), (3, "Other"));

        this.Next();
        Assert.Equal(new[] { true, false, true }, this.Page((1, "Venue"), (4, "Venue"), (5, "New")));
        Assert.Equal(3, this.OnScreen().Prefixes["Venue"]);
        Assert.Equal(1, this.OnScreen().HiddenTotal); // only listing 4

        this.Previous();
        this.Page((1, "Venue"), (2, "Venue"), (3, "Other"));
        Assert.Equal(3, this.OnScreen().Prefixes["Venue"]);
    }

    [Fact]
    public void A_hidden_copy_stays_hidden_when_sent_again_on_another_page()
    {
        Assert.Equal(new[] { true, false }, this.Page((1, "Venue"), (2, "Venue")));

        this.Next();
        Assert.Equal(new[] { false, true }, this.Page((2, "Venue"), (3, "New")));
    }

    [Fact]
    public void Pages_1_to_3_paged_back_and_forth_count_each_listing_once()
    {
        (ulong, string?)[] page1 = [this.New("Ad")];
        (ulong, string?)[] page2 = [this.New("Ad"), this.New("P2")];
        (ulong, string?)[] page3 = [this.New("Ad"), this.New("Ad"), this.New("P3")];

        this.Page(page1);
        for (var round = 0; round < 3; round++)
        {
            this.Next();
            this.Page(page2);
            this.Next();
            this.Page(page3);
            this.Previous();
            this.Page(page2);
            this.Previous();
            this.Page(page1);
        }

        Assert.Equal(4, this.OnScreen().Prefixes["Ad"]);
    }

    [Fact]
    public void A_copy_that_moved_up_to_page_1_counts_once()
    {
        // #85 scenario 4.
        var first = this.New("Ad");
        this.Page(first, this.New("Other"));

        this.Next();
        var moving = this.New("Ad");
        this.Page(moving, this.New("P2"));

        this.Previous();
        Assert.Equal(new[] { true, false }, this.Page(first, moving));
        Assert.Equal(2, this.OnScreen().Prefixes["Ad"]);
    }

    [Fact]
    public void Previous_landing_10_early_shows_the_overlap_like_the_game_does()
    {
        // #85 scenarios 2-3, the owner's Others walk-through: page 2 again started at 41 instead of 51.
        this.Page(Listings(1, 50));
        this.Next();
        this.Page(Listings(51, 100));
        this.Next();
        this.Page(Listings(101, 120));

        this.Previous();
        Assert.All(this.Page(Listings(41, 90)), Assert.True);
        Assert.Equal(0, this.OnScreen().HiddenTotal);
    }

    [Fact]
    public void A_page_made_entirely_of_earlier_listings_is_never_blank()
    {
        this.Page(Listings(1, 50));
        this.Next();
        this.Page(Listings(51, 100));

        this.Next();
        Assert.All(this.Page(Listings(42, 91)), Assert.True);
    }

    [Fact]
    public void A_last_page_that_shrinks_or_grows_changes_nothing_else()
    {
        // #85 scenario 6: nothing is stored per page, so a changed last page can't go stale.
        this.Page(Listings(1, 50));
        this.Next();
        this.Page(Listings(51, 60));
        this.Previous();
        this.Page(Listings(1, 50));

        this.Next();
        Assert.All(this.Page(Listings(51, 55)), Assert.True);
        this.Previous();
        this.Page(Listings(1, 50));
        this.Next();
        Assert.All(this.Page(Listings(51, 70)), Assert.True);
    }

    [Fact]
    public void A_removed_first_copy_keeps_its_copies_hidden_until_a_new_search()
    {
        // Accepted by the owner: the copies come back with Refresh.
        this.Page((1, "Venue"), (2, "P1"));
        this.Next();
        this.Page((3, "Venue"), (4, "P2"));

        this.Previous();
        Assert.Equal(new[] { false, true }, this.Page((3, "Venue"), (2, "P1"))); // listing 1 is gone

        this.filter.NoteButtonClick(SearchStart.RefreshButton);
        Assert.Equal(new[] { true, true }, this.Page((3, "Venue"), (2, "P1")));
    }

    [Theory]
    [InlineData(SearchStart.RefreshButton)]
    [InlineData(CategoryTab)]
    public void Refresh_or_a_category_change_starts_over(int button)
    {
        (ulong, string?)[] page1 = [this.New("Venue"), this.New("Other")];
        this.Page(page1);
        this.Next();
        this.Page(this.New("Venue"), this.New("Venue"), this.New("Page 2"));

        this.filter.NoteButtonClick(button);
        this.Page(page1);

        Assert.Empty(this.OnScreen().Prefixes);
        Assert.True(this.filter.NewestSummary.NewSearch);
    }

    [Fact]
    public void Reopening_the_window_starts_over()
    {
        this.Page((1, "Venue"), (2, "Other"));
        this.Next();
        this.Page((3, "P2"));

        this.filter.NoteWindowOpened();
        Assert.Equal(new[] { true, true }, this.Page((3, "P2"), (4, "Venue")));
    }

    [Fact]
    public void A_background_refetch_keeps_the_search()
    {
        // #51 / #85 scenario 7: while page 2 is on screen, the game re-downloads page 1 with no click.
        (ulong, string?)[] page1 = [this.New("Venue"), this.New("Other")];
        this.Page(page1);
        this.Next();
        this.Page(this.New("Venue"), this.New("Page 2"));

        this.Page(page1);

        Assert.False(this.filter.NewestSummary.NewSearch);
        Assert.Equal(2, this.OnScreen().Prefixes["Venue"]);
    }

    [Fact]
    public void The_log_says_whether_a_result_set_started_a_new_search()
    {
        this.Page(this.New("A"));
        Assert.StartsWith("result set #1 (new search):", this.filter.NewestSummary.Describe());

        this.Next();
        this.Page(this.New("B"));
        this.Previous();
        this.Page(this.New("A"));
        Assert.StartsWith("result set #3 (same search):", this.filter.NewestSummary.Describe());
    }

    [Fact]
    public void Matching_uses_the_first_30_characters()
    {
        this.Page(this.New("Come to my venue tonight! Doors 9pm ST"));

        this.Next();
        Assert.Equal(new[] { false, true }, this.Page(this.New("Come to my venue tonight! Doors 10pm ST"), this.New("Other")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_descriptions_are_never_hidden(string? description)
    {
        this.Page(this.New(description));

        this.Next();
        Assert.Equal(new[] { true, true }, this.Page(this.New(description), this.New(description)));
    }

    [Fact]
    public void A_listing_another_plugin_hid_never_becomes_the_first_copy()
    {
        // Another plugin hid "Spam" on page 1, so the player never saw it there.
        this.filter.Decide(1, 1, "Spam", currentlyVisible: false, gameListEmpty: true);
        this.filter.Decide(0, 2, "Other", currentlyVisible: true, gameListEmpty: false);
        this.filter.NoteGameListShown();

        this.Next();
        Assert.Equal(new[] { true }, this.Page(this.New("Spam")));
    }

    [Fact]
    public void Owners_title_example_gives_7()
    {
        // Page 2 shows [3] [2] and hides 4 listings whose ads page 1 showed: 2 + 1 + 4 = 7.
        this.Page(this.New("P1 a"), this.New("P1 b"), this.New("P1 c"), this.New("P1 d"));

        this.Next();
        this.Page(
            this.New("A"), this.New("A"), this.New("A"), this.New("B"), this.New("B"),
            this.New("P1 a"), this.New("P1 b"), this.New("P1 c"), this.New("P1 d"), this.New("Solo"));

        var shown = this.OnScreen();
        Assert.Equal(7, shown.HiddenTotal);
        Assert.Equal(new Dictionary<string, int> { ["A"] = 3, ["B"] = 2 }, shown.Prefixes);
        Assert.Equal("Party Finder (7 duplicates hidden)", TitleText.Compose("Party Finder", shown.HiddenTotal));
    }

    [Fact]
    public void Summary_counts_every_hidden_copy_on_the_page()
    {
        this.Page(this.New("A"), this.New("B"));
        this.Next();
        this.Page(this.New("A"), this.New("B"), this.New("C"), this.New("C"));

        Assert.Equal(
            "result set #2 (same search): 4 listings, 3 duplicates hidden, 0 hidden by other plugins | \"C\" ×2",
            this.filter.NewestSummary.Describe());
    }
}

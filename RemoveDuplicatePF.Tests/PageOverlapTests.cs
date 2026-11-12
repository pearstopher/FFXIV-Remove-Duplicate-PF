using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>
/// #84: neighbouring pages overlap when results change, and the game's Previous from the last page
/// of a filtered category lands about 10 listings early. The same listing shown on an earlier page
/// is shown again, as without the plugin; only other copies of its text are hidden.
/// </summary>
public class PageOverlapTests
{
    private readonly ListingFilter filter = new();

    // Feeds one page the way the game sends it (see CrossPageHidingTests.Page), with explicit IDs.
    private bool[] Page(int gamePageIndex, params (ulong Id, string Description)[] listings)
    {
        var result = new List<bool>();
        var segments = listings.Chunk(ResultSetBoundary.MaxListingsPerSegment).ToArray();
        for (var s = 0; s < segments.Length; s++)
        {
            var batch = s == segments.Length - 1 ? 0 : s + 1;
            foreach (var (id, description) in segments[s])
                result.Add(this.filter.Decide(batch, id, description, true, gameListEmpty: s == 0, gamePageIndex: gamePageIndex));
        }

        if (result.Any(visible => visible))
            this.filter.NoteGameListShown();

        return result.ToArray();
    }

    // Listings with IDs first..last and distinct texts.
    private static (ulong, string)[] Listings(ulong first, ulong last) =>
        Enumerable.Range((int)first, (int)(last - first + 1)).Select(i => ((ulong)i, $"Listing {i}")).ToArray();

    private DisplayedResultSet Newest() => this.filter.ForDisplayed(Array.Empty<ulong>());

    [Fact]
    public void The_same_listing_on_the_next_page_is_shown_and_not_counted()
    {
        this.Page(0, (1, "Venue"), (2, "Other"));

        Assert.Equal(new[] { true, true }, this.Page(1, (2, "Other"), (3, "New")));
        Assert.Equal(0, this.Newest().HiddenTotal);
    }

    [Fact]
    public void A_different_listing_with_the_same_text_is_still_hidden()
    {
        this.Page(0, (1, "Venue"), (2, "Other"));

        Assert.Equal(new[] { false, true }, this.Page(1, (99, "Venue"), (3, "New")));
        Assert.Equal(1, this.Newest().HiddenFromEarlierPages);
    }

    [Fact]
    public void A_repeat_shows_while_new_copies_of_its_text_stay_hidden()
    {
        this.Page(0, (1, "Venue"));

        // Page 2 repeats listing 1 (overlap) and has another copy of the same ad.
        Assert.Equal(new[] { true, false }, this.Page(1, (1, "Venue"), (99, "Venue")));
        Assert.Equal(1, this.Newest().HiddenTotal);
    }

    [Fact]
    public void A_listing_hidden_as_a_duplicate_stays_hidden_when_repeated()
    {
        // Listing 2 was hidden on page 1 as a copy of listing 1.
        Assert.Equal(new[] { true, false }, this.Page(0, (1, "Venue"), (2, "Venue")));

        Assert.Equal(new[] { false, true }, this.Page(1, (2, "Venue"), (3, "New")));
    }

    [Fact]
    public void Previous_landing_10_early_shows_the_overlap_like_the_game_does()
    {
        // The owner's Others walk-through: page 2 again started at listing 41 instead of 51.
        this.Page(0, Listings(1, 50));
        this.Page(1, Listings(51, 100));

        var page2Again = this.Page(1, Listings(41, 90));

        Assert.All(page2Again, Assert.True);
        Assert.Equal(0, this.Newest().HiddenTotal);
    }

    [Fact]
    public void A_page_made_entirely_of_earlier_listings_is_never_blank()
    {
        // The blank page from the 20:33 log: every listing had been on page 1 or 2.
        this.Page(0, Listings(1, 50));
        this.Page(1, Listings(51, 100));

        Assert.All(this.Page(2, Listings(42, 91)), Assert.True);
    }

    [Fact]
    public void A_new_search_forgets_which_listings_were_shown()
    {
        this.Page(0, (1, "Venue"));
        this.filter.NoteButtonClick(SearchStart.RefreshButton);
        this.Page(0, (5, "Other"));

        // Listing 1 is no longer known as shown, so on page 2 it's an ordinary listing again.
        Assert.Equal(new[] { true }, this.Page(1, (1, "Venue")));
    }

    [Fact]
    public void Revisiting_a_page_replaces_which_listings_it_showed()
    {
        this.Page(0, (1, "P1"));
        this.Page(1, (2, "Was on page 2"));

        // Page 2 changed: listing 2 is gone. On page 3, a different copy of its text is no longer hidden.
        this.Page(1, (3, "Now on page 2"));
        Assert.Equal(new[] { true }, this.Page(2, (4, "Was on page 2")));
    }
}

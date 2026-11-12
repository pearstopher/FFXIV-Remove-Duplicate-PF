using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>#75: listings already seen on an earlier page are hidden on later pages.</summary>
public class CrossPageHidingTests
{
    private readonly ListingFilter filter = new();
    private ulong nextId = 1;

    // Feeds one page the way the game sends it (segments of 4: 1, 2, ..., last one 0; the game's
    // list empty only during the first segment) and returns each listing's visibility.
    private bool[] Page(int gamePageIndex, params string[] descriptions)
    {
        var result = new List<bool>();
        var segments = descriptions.Chunk(ResultSetBoundary.MaxListingsPerSegment).ToArray();
        for (var s = 0; s < segments.Length; s++)
        {
            var batch = s == segments.Length - 1 ? 0 : s + 1;
            foreach (var description in segments[s])
                result.Add(this.filter.Decide(batch, this.nextId++, description, true, gameListEmpty: s == 0, gamePageIndex: gamePageIndex));
        }

        // The window shows the page before the player moves on.
        if (result.Any(visible => visible))
            this.filter.NoteGameListShown();

        return result.ToArray();
    }

    private DisplayedResultSet Newest() => this.filter.ForDisplayed(Array.Empty<ulong>());

    [Fact]
    public void Page_2_hides_listings_seen_on_page_1()
    {
        this.Page(0, "Venue", "Bingo", "Solo A");

        var page2 = this.Page(1, "Venue", "Solo B", "Bingo");

        Assert.Equal(new[] { false, true, false }, page2);
        Assert.Equal(2, this.Newest().HiddenFromEarlierPages);
        Assert.Equal(2, this.Newest().HiddenTotal);
    }

    [Fact]
    public void A_group_already_on_page_1_is_hidden_entirely_on_page_2()
    {
        this.Page(0, "Venue");

        // Two copies on page 2: no copy stays, so there's no [2] for it either.
        var page2 = this.Page(1, "Venue", "Venue", "Other");

        Assert.Equal(new[] { false, false, true }, page2);
        Assert.Empty(this.Newest().Tracker.DuplicateGroupsByDescription);
        Assert.Equal(2, this.Newest().HiddenTotal);
    }

    [Fact]
    public void Duplicates_within_a_later_page_still_work_as_before()
    {
        this.Page(0, "Page one only");

        var page2 = this.Page(1, "Dancers", "Dancers", "Dancers");

        Assert.Equal(new[] { true, false, false }, page2);
        Assert.Equal(new Dictionary<string, int> { ["Dancers"] = 3 }, this.Newest().Tracker.DuplicateGroupsByDescription);
        Assert.Equal(2, this.Newest().HiddenTotal);
    }

    [Fact]
    public void Owners_title_example_gives_7()
    {
        // Page 2 shows [3] [2] and hides 4 listings already seen on page 1: 2 + 1 + 4 = 7.
        this.Page(0, "P1 a", "P1 b", "P1 c", "P1 d");

        this.Page(1, "A", "A", "A", "B", "B", "P1 a", "P1 b", "P1 c", "P1 d", "Solo");

        var shown = this.Newest();
        Assert.Equal(4, shown.HiddenFromEarlierPages);
        Assert.Equal(3, shown.Tracker.HiddenCount);
        Assert.Equal(7, shown.HiddenTotal);
        Assert.Equal("Party Finder (7 duplicates hidden)", TitleText.Compose("Party Finder", shown.HiddenTotal));
    }

    [Fact]
    public void Page_3_hides_listings_from_pages_1_and_2()
    {
        this.Page(0, "From page 1");
        this.Page(1, "From page 2");

        var page3 = this.Page(2, "From page 1", "From page 2", "New on 3");

        Assert.Equal(new[] { false, false, true }, page3);
    }

    [Fact]
    public void Page_1_is_never_affected_by_other_pages()
    {
        this.Page(0, "A");
        this.Page(1, "B");

        // Back to page 1 (Previous or Refresh): B is shown again, even though page 2 had it.
        var page1 = this.Page(0, "A", "B");

        Assert.Equal(new[] { true, true }, page1);
        Assert.Equal(0, this.Newest().HiddenFromEarlierPages);
    }

    [Fact]
    public void Page_1_resets_the_memory_of_later_pages()
    {
        this.Page(0, "Old search A");
        this.Page(1, "Old search B");

        // A new search (or Refresh) starts at page 1; its page 2 hides only the new page 1.
        this.Page(0, "New search A");
        var page2 = this.Page(1, "Old search A", "Old search B", "New search A");

        Assert.Equal(new[] { true, true, false }, page2);
    }

    [Fact]
    public void Revisiting_a_page_replaces_its_entry()
    {
        this.Page(0, "P1");
        this.Page(1, "Was on page 2");
        this.Page(2, "P3");

        // Back to page 2: its contents changed in the meantime.
        this.Page(1, "Now on page 2");
        var page3 = this.Page(2, "Was on page 2", "Now on page 2");

        Assert.Equal(new[] { true, false }, page3);
    }

    [Fact]
    public void Matching_uses_the_first_30_characters()
    {
        this.Page(0, "Come to my venue tonight! Doors 9pm ST");

        var page2 = this.Page(1, "Come to my venue tonight! Doors 10pm ST");

        Assert.Equal(new[] { false }, page2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_descriptions_are_never_hidden_across_pages(string? description)
    {
        this.Page(0, description!);

        Assert.Equal(new[] { true }, this.Page(1, description!));
    }

    [Fact]
    public void Listings_hidden_by_another_plugin_dont_count_as_seen()
    {
        // Another plugin hid "Spam" on page 1, so the player never saw it there.
        this.filter.Decide(1, 1, "Spam", currentlyVisible: false, gameListEmpty: true, gamePageIndex: 0);
        this.filter.Decide(0, 2, "Other", currentlyVisible: true, gameListEmpty: false, gamePageIndex: 0);

        Assert.Equal(new[] { true }, this.Page(1, "Spam"));
    }

    [Fact]
    public void Unknown_page_counts_as_page_1_and_hides_nothing_extra()
    {
        this.Page(0, "A");

        // The page index couldn't be read: treated as page 1, so nothing is hidden for being on page 1.
        var result = new List<bool>
        {
            this.filter.Decide(1, 500, "A", true, gameListEmpty: true, gamePageIndex: null),
        };

        Assert.Equal(new[] { true }, result);
        Assert.Equal(1, this.Newest().Page);
    }

    [Fact]
    public void Untrusted_page_index_disables_cross_page_hiding()
    {
        this.filter.Pages.Verify(2, "1-42");
        this.filter.Pages.Verify(2, "1-42"); // two mismatches: the index is no longer trusted

        this.Page(0, "A");
        Assert.Equal(new[] { true }, this.Page(1, "A")); // "page 2" is treated as page 1
    }

    [Fact]
    public void Summary_reports_listings_hidden_from_earlier_pages()
    {
        this.Page(0, "A", "B");
        this.Page(1, "A", "B", "C", "C");

        Assert.Equal(
            "result set #2 (page 2): 4 listings, 1 hidden as duplicates in 1 groups, 2 hidden from earlier pages, 0 hidden by other plugins | \"C\" ×2",
            this.filter.NewestSummary.Describe());
    }
}

using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>
/// #80: Previous back to page 1 keeps the search's memory of later pages; a new search
/// (window opened, Refresh, category change) clears it.
/// </summary>
public class NewSearchTests
{
    private const int CategoryTab = 22;

    private readonly ListingFilter filter = new();
    private ulong nextId = 1;

    // Feeds one page the way the game sends it (see CrossPageHidingTests.Page).
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

        if (result.Any(visible => visible))
            this.filter.NoteGameListShown();

        return result.ToArray();
    }

    // The owner's walk-through: open the window, page 1, Next to page 2 (which has "From page 2").
    private void OpenAndGoToPage2()
    {
        this.filter.NoteWindowOpened();
        this.Page(0, "From page 1");
        this.filter.NoteButtonClick(SearchStart.NextButton);
        this.Page(1, "From page 2");
    }

    [Fact]
    public void Previous_back_to_page_1_keeps_page_2_in_memory()
    {
        this.OpenAndGoToPage2();

        this.filter.NoteButtonClick(SearchStart.PreviousButton);
        this.Page(0, "From page 1");
        this.filter.NoteButtonClick(SearchStart.NextButton);
        this.Page(1, "From page 2");
        this.filter.NoteButtonClick(SearchStart.NextButton);

        // Page 3 still hides what page 2 showed, and page 1.
        Assert.Equal(new[] { false, false, true }, this.Page(2, "From page 2", "From page 1", "New on 3"));
    }

    [Fact]
    public void Previous_back_to_page_1_says_same_search_in_the_log()
    {
        this.OpenAndGoToPage2();

        this.filter.NoteButtonClick(SearchStart.PreviousButton);
        this.Page(0, "From page 1");

        Assert.False(this.filter.NewestSummary.NewSearch);
        Assert.StartsWith("result set #3 (page 1, same search):", this.filter.NewestSummary.Describe());
    }

    [Theory]
    [InlineData(SearchStart.RefreshButton)]
    [InlineData(CategoryTab)]
    public void Refresh_or_a_category_change_clears_the_memory(int button)
    {
        this.OpenAndGoToPage2();
        this.filter.NoteButtonClick(SearchStart.NextButton);
        this.Page(2, "From page 3");

        this.filter.NoteButtonClick(button);
        this.Page(0, "New page 1");
        Assert.True(this.filter.NewestSummary.NewSearch);
        Assert.StartsWith("result set #4 (page 1, new search):", this.filter.NewestSummary.Describe());

        // Page 2 of the new search hides only the new page 1.
        this.filter.NoteButtonClick(SearchStart.NextButton);
        Assert.Equal(new[] { true, true, false }, this.Page(1, "From page 1", "From page 3", "New page 1"));
    }

    [Fact]
    public void Reopening_the_window_clears_the_memory()
    {
        this.OpenAndGoToPage2();

        this.filter.NoteWindowOpened();
        this.Page(0, "Another page 1");
        this.filter.NoteButtonClick(SearchStart.NextButton);

        Assert.Equal(new[] { true }, this.Page(1, "From page 1"));
    }

    [Fact]
    public void A_background_refetch_of_page_1_keeps_the_memory()
    {
        // #51: while page 2 is on screen, the game re-downloads page 1 with no click.
        this.OpenAndGoToPage2();
        this.Page(0, "From page 1");

        this.filter.NoteButtonClick(SearchStart.NextButton);
        this.Page(1, "From page 2");
        this.filter.NoteButtonClick(SearchStart.NextButton);

        Assert.Equal(new[] { false }, this.Page(2, "From page 2"));
    }

    [Fact]
    public void Later_pages_are_never_labelled_a_new_search()
    {
        this.OpenAndGoToPage2();

        Assert.False(this.filter.NewestSummary.NewSearch);
        Assert.StartsWith("result set #2 (page 2):", this.filter.NewestSummary.Describe());
    }
}

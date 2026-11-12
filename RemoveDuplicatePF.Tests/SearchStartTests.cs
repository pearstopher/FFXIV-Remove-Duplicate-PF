using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>#80: telling a new search's page 1 from going back to page 1.</summary>
public class SearchStartTests
{
    private const int CategoryTab = 22;

    private readonly SearchStart start = new();

    [Fact]
    public void Opening_the_window_is_a_new_search()
    {
        this.start.NoteWindowOpened();

        Assert.True(this.start.TakeIsNewSearch());
    }

    [Theory]
    [InlineData(SearchStart.RefreshButton)]
    [InlineData(CategoryTab)]
    [InlineData(33)]
    public void Refresh_or_a_category_tab_is_a_new_search(int button)
    {
        this.start.NoteWindowOpened();
        this.start.TakeIsNewSearch();

        this.start.NoteButtonClick(button);

        Assert.True(this.start.TakeIsNewSearch());
    }

    [Theory]
    [InlineData(SearchStart.PreviousButton)]
    [InlineData(SearchStart.NextButton)]
    public void Previous_or_Next_is_the_same_search(int button)
    {
        this.start.NoteWindowOpened();
        this.start.TakeIsNewSearch();

        this.start.NoteButtonClick(button);

        Assert.False(this.start.TakeIsNewSearch());
    }

    [Fact]
    public void Only_the_last_click_counts()
    {
        this.start.NoteWindowOpened();
        this.start.TakeIsNewSearch();

        this.start.NoteButtonClick(SearchStart.RefreshButton);
        this.start.NoteButtonClick(SearchStart.PreviousButton);

        Assert.False(this.start.TakeIsNewSearch());
    }

    [Fact]
    public void A_click_is_used_by_one_result_set_only()
    {
        this.start.NoteWindowOpened();
        this.start.TakeIsNewSearch();
        this.start.NoteButtonClick(SearchStart.RefreshButton);
        this.start.TakeIsNewSearch();

        // Next result set with no click in between: a background re-fetch, not another refresh.
        Assert.False(this.start.TakeIsNewSearch());
    }

    [Fact]
    public void A_background_refetch_after_paging_is_the_same_search()
    {
        this.start.NoteWindowOpened();
        this.start.TakeIsNewSearch();
        this.start.NoteButtonClick(SearchStart.NextButton);
        this.start.TakeIsNewSearch();

        Assert.False(this.start.TakeIsNewSearch());
    }

    [Fact]
    public void Without_any_clicks_every_result_set_is_a_new_search()
    {
        // Clicks may not be reaching the plugin: fall back to the #75 behaviour.
        Assert.True(this.start.TakeIsNewSearch());
        Assert.True(this.start.TakeIsNewSearch());
    }

    [Fact]
    public void Reopening_the_window_forgets_earlier_clicks()
    {
        this.start.NoteWindowOpened();
        this.start.NoteButtonClick(SearchStart.NextButton);
        this.start.TakeIsNewSearch();

        this.start.NoteWindowOpened();
        this.start.TakeIsNewSearch();

        // No click since reopening: can't tell, so a new search.
        Assert.True(this.start.TakeIsNewSearch());
    }

    [Fact]
    public void Opening_the_window_wins_over_a_paging_click()
    {
        this.start.NoteButtonClick(SearchStart.PreviousButton);
        this.start.NoteWindowOpened();

        Assert.True(this.start.TakeIsNewSearch());
    }
}

namespace RemoveDuplicatePF.Core;

/// <summary>
/// Tells whether a page-1 result set starts a new search (window opened, Refresh, category
/// change) or continues the same search (Previous from page 2, or a background re-fetch) (#80).
/// </summary>
/// <remarks>
/// The page index alone can't tell: Previous and Refresh on page 2 both go to page 1. Instead
/// the Party Finder window's button clicks are noted as they happen, about a tenth of a second
/// before the requested results arrive. Event numbers seen in game (2026-11-11): Next 9,
/// Previous 10, Refresh 6, category tabs one number each (22, 33, ...). Only Next and Previous are
/// matched exactly; any other click counts as starting a new search, so a missed or renumbered
/// button errs towards today's behaviour (clearing the memory), never towards keeping a stale one.
/// </remarks>
public sealed class SearchStart
{
    /// <summary>The window's Next button event number.</summary>
    public const int NextButton = 9;

    /// <summary>The window's Previous button event number.</summary>
    public const int PreviousButton = 10;

    /// <summary>The window's Refresh button event number (for reference and tests).</summary>
    public const int RefreshButton = 6;

    private bool windowOpened;
    private int? lastClick;
    private bool clickSeenSinceOpen;

    /// <summary>Call when the Party Finder window opens.</summary>
    public void NoteWindowOpened()
    {
        this.windowOpened = true;
        this.lastClick = null;
        this.clickSeenSinceOpen = false;
    }

    /// <summary>Call for each button click in the Party Finder window, with its event number.</summary>
    public void NoteButtonClick(int eventParam)
    {
        this.lastClick = eventParam;
        this.clickSeenSinceOpen = true;
    }

    /// <summary>
    /// Call once when a result set starts. Whether it starts a new search, judged from what happened
    /// since the previous result set started. Only meaningful for page 1.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The window opened: new search.</item>
    /// <item>The last click was Next or Previous: same search.</item>
    /// <item>The last click was anything else (Refresh, a category tab, ...): new search.</item>
    /// <item>No click: the game re-fetched on its own (#51), so the same search, but only if clicks
    /// have been seen since the window opened. Otherwise the clicks may not be reaching the plugin,
    /// so it's treated as a new search.</item>
    /// </list>
    /// </remarks>
    public bool TakeIsNewSearch()
    {
        var isNew = this.windowOpened
            || (this.lastClick is { } click
                ? click is not (NextButton or PreviousButton)
                : !this.clickSeenSinceOpen);

        this.windowOpened = false;
        this.lastClick = null;
        return isNew;
    }
}

using RemoveDuplicatePF.Core;

namespace RemoveDuplicatePF.Tests;

/// <summary>
/// A result set that fits in one packet (4 listings or fewer) arrives as a lone packet 0, straight
/// after a set that also ended with packet 0 (#72, #75). Since #94 it's told apart by the plugin's
/// own knowledge: once a set's last packet has arrived and a frame has passed, the set is complete.
/// Nothing on screen is used, so the plugin's own hiding can't confuse it.
/// </summary>
public class SmallResultSetTests
{
    private readonly ResultSetBoundary boundary = new();

    // Feeds one packet: Dalamud raises all of its listings together, within one frame.
    private bool[] Packet(int batch, params ulong[] ids) =>
        ids.Select(id => this.boundary.IsNewResultSet(batch, id)).ToArray();

    // A normal 10-listing page: packets 1, 2, then the last packet 0, then the page is shown.
    private void FullPage(ulong firstId)
    {
        this.Packet(1, firstId, firstId + 1, firstId + 2, firstId + 3);
        this.Packet(2, firstId + 4, firstId + 5, firstId + 6, firstId + 7);
        this.Packet(0, firstId + 8, firstId + 9);
        this.boundary.NoteFrame();
    }

    [Fact]
    public void Lone_packet_0_after_a_complete_page_is_a_new_set()
    {
        // The owner's 19:26:17 case (#72): Others page, then a category with only 2 listings.
        this.FullPage(100);

        Assert.Equal(new[] { true, false }, this.Packet(0, 900, 901));
    }

    [Fact]
    public void Without_a_frame_in_between_the_packet_numbers_alone_cant_tell()
    {
        // Documents why the frame is needed: by packet numbers and IDs alone it looks like the
        // rest of the previous set. In game a new request always takes more than a frame.
        this.Packet(1, 100, 101, 102, 103);
        this.Packet(0, 104, 105);

        Assert.Equal(new[] { false, false }, this.Packet(0, 900, 901));
    }

    [Fact]
    public void A_whole_lone_packet_is_one_set()
    {
        this.FullPage(100);

        Assert.Equal(new[] { true, false, false, false }, this.Packet(0, 900, 901, 902, 903));
    }

    [Fact]
    public void Two_small_sets_in_a_row_are_separate()
    {
        this.FullPage(100);
        this.Packet(0, 900, 901);
        this.boundary.NoteFrame();

        Assert.Equal(new[] { true, false, false }, this.Packet(0, 950, 951, 952));
    }

    [Fact]
    public void Frames_between_the_packets_of_a_page_dont_split_it()
    {
        // Packets of one page can arrive over several frames; only the last packet (0) ends it.
        var result = new List<bool>();
        result.AddRange(this.Packet(1, 1, 2, 3, 4));
        this.boundary.NoteFrame();
        result.AddRange(this.Packet(2, 5, 6, 7, 8));
        this.boundary.NoteFrame();
        this.boundary.NoteFrame();
        result.AddRange(this.Packet(0, 9, 10));

        Assert.True(result[0]);
        Assert.All(result.Skip(1), Assert.False);
    }

    [Fact]
    public void Final_packet_0_within_a_page_is_not_split()
    {
        var result = new List<bool>();
        result.AddRange(this.Packet(1, 1, 2, 3, 4));
        result.AddRange(this.Packet(2, 5, 6, 7, 8));
        result.AddRange(this.Packet(0, 9, 10));

        Assert.True(result[0]);
        Assert.All(result.Skip(1), Assert.False);
    }

    [Fact]
    public void A_frame_before_any_listing_changes_nothing()
    {
        this.boundary.NoteFrame();

        Assert.Equal(new[] { true, false }, this.Packet(0, 1, 2));
    }

    [Fact]
    public void A_page_with_every_listing_hidden_doesnt_swallow_the_next_small_set()
    {
        // #94: page 2 is all copies of page 1's ads, so nothing on it is visible. Then a category
        // tab with 2 listings, one a copy of an old ad. It must be a new set and a new search.
        var filter = new ListingFilter();
        filter.NoteWindowOpened();
        GameFeed.Page(filter, [(1, "Venue"), (2, "Bingo"), (3, "Other")]);

        filter.NoteButtonClick(SearchStart.NextButton);
        Assert.All(GameFeed.Page(filter, [(4, "Venue"), (5, "Bingo"), (6, "Venue")]), Assert.False);

        filter.NoteButtonClick(22); // a category tab
        Assert.Equal(new[] { true, true }, GameFeed.Page(filter, [(7, "Venue"), (8, "New category")]));

        var summary = filter.NewestSummary;
        Assert.Equal(3, summary.ResultSetNumber);
        Assert.Equal(2, summary.Received);
        Assert.True(summary.NewSearch);
    }

    [Fact]
    public void A_page_another_plugin_hid_entirely_doesnt_swallow_the_next_small_set()
    {
        var filter = new ListingFilter();
        filter.Decide(0, 1, "Spam", currentlyVisible: false);
        filter.Decide(0, 2, "Spam too", currentlyVisible: false);
        filter.NoteFrame();

        Assert.True(filter.Decide(0, 900, "Next set", true));
        Assert.Equal(2, filter.NewestSummary.ResultSetNumber);
    }

    [Fact]
    public void Small_set_after_a_page_doesnt_inherit_the_previous_pages_groups()
    {
        var filter = new ListingFilter();

        // Previous page: a pair of "Venue" ads.
        GameFeed.Page(filter, [(1, "Venue"), (2, "Venue"), (3, "Other"), (4, "More"), (5, "Last")]);

        // New category with 2 listings, one of them the same venue ad. No clicks were noted, so it's
        // a new search.
        Assert.Equal(new[] { true, true }, GameFeed.Page(filter, [(900, "Venue"), (901, "Something else")]));

        var summary = filter.NewestSummary;
        Assert.Equal(2, summary.ResultSetNumber);
        Assert.Equal(2, summary.Received);
        Assert.Equal(0, summary.Hidden);
    }
}

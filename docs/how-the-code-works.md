# How the code works

This page explains the code that ships inside the plugin and runs in the game while you use the Party Finder. It leaves out tests, build files and anything else that only lives in the repo.

> **Keep this page accurate.** Any change to a file in `RemoveDuplicatePF/` or `RemoveDuplicatePF.Core/` must update this page in the same PR, including new, renamed and deleted files. A unit test (`HowTheCodeWorksDocTests`) fails if a source file is missing from this page or the page names a file that no longer exists.

## The big picture

The code is split into two parts:

- **[RemoveDuplicatePF/](../RemoveDuplicatePF/)**: the plugin itself. It talks to the game through Dalamud (the plugin framework): it hears about listings arriving, reads button clicks, and changes the text in the Party Finder window.
- **[RemoveDuplicatePF.Core/](../RemoveDuplicatePF.Core/)**: the decisions. Which listings are duplicates, which copy to keep, what number to show. It knows nothing about the game, which is what lets it be unit tested outside the game.

Both are compiled into the plugin and both run in game.

### What happens when you use the Party Finder

1. **You open the Party Finder or click a button.** The plugin notes it: opening the window, Refresh or a category tab starts a *new search*; Next and Previous stay in the *same search*.
2. **The server sends the listings for a page.** They arrive in small packets of up to 4. For every listing, before the game draws it, the plugin decides *show* or *hide*:
   - It works out whether this listing begins a new page of results (a "result set").
   - It compares the first 30 characters of the description with every listing seen so far in this search.
   - The first copy of an ad is shown. Every later copy, on this page or any other, is hidden.
   - A listing another plugin already hid stays hidden and isn't counted.
3. **The window draws the page.** About four times a second, and whenever the list changes, the plugin:
   - works out which page is actually on screen (the game sometimes fetches a page in the background);
   - adds a yellow **[N]** in front of each shown listing that has copies (N = total copies found so far in this search);
   - adds **(N duplicates hidden)** to the window title.
4. **The plugin is unloaded.** It stops listening and puts the original row text and title back.

```
 Server packets ──► PartyFinderListener ──► ListingFilter.Decide ──► show / hide
                                               │  ResultSetBoundary  (new page?)
 Button clicks ──► SearchEvents ──────────────►│  SearchStart        (new search?)
                                               │  DuplicateTracker   (duplicate? keep which?)
                                               │    └ DescriptionMatch (first 30 characters)
                                               ▼
                                     remembers the last 4 pages
                                               │
 Window redraw ──► DisplayedListings ──────────┘ (which page is on screen?)
                     ├─► ListingPrefixer ──► [N] on rows   (RowPrefixMatcher, CountPrefix)
                     └─► TitleUpdater    ──► title count   (TitleText)
```

## The plugin: `RemoveDuplicatePF/`

These files connect the game to the decision logic.

### [Plugin.cs](../RemoveDuplicatePF/Plugin.cs): start and stop

The entry point Dalamud calls when the plugin loads.

- **On load:** creates one shared `ListingFilter` (the brain, see below) and hands it to the four parts that talk to the game: `PartyFinderListener`, `ListingPrefixer`, `TitleUpdater` and `SearchEvents`. Writes a line to the log saying which build loaded and when it was built.
- **On unload:** shuts each part down in turn. If one fails, the others are still shut down, so nothing is left attached to the game.
- The plugin has no settings window, commands or menus.

### [PartyFinderListener.cs](../RemoveDuplicatePF/PartyFinderListener.cs): show or hide each listing

The only place where listings are actually hidden.

- **Every listing received:** asks `ListingFilter.Decide` and writes the answer to the listing's "visible" flag. Dalamud then shows or hides the listing.
- **Every frame:** tells the filter a frame has passed. This is how it tells where one page of results ends and the next begins.
- **After the last packet of a page:** writes one summary line to the Debug log (see `ResultSetSummary`).

### [SearchEvents.cs](../RemoveDuplicatePF/SearchEvents.cs): watch buttons and window opens

Only watches; it never blocks or changes a click.

- When the Party Finder window opens, it tells the filter (this always starts a new search).
- When a button in the window is clicked, it passes the button's number to the filter, so it can tell Next/Previous apart from Refresh or a category tab.

### [DisplayedListings.cs](../RemoveDuplicatePF/DisplayedListings.cs): which page is on screen?

The game can fetch another page in the background while you're still looking at the old one. So instead of assuming "the newest page", this reads the IDs of the listings the window is actually showing and asks the filter which remembered page they belong to. The prefixes and the title both use this answer.

### [ListingPrefixer.cs](../RemoveDuplicatePF/ListingPrefixer.cs): the [N] in front of rows

Adds the yellow `[N] ` to the shown copy of each duplicated ad.

- **When it runs:** whenever the list changes, and otherwise at most every quarter second.
- **How it finds rows:** it walks through every piece of text in the Party Finder window and compares it to the descriptions of ads that have copies (using `RowPrefixMatcher`). Matching by text means it doesn't depend on row order or layout. The window reuses rows as you scroll, so it checks again each time.
- **Changing text:** before adding a prefix it saves the row's original text. If the number changes (more copies found on a later page), it rebuilds the row from the saved original rather than stacking prefixes.
- **Skipping work:** a row whose text and numbers haven't changed since last time is skipped.
- **On unload:** finds every row it prefixed and puts the original text back.
- **Debug log only:** notes rows that look almost like a duplicated ad but didn't match, and a summary of which groups got a prefix, to help track down a missing `[N]`.

### [TitleUpdater.cs](../RemoveDuplicatePF/TitleUpdater.cs): the count in the title

Changes the window title to e.g. **Party Finder (7 duplicates hidden)**, with the count in the same yellow as the prefixes.

- **When it runs:** whenever the list changes, and otherwise every quarter second, because the game sometimes rewrites the title.
- The number is how many listings were hidden on the page on screen.
- It only writes to the title when the text is actually different.
- If it can't find the title for about 5 seconds, it writes one warning to the log.
- **On unload:** puts the game's own title back.

## The decisions: `RemoveDuplicatePF.Core/`

These files hold the rules. None of them touch the game directly.

### [ListingFilter.cs](../RemoveDuplicatePF.Core/ListingFilter.cs): the brain

The single object every plugin part shares. It ties the other Core pieces together.

- **`Decide`** (called for every listing): checks whether this listing starts a new page (`ResultSetBoundary`). If so, it checks whether that page starts a new search (`SearchStart`), and if it does, forgets all earlier listings. Then it asks `DuplicateTracker` whether this listing is the first copy or a later one, and records the answer on the current page.
- **Remembers the last 4 pages**, each with the listings it showed and how many it hid. That way the title and prefixes can describe the page on screen, even if a newer page has arrived in the background.
- **`ForDisplayed`**: given the listing IDs the window is showing, picks the remembered page that matches best. The game keeps 50 listing slots but doesn't clear the unused ones on a short page, so only the slots actually on screen are compared.
- Works out the `[N]` for each shown listing: how many copies of it have been found so far in the search, including on other pages.
- Never un-hides a listing another plugin hid.

### [DuplicateTracker.cs](../RemoveDuplicatePF.Core/DuplicateTracker.cs): which copy to keep

Keeps the list of ads seen in the current search.

- The first listing with a given description (see `DescriptionMatch`) is kept and shown. Every later one is hidden and adds 1 to the kept listing's count.
- The same listing arriving again (going back a page, or pages overlapping) gets the same answer as before and isn't counted twice.
- Listings with an empty description are never treated as duplicates.
- Cleared when a new search starts.

### [DescriptionMatch.cs](../RemoveDuplicatePF.Core/DescriptionMatch.cs): what counts as "the same ad"

Two listings are the same ad if their descriptions share the **first 30 characters** exactly (capitals matter). Shorter descriptions must match in full. This is deliberate: ads that share a long opening (a template, a repost with a different time) are treated as one ad. See [the design decision](party-finder-notes.md#design-decision-match-on-the-first-30-characters-42).

### [ResultSetBoundary.cs](../RemoveDuplicatePF.Core/ResultSetBoundary.cs): where a page starts

Listings arrive one at a time, so the plugin has to work out when a new page of results begins. The server sends a page in packets of up to 4 listings, numbered 1, 2, 3, … with the last packet numbered 0. A new page starts when:

- the previous page's last packet (0) arrived and at least one frame has passed since (the main rule);
- the packet number goes back to 1, or changes away from 0;
- a listing ID repeats within the page (the same listings were sent again);
- more listings arrive with the same packet number than one packet can hold.

It only looks at what the plugin received, never at the window, because the window shows what's left after hiding.

### [SearchStart.cs](../RemoveDuplicatePF.Core/SearchStart.cs): new search or same search?

When a new page starts, this decides whether it's a new search (forget everything) or the same search continuing (keep counting). It goes by what happened since the last page:

| What happened | Result |
| --- | --- |
| The window opened | New search |
| Last click was **Next** or **Previous** | Same search |
| Last click was anything else (Refresh, a category tab, …) | New search |
| No click, but clicks have reached the plugin since the window opened | Same search (the game fetched on its own) |
| No click, and no clicks seen since the window opened | New search (to be safe) |

Anything unexpected counts as a new search, so the worst case is forgetting too early, never mixing two searches.

### [RowPrefixMatcher.cs](../RemoveDuplicatePF.Core/RowPrefixMatcher.cs): which row is which ad

Given the text of a row in the window, works out whether it's the shown copy of a duplicated ad. A row matches when its text is:

- exactly the description;
- the description with game tags in front, like `[Practice] `;
- a shortened version ending in `...` or `…` that only one ad starts with;
- any of the above with the plugin's own `[N] ` in front.

It also says what to do with the row: add the prefix, leave it (already correct), or rebuild it (the number is out of date). It also finds rows to restore on unload, and spots near misses for the Debug log.

### [CountPrefix.cs](../RemoveDuplicatePF.Core/CountPrefix.cs): the [N] text and colour

Builds the prefix text, e.g. `[3] `, and picks its colour (the game's yellow, colour 500). Nothing is shown for an ad with only one copy.

### [TitleText.cs](../RemoveDuplicatePF.Core/TitleText.cs): the title wording

- Builds the title text: `(1 duplicate hidden)` or `(N duplicates hidden)`, or nothing when none are hidden.
- Removes the plugin's own text from the title to recover the game's title, so the count is replaced rather than added again. It also removes the wording older versions used.

### [ResultSetSummary.cs](../RemoveDuplicatePF.Core/ResultSetSummary.cs): the Debug log line

Formats the one line per page written to the Debug log, for example:

```
PF result set #7 (same search): 50 listings, 5 duplicates hidden, 1 hidden by other plugins | "BUNN$BEAT club night" ×3
```

Duplicated ads are named by their first 30 characters only.

### [BuildInfo.cs](../RemoveDuplicatePF.Core/BuildInfo.cs): the "loaded" log line

Formats the build details written when the plugin loads (Debug or Release, when it was built, where the file is), so an old or wrong build is easy to spot in `/xllog`.

### [CoreAssembly.cs](../RemoveDuplicatePF.Core/CoreAssembly.cs): a marker

Empty. It gives tests a way to check that the Core part never depends on the game.

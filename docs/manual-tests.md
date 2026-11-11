# Manual in-game tests

Unit tests cover the logic in Core. These steps cover what only the game can show. Run the relevant section after building a change, following [DEVELOPMENT.md](../DEVELOPMENT.md) to load the dev build.

Record the result in the PR description: pass or fail, plus anything odd you noticed.

## Setup for every session

Follow **"Before a session"** in [in-game-testing.md](in-game-testing.md). It covers building, checking the `loaded (...)` build line, setting `/xllog` to **Debug+**, and keeping `dalamud.log` from filling up. With Debug+ on, each listing logs a line like:

```
PF batch=1 id=123456 secs=3200 visible=False text=Come to my venue…
```

Report results in the format under "After a session" in that guide.

---

## Hide duplicate listings (#7)

1. Open Party Finder (Duty menu > Party Finder) and pick a busy category, for example **Others**, or **All**.
2. Look for listings whose descriptions start the same way, such as venue ads. Listings count as duplicates when their **first 30 characters** match (shorter descriptions must match in full).
   - **Expected:** only one listing per duplicate group is shown, and it is the first one the game would have listed. In the Debug log, no two `visible=true` lines in the same result set have the same `text=`.
3. Find listings with an **empty** description.
   - **Expected:** they are all still shown.
4. With Debug logging on, check the log.
   - **Expected:** one `PF batch=…` line per listing, `visible=False` only on repeat descriptions, and no errors.
5. Disable the plugin in `/xlplugins`, then close and reopen Party Finder.
   - **Expected:** all duplicates are visible again, and `Remove Duplicate PF unloaded` is in the log.
6. Use the game normally: open a listing's details, join or leave a party, open other windows.
   - **Expected:** nothing outside the Party Finder list behaves differently.

### Also record (answers open questions in `docs/party-finder-notes.md`)

From the Debug log during step 2:

- [ ] Sequence of `batch=` numbers for one load (does it start at 1 and end at 0?)
- [ ] Number of listings per page
- [ ] Does the order of log lines match the order of listings shown in the window?

## Reset on refresh, search and page change (#8)

Counts are **per page**: duplicates on different pages are not combined (see `docs/party-finder-notes.md`, section 6).

1. Open a busy category where duplicates are hidden (as in the #7 steps).
2. Click **Refresh** (or wait for the list to refresh) several times.
   - **Expected:** each time, exactly one copy of each duplicate description is shown. Listings don't progressively disappear.
3. Switch to another category, then back.
   - **Expected:** the same as step 2, with no listings missing from the category you came back to.
4. Change the search filters (for example **Data Center** vs **World**, or a duty filter), then clear them.
   - **Expected:** duplicates are combined correctly for each search, and nothing stays hidden after clearing.
5. If there's more than one page, go to the next page and back.
   - **Expected:** each page shows one copy per duplicate group *on that page*. A description that also appears on the previous page is still shown once on this page.
6. With Debug+ logging, record the `batch=` sequence for one refresh and one page change.
   - **Expected:** each starts again at `batch=1`, and the last segment is `batch=0`.

### Also record

- [ ] Did any result set arrive as a single segment numbered 0? The code handles it, but it isn't confirmed in game.

## Coloured [N] prefix (#9)

1. Open a busy category with duplicates (as in the #7 steps).
   - **Expected:** each listing that stands in for a duplicate group shows a coloured `[N]` at the start of its description, where N is the number of listings in its duplicate group (including itself). The colour differs from the normal description text.
2. Look at listings that have no duplicates.
   - **Expected:** no `[N]` prefix.
3. Scroll the list up and down several times, and switch between the standard and compact list views if available.
   - **Expected:** prefixes stay on the right listings, never stack (no `[3] [3] …`), and never appear on a listing without duplicates.
4. Refresh, change category, and change page.
   - **Expected:** prefixes update to match the new results; stale counts don't stay on screen.
5. With Party Finder open, disable the plugin in `/xlplugins`.
   - **Expected:** the `[N]` prefixes disappear straight away (hidden duplicates come back on the next refresh).
6. With Debug+ logging, check for `PF prefix: X rows matched for Y duplicate groups` lines.
   - **Expected:** X is roughly equal to Y while the matching rows are on screen. **If X stays 0 while there are duplicate groups,** the row text doesn't match the descriptions. Report it, and note what a duplicate's row looks like (a screenshot helps).
7. Open a prefixed listing's details.
   - **Expected:** the details window shows the original description without the prefix (expected and fine).

## Duplicate count in the window title (#10)

The count is for the **page currently shown**.

1. Open a busy category with duplicates.
   - **Expected:** the title reads `Party Finder (N duplicates combined)`, where N matches the number of `visible=false` lines for that result set in the Debug log. It says `1 duplicate combined` when N is 1.
2. Open a category or page with no duplicates.
   - **Expected:** the title is just `Party Finder`.
3. Refresh, change category and change page a few times.
   - **Expected:** the count updates each time. It never stacks (no `(3 duplicates combined) (3 duplicates combined)`).
4. Close Party Finder and reopen it.
   - **Expected:** the title starts as `Party Finder` and gains the count once listings load.
5. With Party Finder open and showing a count, disable the plugin in `/xlplugins`.
   - **Expected:** the title goes back to `Party Finder` straight away.
6. With Debug+ logging, look for `PF title:` lines.
   - **Expected:** one line each time the count changes. **If you see `Party Finder title text node not found`,** report it; the title count is then disabled but nothing else is affected.

7. **Background re-fetch (#51):** go to page 2 with Next, then wait about 20 seconds without touching anything.
   - **Expected:** the title keeps matching page 2. Add up the prefixes on the page (each `[N]` counts N − 1) and compare with the title. The game may re-download page 1 in the background; that must not change the title or prefixes for page 2.
   - In the Debug log, `PF on screen: result set #N (older, M listings matched…)` means the plugin noticed a background fetch and kept using the page on screen. **If M is always 0,** the game's displayed-listings list isn't what we expected; report it.

### Also record

- [ ] Does the window show a **subtitle** under or next to the title? If so, what does it say?

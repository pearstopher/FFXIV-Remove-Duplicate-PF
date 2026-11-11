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
2. Look for listings with identical descriptions, such as venue ads.
   - **Expected:** only one copy of each identical description is shown, and it is the first one the game would have listed.
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

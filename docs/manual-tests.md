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

> Until #8 lands, results are only reliable on the **first** load after opening Party Finder. Refresh and page changes aren't handled yet.

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

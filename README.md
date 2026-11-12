<img src="images/icon.png" alt="Remove Duplicate PF icon" width="96" align="right">

# Remove Duplicate PF

[![Build and test](https://github.com/pearstopher/FFXIV-Remove-Duplicate-PF/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/pearstopher/FFXIV-Remove-Duplicate-PF/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/pearstopher/FFXIV-Remove-Duplicate-PF)](https://github.com/pearstopher/FFXIV-Remove-Duplicate-PF/releases/latest)

Hide duplicate Party Finder ads in FINAL FANTASY XIV (FFXIV / FF14). This [Dalamud](https://github.com/goatcorp/Dalamud) plugin cleans up the Party Finder: when the same ad is posted several times, as often happens with spam, venue ads and recruitment ads, you see it **once**, marked with how many copies there were.

There are no settings: install it, enable it, and open Party Finder.

**How do I hide duplicate Party Finder ads in FF14?** Install this plugin through XIVLauncher (see [Install](#install)). Repeated listings are combined into one automatically.

## What it does

- **Shows each ad once.** Listings whose descriptions start with the same 30 characters count as copies of the same ad. Only the first copy you come across stays visible; every other copy is hidden, on every page.
- **Marks it** with a coloured **`[N]`** in front of its description, where N is the number of copies found so far. For example, three copies of a venue ad become one listing showing **`[3]`** `Come to my venue tonight…`. Copies found on later pages count too: if page 1 shows `[2]` and page 2 has 3 more copies, going back to page 1 shows `[5]`. Each listing is counted once, however often you page back and forth.
- **Starts over with each new search.** Opening the window, pressing Refresh or changing category starts a new search; Next and Previous don't. If the copy being shown is taken down, its other copies come back when you press Refresh.
- **Shows how many listings are hidden in the window title** in the same colour, for example **`Party Finder (7 duplicates hidden)`**, for the page you're looking at. It counts the copies hidden on that page. So `[3]` and `[2]` on page 2, plus 4 copies of ads already shown on page 1, make 2 + 1 + 4 = 7.
- **Leaves listings with an empty description alone.** They're never combined or hidden.

**About the 30-character rule:** ads that start with the same 30 characters are treated as the same ad, even if their endings differ, for example the same venue ad posted with different times. That's deliberate; see [the design decision](docs/party-finder-notes.md#design-decision-match-on-the-first-30-characters-42). Short descriptions (under 30 characters) must match exactly.

The plugin only changes what the Party Finder list and title show. It doesn't send anything to the server, and turning it off puts the window back to normal (hidden duplicates reappear the next time the list refreshes).

## Install

You need [XIVLauncher](https://goatcorp.github.io/) with Dalamud enabled.

1. In game, type **`/xlsettings`** and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this URL into the empty field and click **+**:

   ```
   https://raw.githubusercontent.com/pearstopher/FFXIV-Remove-Duplicate-PF/main/repo.json
   ```

3. Make sure the checkbox next to the new entry is ticked, then click **Save and Close**.
4. Type **`/xlplugins`**, go to **All Plugins**, and search for **Remove Duplicate PF**.
5. Click **Install**. It's enabled straight away.
6. Open Party Finder (Duty menu > Party Finder). Duplicate ads now show a coloured `[N]`, and the title shows how many were combined.

**Updates** arrive through the plugin installer like any other plugin.

**To turn it off**, disable or uninstall it in `/xlplugins`.

## Works with other Party Finder plugins

Tested alongside [NoSoliciting](https://github.com/Noevain/NoSoliciting) and [Better Party Finder](https://github.com/Infiziert90/BetterPartyFinder). It never shows a listing another plugin has hidden. Details are in [the compatibility notes](docs/party-finder-notes.md#compatibility-with-other-party-finder-plugins-28).

## Problems or ideas

Please [report a bug](https://github.com/pearstopher/FFXIV-Remove-Duplicate-PF/issues/new?template=bug.md). The form asks for your versions, what happened, and the relevant lines from `dalamud.log`, which makes problems much quicker to fix.

## Contributing

- **Building and testing:** see [DEVELOPMENT.md](DEVELOPMENT.md). It covers the .NET 10 SDK, building, loading a dev build in game, unit tests (`dotnet test`), and the in-game checks in [docs/in-game-testing.md](docs/in-game-testing.md) and [docs/manual-tests.md](docs/manual-tests.md).
- **Releasing:** push a version tag such as `v1.0.1`; GitHub Actions builds it, publishes the release and updates `repo.json` ([DEVELOPMENT.md, section 8](DEVELOPMENT.md#8-releasing-a-new-version)).
- **After game or Dalamud patches:** see the patch-day checklist ([DEVELOPMENT.md, section 9](DEVELOPMENT.md#9-staying-up-to-date-after-patches)).
- **How it works:** a plain-language tour of the code that runs in game is in [docs/how-the-code-works.md](docs/how-the-code-works.md). Research and design decisions are in [docs/party-finder-notes.md](docs/party-finder-notes.md).

## License

Licensed under the [GNU Affero General Public License v3.0](LICENSE.md), the same license as [goatcorp/SamplePlugin](https://github.com/goatcorp/SamplePlugin), which this plugin was scaffolded from.

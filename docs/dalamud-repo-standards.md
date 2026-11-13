# Official Dalamud repo standards (#98)

The rules for the official plugin repository ([goatcorp/DalamudPluginsD17](https://github.com/goatcorp/DalamudPluginsD17)), and how the plugin meets them. First checked on 2026-11-12 (#99). **Re-checked on 2026-11-12 (#121)** against every page in the submission page's "Before submitting your plugin" box, read again from scratch, plus a review of all the plugin's code. Submitted on 2026-11-12 as [goatcorp/DalamudPluginsD17#9582](https://github.com/goatcorp/DalamudPluginsD17/pull/9582) (testing track) and waiting for review; until it's accepted, the plugin is only in the custom repo (`repo.json`).

**Summary:** the plugin meets every requirement. The final review found no problems to fix: the code only reads Party Finder data and changes the text drawn in the Party Finder window, it has no hooks, network access or automation, and it puts the window's text back when it unloads. The owner submitted it (#115).

## Sources

The four pages in the "Before submitting your plugin" box on [Submitting a plugin](https://dalamud.dev/plugin-publishing/submission):

- [Plugin metadata](https://dalamud.dev/plugin-development/plugin-metadata)
- [Technical considerations](https://dalamud.dev/plugin-development/technical-considerations) (new in #121)
- [Plugin restrictions](https://dalamud.dev/plugin-publishing/restrictions/)
- [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy/)

Also used:

- [Approval process](https://dalamud.dev/plugin-publishing/approval-process)
- [DalamudPluginsD17 README](https://github.com/goatcorp/DalamudPluginsD17)
- [Plogon `BuildProcessor.cs`](https://github.com/goatcorp/Plogon/blob/main/Plogon/BuildProcessor.cs), the build bot (lock file checks)

## 1. Plugin metadata

Checked against the manifest the build produces (`RemoveDuplicatePF.json`), which comes from `RemoveDuplicatePF.csproj`.

| Field | Required | Status | Value or note |
| --- | --- | --- | --- |
| Name | Yes | ✅ | Remove PF Duplicates |
| Author | Yes | ✅ | Ane Mora |
| Punchline | Yes | ✅ | "Hide duplicate Party Finder ads. Each repeated listing shows once, with a count." One line. |
| Description | Yes | ✅ | Three sentences on what it does, then "This plugin has no settings." The first line works as a summary, as the page suggests. |
| RepoUrl | No | ✅ | The public GitHub repo. |
| Tags | No | ✅ | party finder, pf, duplicates, duplicate, hide, filter, spam, ads, listings |
| IconUrl | No | ✅ | `images/icon.png` on `main`. |
| ApplicableVersion, LoadRequiredState, LoadSync, CanUnloadAsync, LoadPriority, AcceptsFeedback | No | ✅ | DalamudPackager defaults. |
| CategoryTags, ImageUrls, FeedbackMessage | No | — | Not set; all optional. |
| AssemblyVersion, InternalName, DalamudApiLevel | Filled in automatically | ✅ | 1.2.2.0 (from the csproj, #110), `RemoveDuplicatePF`, 15. The page says to leave these to DalamudPackager. |
| Changelog | No | ✅ | In the submitted [`manifest.toml`](https://github.com/pearstopher/DalamudPluginsD17/blob/remove-pf-duplicates/testing/live/RemoveDuplicatePF/manifest.toml) (#115). The page warns a changelog only in the PR description doesn't reach the installer; the manifest is one of the two places that does. |
| `Dip17Channel` | Must not be set | ✅ | Not set. |

## 2. Technical considerations

| Point | Kind | Status | Why |
| --- | --- | --- | --- |
| Regular windows use the Dalamud Windowing API | Recommendation | — | The plugin has no windows of its own. It only changes text in the game's Party Finder window. |
| Use Lumina rather than XIVAPI for game data | Recommendation | — | It reads no game data sheets and makes no web requests. The `[N]` colour is a fixed row number from the game's colour sheet (`CountPrefix.DefaultColorId`). |
| Don't hurt game performance | Recommendation | ✅ | Work only happens while the Party Finder window is open. The window text is scanned after list updates and at most every 250 ms otherwise, and rows whose text hasn't changed are skipped (#95). The per-frame handler is a single field write. The owner can confirm in game with **Plugin Stats** (`/xldev` → Plugins → Open Plugin Stats). |
| Backend server rules (data minimisation, opt-in telemetry, identifiers, HTTPS, DNS names…) | Mandatory if a server is used | — | Not applicable: the plugin has no networking at all and talks to no server. |

## 3. Restrictions

| Rule | Status | Why |
| --- | --- | --- |
| No automatic server traffic (polling or requests without the player doing something) | ✅ | The plugin never sends anything. It reacts to listings the game has already received (`IPartyFinderGui.ReceiveListing`). The game's own background page fetch (#51) isn't caused by the plugin. |
| No out-of-spec server traffic (letting the player submit things they normally couldn't) | ✅ | No server traffic. |
| No combat changes | ✅ | Party Finder window only. |
| Doesn't affect Square Enix's revenue (Mog Station) | ✅ | |
| No parsing, raid logging, DPS meters or similar | ✅ | It shows nothing beyond what the Party Finder already lists; it only hides repeats and counts them. |
| Doesn't collect account IDs of other players' characters | ✅ | It uses Party Finder **listing** IDs (`IPartyFinderListing.Id`), never content or account IDs. They're kept in memory for the current search only and never stored or uploaded. |
| No hard dependency on rule-breaking plugins | ✅ | No dependencies. It works alongside NoSoliciting and Better Party Finder (#28) and never shows a listing another plugin hid. |
| Not only useful out of spec | ✅ | It's for the normal Party Finder. |
| No PvP or competitive advantage | ✅ | |
| Not a "common non-starter" (emote looping, skipping cutscenes or dialogs, auto crafting, auto loot, friend alerts, AOE markers or recolouring, camera zoom, FFLogs, parsers, Fantasia, XIVCombo, PvP) | ✅ | None of these. It only changes how the Party Finder list looks. |

## 4. AI Usage Policy

| Rule | Status | Why |
| --- | --- | --- |
| Disclose the level of AI use in the PR description | ✅ | Level **Copilot**, written by the owner (#114) and used as the submission PR's description (#115). |
| Disclosures and PR descriptions written by a person | ✅ | Written by the owner and saved word for word; the only change was the level word, at the owner's request. |
| AI-generated assets disclosed in the plugin description | ✅ | None: the owner made the icon by hand in Photopea. |
| Tested personally before submitting | ✅ ongoing | In-game manual tests (`docs/manual-tests.md`, #92). Run them again on the exact commit being submitted. |
| Able to explain every design decision | Owner | Copilot-level submissions are reviewed at the team's discretion, and reviewers may ask how it works. [how-the-code-works.md](how-the-code-works.md) is the preparation. |

## 5. Code review: how the plugin touches the game (#121)

All code in `RemoveDuplicatePF/` and `RemoveDuplicatePF.Core/` was read. `RemoveDuplicatePF.Core` is plain logic with no game, Dalamud or network access (its only file access is reading the plugin DLL's timestamp for the load message). Everything that touches the game is in `RemoveDuplicatePF/`:

| What | Where | What it's for | Reads or changes |
| --- | --- | --- | --- |
| `IPartyFinderGui.ReceiveListing` | `PartyFinderListener` | Sees each listing the game receives and sets `args.Visible = false` on repeats. This is Dalamud's supported way to hide a listing, the same one NoSoliciting uses. | Changes only whether the listing is shown, on this PC. |
| `IFramework.Update`, `RunOnTick` | `PartyFinderListener` | Notes each frame so the plugin can tell when a batch of results is complete (#94), and logs one summary line a frame later. | Reads only. |
| `IAddonLifecycle` on `LookingForGroup`: `PostSetup`, `PreReceiveEvent` | `SearchEvents` | Notices the window opening and button clicks (Refresh, Next, Previous, categories), to tell a new search from paging (#80). | Reads only; never changes or blocks the event. |
| `IAddonLifecycle` on `LookingForGroup`: `PostRequestedUpdate`, `PostRefresh`, `PostUpdate` | `ListingPrefixer`, `TitleUpdater` | Adds the `[N]` prefix to the kept listing's description text and the "(N duplicates hidden)" count to the window title. | Changes the text of text nodes in the Party Finder window (`AtkTextNode.SetText`). Nothing else in game memory is written. |
| `AgentLookingForGroup` (ClientStructs) | `DisplayedListings` | Reads which listing IDs are on the page on screen, so the right result set is used (#51, #74). | Reads only. |
| `IGameGui.GetAddonByName` | `ListingPrefixer`, `TitleUpdater` | Finds the Party Finder window on unload to put its text back. | Restores the original text. |
| `IPluginLog` | everywhere | Load/unload lines, errors, and Debug-level diagnostics (row text of ads, counts). | Writes to `dalamud.log` on the player's PC only. |

**Not used anywhere:** hooks or signatures (`IGameInteropProvider`), network access, chat, commands, sending packets or clicking anything for the player, player names or content IDs, object tables, config files or other file writes.

**Safety checks:**

- **Error handling:** every handler that touches the game is wrapped in `try`/`catch` and logs the error instead of throwing. The exceptions are `OnSetup` and `OnFrame`, which each call a one-line method that only sets a field, and the one-line Debug summary in `PartyFinderListener.LogSummary`, which runs as a scheduled task (`RunOnTick`), so an error there stays in that task rather than reaching the game. An error can't crash the game; at worst a prefix or the title count isn't shown.
- **Pointer safety:** the window, its node list and the title node are null-checked before use, and the walk through nested UI components stops at depth 8.
- **Unloading (#11):** every listener is removed, the original row text and title are written back, and each part is cleaned up even if another part fails. Hidden listings come back the next time the list refreshes (the plugin can't un-hide a listing the game has already processed). This is stated in the README.
- **Threading:** all callbacks run on the game's main thread, as Dalamud calls them.

## Build

| Rule | Status | Why |
| --- | --- | --- |
| Public, open-source Git repo | ✅ | Public GitHub repo, AGPL-3.0. |
| Uses Dalamud.NET.Sdk / DalamudPackager | ✅ | Dalamud.NET.Sdk 15.0.0, set in `global.json`. |
| `packages.lock.json` committed | ✅ | `RemoveDuplicatePF/packages.lock.json` is tracked, and `dotnet restore --locked-mode` passes (re-checked in #121). `RemoveDuplicatePF.Core` has no packages, so it doesn't need a lock file. |
| No downloads during the build | ✅ | The build fetches nothing beyond the NuGet packages in the lock file. |
| Same commit always builds the same version | ✅ | Fixed in #110: `<Version>` in the csproj is the real version, bumped in each release commit. `release.yml` checks the tag matches it, and a unit test fails if it's older than the version in `repo.json`. |
| Version not based on a timestamp or build counter | ✅ | Hand-set `MAJOR.MINOR.PATCH.0`. |
| Icon 64–512 px, square PNG | ✅ | `images/icon.png` is 512×512 (re-checked in #121). The submission needs a copy at `testing/live/RemoveDuplicatePF/images/icon.png`. |
| Extra images (optional) | — | None. If any are added later, they must be PNGs no larger than 730×380. |

## Submission process

| Rule | Status | What's needed |
| --- | --- | --- |
| New plugins start in `testing/live/` | ✅ | Submitted as [`testing/live/RemoveDuplicatePF/`](https://github.com/pearstopher/DalamudPluginsD17/tree/remove-pf-duplicates/testing/live/RemoveDuplicatePF), building the v1.2.3 release commit `39f16f3d2fd15aa1e57a80d36f07758606aa33ed` (#115, #126). |
| One plugin per PR, from its own branch | ✅ | [goatcorp/DalamudPluginsD17#9582](https://github.com/goatcorp/DalamudPluginsD17/pull/9582): only this plugin's two files, from the `remove-pf-duplicates` branch of the owner's fork (#115). The manifest check passed; the first build waits for the approval team to enable builds for a first-time submitter. |

## Updates after acceptance

New plugins start in `testing/live/`; a later PR moves the folder to `stable/` without a version bump or new commit. Each update after that is a PR changing `commit` in the manifest, and can go straight to stable: there's no rule requiring testing builds for updates. Details in [DEVELOPMENT.md, section 10](../DEVELOPMENT.md#10-updates-in-the-official-repo).

## The custom repo after acceptance (#112)

**Decision: keep it, but stop pointing people to it.** Once the plugin is accepted into the official repo:

- `repo.json`, the release workflow that updates it, and the plugin's place in the `ffxiv-plugins` combined repo all stay as they are, as a backup.
- The README's Install section switches to the official instructions (`/xlplugins` → search for **Remove PF Duplicates**, no custom repo URL). The project itself no longer tells anyone to add the custom repo.

At submission the owner removed the README's custom repo install instructions (#130). For now the README has no install section; which instructions to add back depends on whether the plugin is accepted.

## Follow-up issues

1. **Keep the version in the csproj** (#110, done).
2. **Prepare the D17 submission files** (#111, done). The draft folder was removed once its contents were in the submission (#129).
3. **AI disclosure and icon check** (#114, done).
4. **Decide the custom repo's future** (#112, done).
5. **Final review against the requirements** (#121, done). No problems found.
6. **Submit** (#115). Submitted by the owner on 2026-11-12 as [goatcorp/DalamudPluginsD17#9582](https://github.com/goatcorp/DalamudPluginsD17/pull/9582); waiting for review.

Can't be checked until submission: the build bot (Plogon) building the plugin with no internet access. The D17 PR runs that build automatically.

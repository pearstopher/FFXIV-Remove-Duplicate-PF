# Party Finder research notes

Findings for #4. Later issues (#5 to #11) build on these answers.

**Sources read** (shallow clones, November 2026):

| Source | Commit | Notes |
| --- | --- | --- |
| [goatcorp/Dalamud](https://github.com/goatcorp/Dalamud) | `43b3411` | The same build as the local dev Dalamud (`15.0.3.9-29-g43b34118b`) |
| [aers/FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) | `80cb4a7` | Game struct and function definitions |
| [Noevain/NoSoliciting](https://github.com/Noevain/NoSoliciting) | `51e6965` | |
| [Infiziert90/BetterPartyFinder](https://github.com/Infiziert90/BetterPartyFinder) | `eb7816d` | |

Each finding is marked **Confirmed (source)** when it was read in code, or **To verify in game** when it is inferred and needs checking with a debug log before a feature relies on it.

---

## 1. How listings arrive

**Confirmed (source).**

- Dalamud's `IPartyFinderGui` exposes one thing: the `ReceiveListing` event (`Dalamud/Plugin/Services/IPartyFinderGui.cs`).
- Dalamud hooks the game function `InfoProxyCrossRealm.ReceiveListing`. The server sends Party Finder results as a series of **segment packets** (`CrossRealmListingSegmentPacket`), each holding **4 listing slots** (`FixedSizeArray4<CrossRealmListing>`). See `Dalamud/Game/Gui/PartyFinder/PartyFinderGui.cs` and ClientStructs `FFXIV/Client/Game/Network/PartyFinderPackets.cs`.
- For each packet, Dalamud:
  1. skips empty slots (`ListingId == 0`),
  2. builds a **read-only copy** of each listing (`PartyFinderListing`),
  3. raises `ReceiveListing(listing, args)` once per listing,
  4. hides the listing in the packet if `args.Visible` is false, then lets the game process the packet.
- `args.BatchNumber` is the packet's `SegmentIndex`. ClientStructs documents it as: *"Starts at 1, counts up, ends with 0"*. So:
  - `BatchNumber == 1` means **a new result set has started** (a new search, refresh, category change or page). This is the reset signal for #8.
  - `BatchNumber == 0` means **the last segment** of the result set. This is the signal that the page is complete, used for title updates (#10).
  - **Confirmed in game (2026-11-11):** 14 result sets (refreshes, category changes, pages) all started at 1 and ended at 0. A full page is segments 1–12 then 0. Shorter pages (10 and 26 listings) follow the same pattern (`1, 2, 0` and `1…6, 0`). No set arrived as a lone segment 0 in that session.
  - **Correction (#72, 2026-11-11 19:26):** a category with only 2 listings **did** arrive as a lone segment 0, straight after a page ending in segment 0, and the #8 rules did **not** catch it: it was merged into the previous set. Fix: the game empties its displayed list (`AgentLookingForGroup.NumberOfListingsDisplayed == 0`) whenever it requests new results, which held for every page, refresh and category change in #71's sessions. `ResultSetBoundary` now also starts a new set when the list **has just become empty** (the first listing to see it empty after one that saw it non-empty). Every listing in a segment sees the same state, because Dalamud raises a whole segment before the game processes it.
- Fields available on `IPartyFinderListing` include `Id` (server listing ID), `ContentId`, `Name`, `Description` (`SeString`), `Category`, `Duty`, `SecondsRemaining`, `World`, `HomeWorld`, and `LastPatchHotfixTimestamp`.

### Pages and refreshes

- The game's agent holds the IDs of the listings currently shown in a 50-entry array (`AgentLookingForGroup.ListingsSub._listingIds`). **Confirmed in game:** a page holds up to **50 listings**, sent as 13 segments (12 × 4, then 2 in the final segment 0).
- Each page, refresh and category change is a new request to the server, and the response arrives as a new set of segments starting at `BatchNumber == 1`. The plugin only ever sees **the listings on the page being shown**. **Confirmed in game.**
- **Arrival order (in game):** listings arrive sorted by `SecondsRemaining`, ascending. In each 50-listing page, 48 of 49 consecutive pairs were in order. This is consistent with the window's own sort, which supports the decision to keep the first listing in display order (#5).

## 2. Hiding a listing

**Confirmed (source).** Set `args.Visible = false` in the `ReceiveListing` handler. Dalamud then overwrites that packet slot with an empty listing (`packet->Payload.Entries[i] = default`), and the game skips it like any other empty slot. Both reference plugins do this (NoSoliciting `Filter.cs`, BetterPartyFinder `Filter.cs`).

Rules that follow from the source:

- **Never set `args.Visible = true`.** The same `args` object is passed to every plugin's handler. Writing `true` would undo a hide from another plugin such as NoSoliciting. Use BetterPartyFinder's pattern:

  ```csharp
  args.Visible = args.Visible && !isDuplicate;
  ```

- Hidden listings leave the page **shorter** (for example 47 rows instead of 50). The game does not backfill from the next page.
- The game's own counts are calculated by the server, so they still include hidden listings: the per-category numbers (`AddonLookingForGroup._categoryCountTextNodes`) and the results count (`ResultsCountTextNode`).
- Handler exceptions are caught and logged by Dalamud, so a bug in our handler can't crash the game. It does mean the listing is left visible.

## 3. Identifying the oldest listing in a duplicate group

> **Decision (after #4):** the display order is trusted as-is. The **first listing in each group in the order the game shows it** stays visible. No age signal (listing ID, time remaining) is used. This is what option 1 below does, and `DuplicateTracker` in Core implements it (#5). The rest of this section is kept as background.

### Candidate fields

| Field | Meaning | Usefulness |
| --- | --- | --- |
| `Id` (server listing ID) | Assigned by the server when the listing is created | **Best candidate:** if IDs are assigned in increasing order, the lowest ID is the oldest. **To verify in game.** |
| `SecondsRemaining` | Time until the listing expires | Lower usually means older, but hosts choose different durations and can renew, so it isn't reliable on its own. Useful as a tie-breaker and sanity check. |
| Arrival order / list position | Order of segments and slots | Follows the server's sort order for the current view, which isn't necessarily age. |

**Proposal:** treat **lowest `Id`** as oldest, with lowest `SecondsRemaining` as the tie-breaker. #5 should add a debug log that prints `BatchNumber`, `Id`, `SecondsRemaining` and the first 30 characters of the description for each listing. One session in a busy category will show whether IDs increase with age and in what order listings arrive.

### Do listings arrive oldest-first?

**Unknown; to verify in game** with the same log. The server sends results in the window's sort order, so arrival order depends on that sort, not on listing age.

### Keeping the oldest visible when decisions are made on arrival

The hide decision for a listing has to be made **when its segment arrives**. A listing that was already let through can't be hidden later through the API. Options:

1. **Keep the first one to arrive (recommended for v1).** The first listing in each group stays visible and later copies are hidden. This is simple and deterministic, and if arrival order turns out to be oldest-first, it is already correct.
2. **Keep the oldest of the copies that arrive together in one packet.** A plugin-owned hook (see section 4) sees all 4 slots of a segment at once and can pick the oldest among them. This helps only within a segment, not across segments.
3. **Keep the oldest across the whole page.** This would mean editing the game's stored list after the final segment, through the agent or addon, which is undocumented in ClientStructs. It isn't recommended without a prototype.

**Recommendation:** ship option 1. Write the grouping logic in Core so it reports which listing it chose, which lets the "keep" rule change later without touching the hook code. If the in-game log shows arrival isn't oldest-first and the difference matters, look at option 3 in a follow-up issue.

## 4. Changing a listing's displayed description

**Confirmed (source): there is no supported API.** `IPartyFinderListing.Description` is a read-only copy, and Dalamud writes back only the `Visible` flag. Dalamud has no other `IPartyFinderGui` members.

The main problem is timing: the count for a group (`[N]`) isn't known until **after** the last segment arrives. The kept listing arrives first, so its description has already gone to the game before later duplicates are seen.

### Options

| Option | How | Pros | Cons |
| --- | --- | --- | --- |
| **A. Edit the rendered list rows** | `IAddonLifecycle` listener on `LookingForGroup` (`PostRefresh` / `PostRequestedUpdate`). After the final segment, map each visible row to its listing ID through `AgentLookingForGroup.Listings` (the displayed IDs in order), and prepend a coloured `[N]` to that row's description text node. | Correct final counts. No packet editing. Nothing changes when the plugin is unloaded except text it re-sets. | Rows are reused as you scroll, so the text has to be re-applied on every list update. The row-to-node layout isn't in ClientStructs and needs a prototype. |
| **B. Edit the packet in our own hook** | Hook `InfoProxyCrossRealm.ReceiveListing` with `IGameInteropProvider.HookFromAddress`, using `InfoProxyCrossRealm.Addresses.ReceiveListing`, and rewrite `CrossRealmListing._description` (192 bytes) before the game reads it. | The game displays it naturally everywhere the list shows it. | The count is only "copies seen so far", which is wrong for the kept listing. It has to fit 192 bytes including the colour payload, so it may truncate. Other plugins (NoSoliciting) would see the modified text. |
| **C. No in-list prefix (fallback)** | Show only the total in the window title (#10) and log the per-group counts. | No UI hacking at all. | Loses the per-listing `[N]`, which is a headline feature. |

**Decision (after #4):** go with **option A**, implemented in #9. If row mapping proves unreliable, fall back to **option C** for the first release, and record what was learned on #9. Option B can't show correct counts, so it is rejected.

**Colour:** build the prefix as an `SeString` with a `UIForeground` payload, the same mechanism the game uses for its coloured tags. Then write it with `AtkTextNode.SetText` using the encoded bytes.

## 5. Party Finder window title

- The window is the `LookingForGroup` addon (`AddonLookingForGroup`, which inherits `AtkUnitBase`).
- The title belongs to the window component: `AtkUnitBase.WindowNode` → `AtkComponentWindow`. Its node IDs list `[0] = Title` and `[1] = Subtitle`, and the component has `SetTitle(title, subtitle)` (ClientStructs `Component/GUI/AtkComponentWindow.cs`). **Confirmed (source).**
- **Safe approach:**
  1. `IAddonLifecycle` `PostSetup` on `LookingForGroup`: read and **store** the original title and subtitle text.
  2. After the final segment (`BatchNumber == 0`), on the next `PostRefresh`/`PostRequestedUpdate`: set the title (or subtitle) to the original plus ` (N duplicates hidden)`. Only touch it while the addon exists (check `IsAddonReady`/non-null).
  3. `PreFinalize` and plugin `Dispose`: restore the stored text and unregister all listeners (#11).
- **Prefer the subtitle** if it is empty, because it leaves the game's title untouched. **To verify in game:** what the title and subtitle currently show, and whether the game resets them on refresh. If it does, re-apply on every refresh.
- **As built (#10, verified in game 2026-11-11):** the count is appended to the **title**, which the owner confirmed displays correctly and updates on refresh and page changes. The subtitle isn't used and isn't needed. The title is re-checked after list updates and every 250 ms, and the count follows the page on screen (#51). **Changed in #64:** the title reads `Party Finder (N duplicates)`, where N is the **total** number of listings in duplicate groups (including the kept one in each), which equals the sum of the `[N]` prefixes. The suffix is shown in the prefix colour (`UIForeground`, `CountPrefix.DefaultColorId`). It was `N duplicates combined` (hidden copies only) in v1.0.0. **Changed in #75 (v1.2.0):** the title reads `Party Finder (N duplicates hidden)` (`1 duplicate hidden`), where N counts **hidden listings only**: the extra copies in each group plus listings hidden because an earlier page had them. Older suffixes are still recognised and stripped.
- Interop calls must run on the main thread. `IAddonLifecycle` callbacks already do; anything triggered from elsewhere should use `IFramework.RunOnFrameworkThread`.

## 6. Counting duplicates across pages

**No, not reliably.** The plugin only receives the page currently being shown (section 1). Earlier pages could be remembered, but:

- rows already shown on page 1 can't have their counts updated after page 2 arrives, and going back to page 1 is a new request anyway;
- a page-2 listing could be hidden because its twin was on page 1, but the user would never see the twin's count change.

**Recommendation:** count **per page**, reset on `BatchNumber == 1` (#8), and word the title so it is clearly per page (for example "3 duplicates hidden on this page").

**Decision (owner, 2026-11-11): no cross-page combining.** Counts stay per page. The plugin only sees pages the game downloads; it must not request other pages itself, since that would send extra server requests and change what the window is doing (#11). Combining across pages already visited was also rejected: counts on a page would change after visiting another, and results would depend on browsing order. The last few result sets are kept only to tell which page is on screen (#51), never to combine them.

**Reversed (owner, 2026-11-11, #75):** listings already seen on an **earlier** page are hidden on later pages, and the plugin still never requests pages itself. See section 7 for how the page number is found.

## 7. Which page a result set belongs to (#71, #73)

Needed by #74 and #75: the hide decision is made per listing as it arrives, so the page must be known **before** the window redraws.

| Source | Gives the page? | Valid when results start arriving? |
| --- | --- | --- |
| Window range text, `AddonLookingForGroup.CurrentPageTextNode` (`1-42`, `51-93`, `101-147`, `151-162`) | Yes: page = (start − 1) / 50 + 1, page size 50 | **No:** still shows the previous page; updates a frame after the first segment |
| Window `ResultsCountTextNode` (`42(306)`) | No: shown count and server total | - |
| `AgentLookingForGroup` (whole struct, 0x36D8 bytes) | **No:** no field follows the page | - |
| **`InfoProxyCrossRealm` + 0x0484** (unmapped byte, before `LocalPlayerGroupIndex` at 0x485) | **Yes: zero-based page index** | **Yes:** already the new page when the first listing arrives |

**Evidence (owner's walk-through, 2026-11-11 19:34, `Others`, 4 pages):** at each result-set start, byte 0x0484 was `0 1 2 3 2 1 0 1 0` for pages `1 2 3 4 3 2 1 2 1` (Next ×3, Previous ×3, Next, Refresh). It was the only byte of 108 changing bytes in both structs that tracked the page. Reached through `AgentLookingForGroup.InfoProxyCrossRealm`. No hook is needed; it's a plain read.

**Other behaviour seen:**
- **Refresh, opening the window and changing category load page 1.** Only Next and Previous reach other pages.
- **The game empties its displayed list** (`NumberOfListingsDisplayed == 0`) whenever it requests results; #72 uses this.

**Caveats for #74:**
- 0x0484 isn't in FFXIVClientStructs, so a game patch could move it. #74 cross-checks it against the range text once each result set has finished, and logs a mismatch, so the patch-day CI and manual tests catch it. On any mismatch or out-of-range value, treat the set as page 1 (hide nothing extra).
- Category changes weren't exercised in the round-3 session. They load page 1 per rounds 1 and 2, and the cross-check covers them.

**As built (#75):**
- `PageMemory` keeps, per page, the 30-character match keys of the listings the plugin left visible or hid as duplicates (not ones another plugin hid). A new search's page 1 clears it (#80; was any page 1 in #75); every result set replaces its own page's entry.
- On page 2 and later, a listing whose key is on any earlier page is hidden, including the copy that would otherwise be kept, so no `[N]` appears for it. Empty descriptions are never hidden. Page 1 is never affected.
- If the page index is unknown or no longer trusted (two cross-check mismatches), every set counts as page 1, which turns cross-page hiding off.
- **Back-to-back small result sets (gap in #72, fixed here):** when two single-segment result sets arrive in a row (e.g. switching between two small categories), every listing of both sees the game's list empty, so the "list just emptied" signal never fired and they merged. `TitleUpdater` now notes each frame that the window has listings (`ResultSetBoundary.NoteGameListShown`); an empty list after that counts as a new transition.

## 8. New search or back to page 1 (#80)

Previous and Refresh on page 2 both move the page index from 1 to 0, so the index can't tell them apart. The window's own events can.

**Evidence (owner's walk-through, 2026-11-11 20:09):** `IAddonLifecycle` `PreReceiveEvent` on `LookingForGroup` logged one `ButtonClick` per action, 80-130 ms before the first listing of the result set it requested:

| Action | `ButtonClick` event param | Page index after |
| --- | --- | --- |
| Open window | none (`PostSetup` fires ~0.2 s before the first listing) | 0 |
| Next | 9 | +1 |
| Previous | 10 | −1 |
| Refresh | 6 | 0 |
| Category tabs | one number per tab (22 and 33 seen) | 0 |

**As built:** `SearchEvents` passes clicks and window opens to `SearchStart`, which judges each result set from what happened since the previous one started:
- the window opened → new search;
- last click Next or Previous → same search;
- last click anything else (Refresh, a tab, ...) → new search;
- no click → same search (a background re-fetch, #51), but only if clicks have been seen since the window opened; otherwise new search, in case clicks aren't reaching the plugin.

Only Next and Previous are matched by number, so if a patch renumbers the buttons, the plugin falls back to treating every page 1 as a new search (the #75 behaviour) rather than keeping a stale memory. The `PF result set` line shows `(page 1, new search)` or `(page 1, same search)`.

## 9. Pages overlap, and Previous can land early (#84)

**Symptom (v1.2.0):** in Others, Previous from the last page gave short or blank pages, and sometimes no Next button.

**Cause:** pages are positions in a list that keeps changing (parties are listed, filled and delisted), so pages can start a few listings earlier or later than on the last visit. In **filtered categories** such as Others, the game's own Previous from the last page lands about **10 listings early**: page 2 again started at listing 41 instead of 51, so it repeated the end of page 1. A comparison build that hid nothing confirmed this happens **without the plugin's hiding** (owner's walk-through, 2026-11-11 20:47). In the biggest category Previous lands exactly. Without the plugin the overlap goes unnoticed; cross-page hiding (#75) hid the repeated listings as "seen on an earlier page", leaving short or blank pages.

**Memory evidence** (`InfoProxyCrossRealm`, read at each click and after each result set): `0x30` = listings on screen (after hiding), `0x34` (16-bit) = start of the previous page (`(page index − 1) × 50`, `0xFFFF` on page 1), `0x32` (16-bit) = a cursor into the server's list, about 52 per page in the biggest category and further apart in Others, `0xFFFF` after the last page. The plugin's hiding only changes `0x30`.

**Fix:** a listing **left visible on an earlier page** and sent again on a later page is shown again and not counted as hidden. `PageMemory` keeps the shown IDs per page, cleared and replaced the same way as the match keys. Other listings with the same first 30 characters are still hidden. A listing hidden on the earlier page as a within-page duplicate stays hidden when repeated.

**Also fixed in #84:**
- The page check (#74) compared the page with the window's range text before the window had redrawn it (page 6 arrived while it still showed `201-238`). Two stale frames in a row switched cross-page hiding off for the session. A check is now skipped when the range text doesn't cover the number of listings on screen.
- `Party Finder title text node not found` was logged as a warning every time the window opened, because the node doesn't exist on the first frame. It now warns only if the title stays missing for about 5 seconds.

Wider handling of changing results is tracked in #85.

---

## Other notes for later issues

- **Matching key (#5, changed in #42):** use `listing.Description.TextValue` (the text players see), compared ordinally on its **first 30 characters** (`DescriptionMatch.Key`); shorter descriptions must match in full. See "Design decision: match on the first 30 characters" below. Keep the matching function in Core with unit tests for whitespace, colour payloads and auto-translate. Whether to group across categories when viewing "All" is a #5 decision. The PROJECT.md wording ("exact same text") implies yes.
- **NoSoliciting resets on every batch change.** It clears its history whenever `BatchNumber` changes, which is every segment, so it isn't a model for per-page tracking. Reset on `BatchNumber == 1` instead.
- **Detail view:** opening a listing loads its full details separately (`AgentLookingForGroup.Detailed`). The `[N]` prefix won't appear there, which is acceptable.
- **Main thread:** `ReceiveListing` runs on the game's network-handling path. Keep the handler fast and allocation-light, with no blocking calls.

## Design decision: match on the first 30 characters (#42)

**Decided by the owner on 2026-11-11, replacing the original "exact same text" rule from #5.**

**Rule.** Two listings are duplicates when their descriptions start with the **same 30 characters** (ordinal, case-sensitive, compared on the text players see). Descriptions shorter than 30 characters must match in full. Empty or whitespace-only descriptions are never combined. The constant is `DescriptionMatch.Length` in Core.

**Why 30, and why from the log.** The plugin's Debug log records the first 30 characters of each listing (`PF batch=… text=…`), and testing is done by reading that log. Under the exact-match rule, the first test session showed listings that looked identical in the log but weren't combined, because their text differed after character 30. There was no way to tell from the log whether that was correct. Matching on exactly what the log shows removes that ambiguity: **if two listings look identical in the log, they are combined**, so results can be checked straight from the log. The log and the rule both use `DescriptionMatch.Key`, so they can't drift apart. (Since #11 the log writes one `PF result set #…` summary per page instead of a line per listing. It names each duplicate group by the same 30-character key, so this still holds, without recording every listing's text.)

**Accepted consequence: different ads with the same opening are merged.** Two ads that start with the same 30 characters are combined **even if the rest of their text is different**. This is intentional. An ad whose first 30 characters are the same as another ad's isn't unique enough to deserve its own row: it's boilerplate (a shared venue or FC header, a copied template, the same ad reposted with a different time or room number). Merging these is the desired behaviour, not a bug. The kept listing shows its own full text, and the `[N]` count includes the merged ads.

**Not affected:** ads shorter than 30 characters still need an exact match. For example, "Join our FC!" is not merged with "Join our FC! Discord: abc", because the shorter one doesn't have 30 characters to compare.

## Verification checklist for the first in-game session

Answered from the Debug log of the 2026-11-11 session (11:56–12:05), unless marked open:

- [x] `BatchNumber` sequence: always `1, 2, …, 0` for refreshes, category changes and pages (section 1)
- [x] Page size: 50 (section 1)
- [x] Arrival order: sorted by time remaining, consistent with the window's order (section 1). The owner also confirmed that the kept listings and `[N]` prefixes appear on the right rows (#9).
- [x] Row text matching for `[N]` (#9): works. `PF prefix: X rows matched` showed 1–3 on-screen rows matched for up to 7 groups; only rows currently on screen exist as text nodes.
- [x] Window title: reads "Party Finder"; the count appended by #10 displays and updates correctly. No subtitle is needed (owner, 2026-11-11).

## Compatibility with other Party Finder plugins (#28)

**Research (Dalamud `43b3411` source and the owner's log):**
- Plugins' `ReceiveListing` handlers run in the order the plugins were **constructed**. Each plugin's scoped `PartyFinderGui` subscribes once, in its constructor, and Dalamud calls subscribers in order (`Delegate.EnumerateInvocationList`). NoSoliciting and Better Party Finder also subscribe once, at load.
- Dev plugins load first at game start, so ours runs **before** NoSoliciting and Better Party Finder after a restart, and **after** them after a hot reload or re-enable.
- `RemoveDuplicatePF` never un-hides another plugin's decision and doesn't count listings already hidden, so when ours runs last there's no interaction.
- **Known gap when ours runs first:** if another plugin hides the copy we kept (for example, Better Party Finder filtering on duty, jobs or item level), the copies we hid stay hidden and that group disappears. NoSoliciting filters on text, so it would normally hide every copy anyway.
- Re-subscribing per page can't change the order between plugins. `LoadPriority` only nudges load order, because most plugins load in parallel.

**Outcome (owner, 2026-11-11):** tested alongside NoSoliciting and Better Party Finder with no problems seen. No mitigation added. Any compatibility problem found later will be filed as a new issue, referencing this section.

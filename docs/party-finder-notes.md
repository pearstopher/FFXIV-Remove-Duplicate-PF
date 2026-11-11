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
  - **To verify in game:** that every refresh and page change starts at 1, and that the final segment is always 0, including when there are 4 or fewer results.
- Fields available on `IPartyFinderListing` include `Id` (server listing ID), `ContentId`, `Name`, `Description` (`SeString`), `Category`, `Duty`, `SecondsRemaining`, `World`, `HomeWorld`, and `LastPatchHotfixTimestamp`.

### Pages and refreshes

- The game's agent holds the IDs of the listings currently shown in a 50-entry array (`AgentLookingForGroup.ListingsSub._listingIds`). That suggests **a page shows up to 50 listings**, sent as about 13 segments of 4. **To verify in game.**
- Each page, refresh and category change is a new request to the server, and the response arrives as a new set of segments starting at `BatchNumber == 1`. The plugin only ever sees **the listings on the page being shown**. **To verify in game** (log the batch numbers while paging).

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

**Proposal:** prototype **option A** in #9. If row mapping proves unreliable, fall back to **option C** for the first release, and record what was learned on #9. Option B can't show correct counts, so it is rejected.

**Colour:** build the prefix as an `SeString` with a `UIForeground` payload, the same mechanism the game uses for its coloured tags. Then write it with `AtkTextNode.SetText` using the encoded bytes.

## 5. Party Finder window title

- The window is the `LookingForGroup` addon (`AddonLookingForGroup`, which inherits `AtkUnitBase`).
- The title belongs to the window component: `AtkUnitBase.WindowNode` → `AtkComponentWindow`. Its node IDs list `[0] = Title` and `[1] = Subtitle`, and the component has `SetTitle(title, subtitle)` (ClientStructs `Component/GUI/AtkComponentWindow.cs`). **Confirmed (source).**
- **Safe approach:**
  1. `IAddonLifecycle` `PostSetup` on `LookingForGroup`: read and **store** the original title and subtitle text.
  2. After the final segment (`BatchNumber == 0`), on the next `PostRefresh`/`PostRequestedUpdate`: set the title (or subtitle) to the original plus ` (N duplicates hidden)`. Only touch it while the addon exists (check `IsAddonReady`/non-null).
  3. `PreFinalize` and plugin `Dispose`: restore the stored text and unregister all listeners (#11).
- **Prefer the subtitle** if it is empty, because it leaves the game's title untouched. **To verify in game:** what the title and subtitle currently show, and whether the game resets them on refresh. If it does, re-apply on every refresh.
- Interop calls must run on the main thread. `IAddonLifecycle` callbacks already do; anything triggered from elsewhere should use `IFramework.RunOnFrameworkThread`.

## 6. Counting duplicates across pages

**No, not reliably.** The plugin only receives the page currently being shown (section 1). Earlier pages could be remembered, but:

- rows already shown on page 1 can't have their counts updated after page 2 arrives, and going back to page 1 is a new request anyway;
- a page-2 listing could be hidden because its twin was on page 1, but the user would never see the twin's count change.

**Recommendation:** count **per page**, reset on `BatchNumber == 1` (#8), and word the title so it is clearly per page (for example "3 duplicates hidden on this page").

---

## Other notes for later issues

- **Matching key (#5):** use `listing.Description.TextValue` (the text players see), compared exactly (ordinal). Keep the matching function in Core with unit tests for whitespace, colour payloads and auto-translate. Whether to group across categories when viewing "All" is a #5 decision. The PROJECT.md wording ("exact same text") implies yes.
- **NoSoliciting resets on every batch change.** It clears its history whenever `BatchNumber` changes, which is every segment, so it isn't a model for per-page tracking. Reset on `BatchNumber == 1` instead.
- **Detail view:** opening a listing loads its full details separately (`AgentLookingForGroup.Detailed`). The `[N]` prefix won't appear there, which is acceptable.
- **Main thread:** `ReceiveListing` runs on the game's network-handling path. Keep the handler fast and allocation-light, with no blocking calls.

## Verification checklist for the first in-game session

Add a temporary debug log (in #5 or #7) and record the answers here:

- [ ] `BatchNumber` sequence for a refresh, a category change and a page change (does it always start at 1 and end at 0?)
- [ ] Page size (is it 50?)
- [ ] Do `Id` values increase with listing age? Compare with `SecondsRemaining`.
- [ ] Arrival order versus the sort order shown in the window
- [ ] Current title and subtitle text of the `LookingForGroup` window, and whether the game resets them on refresh

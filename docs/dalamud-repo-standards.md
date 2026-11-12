# Official Dalamud repo standards (#98)

Checked on 2026-11-12 against the rules for the official plugin repository ([goatcorp/DalamudPluginsD17](https://github.com/goatcorp/DalamudPluginsD17)). The plugin is only in the custom repo (`repo.json`) for now.

**Summary:** the plugin's code and behavior follow the rules. The one build problem (the version number wasn't stored in the code) is fixed (#110). The rest is submission paperwork, mainly the AI disclosure, which only the owner can write.

## Sources

- [Plugin Restrictions](https://dalamud.dev/plugin-publishing/restrictions)
- [Submitting a plugin](https://dalamud.dev/plugin-publishing/submission)
- [Approval process](https://dalamud.dev/plugin-publishing/approval-process)
- [AI Usage Policy](https://github.com/goatcorp/dalamud-docs/blob/main/docs/plugin-publishing/ai-policy.md)
- [Plugin metadata](https://dalamud.dev/plugin-development/plugin-metadata)
- [DalamudPluginsD17 README](https://github.com/goatcorp/DalamudPluginsD17)
- [Plogon `BuildProcessor.cs`](https://github.com/goatcorp/Plogon/blob/main/Plogon/BuildProcessor.cs), the build bot (lock file checks)

## What the plugin does: restrictions

| Rule | Status | Why |
| --- | --- | --- |
| No automatic or out-of-spec server traffic | ✅ | The plugin only reacts to listings the game receives (`IPartyFinderGui.ReceiveListing`) and reads the window. It never sends requests. The background re-fetch in #51 is the game's own, not the plugin's. |
| No combat changes | ✅ | Party Finder only. |
| Doesn't affect Square Enix's revenue | ✅ | |
| No parsing, logging or DPS meters | ✅ | |
| Doesn't collect other players' account IDs | ✅ | It uses Party Finder listing IDs only, keeps them in memory for the current search, and never stores or uploads them. |
| No hard dependency on rule-breaking plugins | ✅ | It has no dependencies. It works alongside NoSoliciting and Better Party Finder (#28). |
| Not only useful out of spec, no PvP advantage | ✅ | |
| Doesn't upload personal data | ✅ | The code has no networking. Debug lines in `dalamud.log` are only on the player's PC. |
| Not a "common non-starter" | ✅ | It only changes how the Party Finder list looks. |

## Build and manifest

| Rule | Status | Why |
| --- | --- | --- |
| Public, open-source Git repo | ✅ | Public GitHub repo, AGPL-3.0. |
| Uses Dalamud.NET.Sdk / DalamudPackager | ✅ | Dalamud.NET.Sdk 15.0.0 (the latest on NuGet), set in `global.json`. |
| `packages.lock.json` committed | ✅ | `RemoveDuplicatePF/packages.lock.json` is tracked, and `dotnet restore --locked-mode` passes. `RemoveDuplicatePF.Core` has no packages, so it doesn't need a lock file. |
| No downloads during the build | ✅ | The build doesn't fetch anything beyond the NuGet packages in the lock file. |
| Required manifest fields (Name, Author, Punchline, Description) | ✅ | Set in `RemoveDuplicatePF.csproj`. Tags and RepoUrl are set too. |
| Same commit always builds the same version | ✅ | Fixed in #110: `<Version>` in the csproj is the real version, bumped in each release commit. `release.yml` checks the tag matches it instead of overriding it, and a unit test fails if it's older than the version in `repo.json`. |
| Icon 64–512 px, square PNG | ✅ | `images/icon.png` is 512×512. The submission needs a copy at `testing/live/RemoveDuplicatePF/images/icon.png` in D17. |
| Extra images (optional) | — | None. If any are added later, they must be PNGs no larger than 730×380. |
| Regular windows use the Dalamud Windowing API | — | Not needed: the plugin has no windows. |
| Unloads cleanly | ✅ | #11. |

## Submission process and AI policy

| Rule | Status | What's needed |
| --- | --- | --- |
| New plugins start in `testing/live/` | To do | PR adding `testing/live/RemoveDuplicatePF/manifest.toml` (`repository`, `commit`, `owners`, `project_path = "RemoveDuplicatePF"`, `changelog`) and `images/icon.png`. |
| **AI disclosure in the PR, written by a person** | ❌ To do | Most of this plugin's code was written with Claude Code. The owner must say which level applies (probably **Copilot**: the owner plans, reviews and tests while the AI writes most of the code) in their own words. Copilot- and Auto-level submissions are reviewed at the team's discretion. The owner must be able to explain why the code works the way it does. Undisclosed AI use leads to a ban. |
| AI-generated assets disclosed in the plugin description | Owner to confirm | The icon was supplied by the owner (#12). If any part of it was AI-generated, the plugin description must say so. The team prefers hand-made icons. |
| Owner tested the plugin | ✅ ongoing | In-game tests (#92, `docs/manual-tests.md`). |
| One plugin per PR, from its own branch | To do | |

## Updates after acceptance

New plugins start in `testing/live/`; a later PR moves the folder to `stable/` without a version bump or new commit. Each update after that is a PR changing `commit` in the manifest, and can go straight to stable: there's no rule requiring testing builds for updates. Details in [DEVELOPMENT.md, section 10](../DEVELOPMENT.md#10-updates-in-the-official-repo). The draft submission files are in [official-repo-submission/](official-repo-submission/README.md).

## The custom repo after acceptance (#112)

**Decision: keep it, but stop pointing people to it.** Once the plugin is accepted into the official repo:

- `repo.json`, the release workflow that updates it, and the plugin's place in the `ffxiv-plugins` combined repo all stay as they are, as a backup.
- The README's Install section switches to the official instructions (`/xlplugins` → search for **Remove PF Duplicates**, no custom repo URL). The project itself no longer tells anyone to add the custom repo.

This happens in a new issue after the official PR is accepted (#115), not before.

## Follow-up issues

1. **Keep the version in the csproj** (#110, done). Set `<Version>` to the real version, bump it in each release commit, and have `release.yml` check that the tag matches the csproj instead of overriding it. Update DEVELOPMENT.md section 8.
2. **Prepare the D17 submission files** (#111). Draft `manifest.toml` and where each file goes, in [official-repo-submission/](official-repo-submission/README.md).
3. **AI disclosure and icon check** (#114). The owner writes the PR description and AI disclosure, and confirms whether the icon contains any AI-generated parts.
4. **Decide the custom repo's future** (#112, done). Keep it as a backup; after acceptance the README points to the official repo instead (see above).
5. **Submit** (#115). The very last step, after #98 closes, and only with the owner's go-ahead.

Can't be checked until submission: the build bot (Plogon) building the plugin with no internet access. The D17 PR runs that build automatically.

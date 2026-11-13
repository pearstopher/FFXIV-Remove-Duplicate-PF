# Official repo submission (draft)

Files for submitting the plugin to the official Dalamud repo, [goatcorp/DalamudPluginsD17](https://github.com/goatcorp/DalamudPluginsD17). Nothing here has been submitted. Submitting is [#115](https://github.com/pearstopher/FFXIV-Remove-PF-Duplicates/issues/115), the very last step, and only happens with the owner's go-ahead.

## What goes where at submission

| In this repo | In the DalamudPluginsD17 PR | Notes |
| --- | --- | --- |
| [manifest.toml](manifest.toml) | `testing/live/RemoveDuplicatePF/manifest.toml` | Ready as is: `commit` is already the v1.2.3 release commit. Delete the comment lines at the top. |
| [images/icon.png](../../images/icon.png) | `testing/live/RemoveDuplicatePF/images/icon.png` | 512×512 PNG. The icon must be 64–512 px and square. |
| [ai-disclosure.md](ai-disclosure.md) | The PR description | The owner's AI disclosure (#114), written by the owner, not by AI (AI Usage Policy). Paste it exactly as written. The icon was made by hand in Photopea, so the plugin description needs no AI note. |

New plugins must start in `testing/live/`. The folder is named after the plugin's internal name, `RemoveDuplicatePF`.

## The manifest fields

- `repository`: this repo's public clone URL.
- `commit`: the commit the build bot (Plogon) builds. It's `39f16f3d2fd15aa1e57a80d36f07758606aa33ed`, the commit tagged `v1.2.3` (`git rev-list -n 1 v1.2.3`), so the official build is exactly the version released to the custom repo and tested in game (#125, #126). Use the tagged commit, not the "Update repo.json" commit the release bot adds after it. It must have the right `<Version>` in the csproj ([DEVELOPMENT.md, section 8](../../DEVELOPMENT.md#8-releasing-a-new-version)).
- `owners`: GitHub usernames in charge of the plugin. Owners can also push updates.
- `project_path`: the folder with the plugin's csproj.
- `changelog`: shown to players in the plugin installer for this version.

After submission, updates work differently; see [DEVELOPMENT.md, section 10](../../DEVELOPMENT.md#10-updates-in-the-official-repo).

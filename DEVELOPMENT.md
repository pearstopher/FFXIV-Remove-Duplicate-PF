# Development guide

How to build Remove PF Duplicates on your PC, load it into the game, and check that a change works before opening a PR.

You test **locally**. The build produces a plugin DLL on your machine, and you point Dalamud at it as a *dev plugin*. Nothing goes through GitHub until the release workflow (#13) exists. The `latest.zip` you may see in `bin/` is a build artifact: it is git-ignored and is not how you test.

## 1. One-time setup

You need:

- **XIVLauncher with Dalamud.** Launch the game through XIVLauncher at least once, so that Dalamud's files exist in `%APPDATA%\XIVLauncher\addon\Hooks\dev\`. The build reads Dalamud's DLLs from there.
- **The .NET 10 SDK.** Check it with:

  ```sh
  dotnet --list-sdks
  ```

  You should see a `10.0.x` line. If you don't, install it with `winget install Microsoft.DotNet.SDK.10`, then restart your terminal and VS Code.
- **An editor (optional).** VS Code with the C# Dev Kit extension, Visual Studio, or Rider can all open `RemoveDuplicatePF.slnx`.

If XIVLauncher is installed somewhere other than the default, set the `DALAMUD_HOME` environment variable to its `addon\Hooks\dev` folder.

## 2. Build

From the repo root:

```sh
dotnet build
```

This makes a **Debug** build:

```
RemoveDuplicatePF\bin\x64\Debug\RemoveDuplicatePF.dll
```

Use Debug for day-to-day testing. A Release build (`dotnet build -c Release`) is what gets shipped. It also packages `bin\x64\Release\RemoveDuplicatePF\latest.zip`, which only matters for releases.

A good build ends with `Build succeeded. 0 Warning(s) 0 Error(s)`. Treat new warnings as something to fix.

## 3. Load it in game (first time only)

1. Start the game through XIVLauncher.
2. Type `/xlsettings` in chat, then open the **Experimental** tab.
3. Under **Dev Plugin Locations**, paste the **full path to the DLL**, then click **+** and **Save and Close**. For example:

   ```
   C:\Users\<you>\Documents\VSCode\ffxiv-duplicate-pf\RemoveDuplicatePF\bin\x64\Debug\RemoveDuplicatePF.dll
   ```

4. Type `/xlplugins`, then go to **Dev Tools > Installed Dev Plugins**.
5. **Remove PF Duplicates** should be listed. Enable it.

Dalamud remembers the location, so you only do steps 2 and 3 once. If you also install the released version later, disable one of the two: never run both at the same time.

> **Point the game at the Debug DLL.** `dotnet build` only updates `bin\x64\Debug\`. If the location points at `bin\x64\Release\` instead, the game keeps running whatever was last built as Release, which can be days old. To change it, remove the old location in `/xlsettings` > **Experimental** and add the Debug path. Use `dotnet build -c Release` only when you specifically want to test the shipped build.

### Automatic reloading (recommended)

In `/xlplugins` > **Dev Tools > Installed Dev Plugins**, open the plugin's settings and turn on **automatic reloading**. Dalamud then watches **the exact DLL your dev location points at**, and reloads the plugin about half a second after that file changes. Building the configuration the location points at is enough; you don't need to touch the game.

It only watches that one file. Building the other configuration changes nothing in game.

## 4. Check that it loaded

Type `/xllog` to open the Dalamud log. Type `RemoveDuplicatePF` in its filter box to show only this plugin's messages. You should see:

```
[RemoveDuplicatePF] Remove PF Duplicates loaded (Debug build, built 2026-11-11 10:36:02, from C:\...\bin\x64\Debug\RemoveDuplicatePF.dll)
```

**Check this line every time you test.** The configuration, build time and path tell you exactly which build is running:

- **The build time is older than your last `dotnet build`:** the game is running a stale build. Check that the path is the configuration you built, then reload.
- **The path says `Release` but you ran plain `dotnet build`:** your dev location points at the wrong folder (see the note in section 3).

Disable the plugin in `/xlplugins` and you should see `... unloaded`. Red errors (`ERR`) or exceptions mentioning the plugin are bugs.

The same log is saved to `%APPDATA%\XIVLauncher\dalamud.log`. That file is useful to attach to an issue.

## 5. The edit and test loop

1. Change the code.
2. Run `dotnet build`.
3. With automatic reloading on, the plugin reloads by itself. Otherwise, in `/xlplugins` > **Installed Dev Plugins**, click the plugin's **reload** button, or disable and re-enable it. Either way, there's no need to restart the game.
4. In `/xllog`, confirm the new `loaded (...)` line shows the build time you just built.
5. Test the change in game, and watch `/xllog`.

If the build fails because the DLL is in use, disable the plugin in game, build, then enable it again.

For a full test session (log setup, which steps to run, and how to report results so they can be checked against `dalamud.log`), follow **[docs/in-game-testing.md](docs/in-game-testing.md)**.

### Testing the Party Finder behaviour

Once the filtering features land (#7 to #10):

- Open Party Finder from the Duty menu. Categories with lots of advertising (for example venue ads) usually contain duplicates.
- Compare the list with the plugin enabled and disabled. Each set of duplicate listings (same first 30 characters) should collapse into one with a coloured `[N]` prefix, and the window title should show the number of listings combined.
- Change category, search, refresh and change page. The counts should reset and recompute each time (#8).
- Close the window and disable the plugin. The game should behave exactly as it did before (#11).

## 6. Unit tests

The duplicate-grouping and prefix logic lives in `RemoveDuplicatePF.Core`, a library that doesn't reference Dalamud or the game. That lets `RemoveDuplicatePF.Tests` (xUnit) test it without launching anything. A test fails if Core ever references Dalamud, game structs or Lumina, so game-specific code belongs in the plugin project. Run the tests with:

```sh
dotnet test
```

Unit tests cover the logic. The in-game check covers the hooks and UI. A change to either needs the matching check.

## 7. Before opening a PR

- [ ] `dotnet build` succeeds with no new warnings
- [ ] `dotnet test` passes
- [ ] The plugin loads and unloads cleanly in game, with no errors in `/xllog`
- [ ] Anything that changes Party Finder behaviour was tried in the real Party Finder window
- [ ] The PR body says `Closes #<issue>` and lists what you tested in game

## 8. Releasing a new version

The version lives in `<Version>` in [RemoveDuplicatePF/RemoveDuplicatePF.csproj](RemoveDuplicatePF/RemoveDuplicatePF.csproj), because the official Dalamud repo builds the csproj exactly as committed (#110). A release bumps it in a commit, then pushes a matching tag. Everything else is automated by [.github/workflows/release.yml](.github/workflows/release.yml).

1. Make sure `main` has everything you want to release, CI is green, and the manual tests pass.
2. Pick the next version (`vMAJOR.MINOR.PATCH`, for example `v1.0.1` for a fix, `v1.1.0` for a new feature).
3. Set `<Version>` in the csproj to that version with `.0` on the end (`v1.0.1` → `1.0.1.0`), and merge that change to `main` through a PR like any other change.
4. Tag `main` and push the tag:

   ```sh
   git checkout main
   git pull
   git tag v1.0.1
   git push origin v1.0.1
   ```

5. The **Release** workflow then:
   - checks that the tag matches `<Version>` in the csproj, and stops without releasing anything if it doesn't;
   - builds and tests;
   - publishes a **public** GitHub Release named after the tag, with `latest.zip` attached and generated release notes;
   - regenerates `repo.json` on `main` (version, download links, Dalamud API level, update time) with [.github/scripts/make_repo_json.py](.github/scripts/make_repo_json.py), and commits it.
6. Players get the update the next time Dalamud checks the custom repo:

   ```
   https://raw.githubusercontent.com/pearstopher/FFXIV-Remove-PF-Duplicates/main/repo.json
   ```

A pushed tag publishes a public release straight away. If a release goes wrong, fix it on `main` and release a new version rather than reusing the tag. If the version check fails, nothing was published: delete the tag (`git push origin :v1.0.1` and `git tag -d v1.0.1`), fix the csproj, and tag again.

A unit test also fails if the csproj version is ever older than the one `repo.json` already publishes.

## 9. Staying up to date after patches

Two things run automatically (#14):

- **Daily CI against the latest Dalamud.** The **Build and test** workflow runs on a schedule (09:17 UTC daily) as well as on pushes and PRs. It always downloads the newest Dalamud, so if a Dalamud or game patch breaks the plugin, a scheduled run fails and GitHub emails the repo owner.
- **Dependabot PRs every Monday** (`.github/dependabot.yml`) for:
  - `Dalamud.NET.Sdk`, whose version lives in `global.json` under `msbuild-sdks` (not in the csproj);
  - the test project's NuGet packages, grouped into one PR;
  - the GitHub Actions versions used in the workflows.

  Each Dependabot PR runs CI. Merge it if CI is green; if it's red, the PR shows what needs fixing.

### Patch-day checklist

When the game or Dalamud updates:

1. **Wait for Dalamud.** After a game patch, Dalamud and plugins are disabled until Dalamud releases a compatible build. Nothing in this repo can speed that up.
2. **Check CI.** Run **Build and test** manually (Actions tab → *Build and test* → *Run workflow*) or wait for the daily run.
   - **Green:** nothing to do. If the Dalamud **API level** didn't change, players' installed copy keeps working.
   - **Red:** read the failed step's log. Usual causes: a renamed or removed Dalamud API, or a changed game struct in FFXIVClientStructs.
3. **If the Dalamud API level changed** (for example 15 → 16), Dalamud won't load plugins built for the old level:
   1. Merge the Dependabot PR for the new `Dalamud.NET.Sdk`, or bump it by hand in `global.json`.
   2. Fix any build errors, run `dotnet test`, and run the manual tests in game (`docs/manual-tests.md`).
   3. Release a new version (section 8), for example `v1.0.1`. The release workflow puts the new `DalamudApiLevel` into `repo.json` automatically, and players get the update from the custom repo.
4. **If only game structs changed** (the build passes but something misbehaves in game), check `/xllog` for `Party Finder title text node not found` or other errors. The title and prefix code (`TitleUpdater`, `ListingPrefixer`) is what depends on the game's UI layout.

## 10. Updates in the official repo

The plugin isn't in the official Dalamud repo yet. The submission files are drafted in [docs/official-repo-submission/](docs/official-repo-submission/README.md), and submitting is #115. Once it's accepted, updates work like this:

- **The plugin starts in testing.** New plugins must go into `testing/live/` in [goatcorp/DalamudPluginsD17](https://github.com/goatcorp/DalamudPluginsD17). When it's working there, a PR moves the folder to `stable/`. Moving needs no version bump or new commit.
- **Each update is a PR to DalamudPluginsD17**, from a new branch, changing `commit` (and `changelog`) in the plugin's `manifest.toml`. One plugin per PR.
- **Updates can go straight to stable** after that. There's no requirement to ship testing builds; testing the update yourself (section 7, the manual tests) is enough.
- **Bump `<Version>` in the csproj first** (section 8). The build bot builds the commit exactly as it is, so the version must already be right in that commit.
- **Players get it after the Dalamud team reviews and merges the PR**, so official updates aren't instant. The GitHub release from section 8 only updates the custom repo (`repo.json`).
- **Keep releasing to the custom repo too.** It stays as a backup after the plugin is official, but the README no longer tells players to add it ([#112](docs/dalamud-repo-standards.md#the-custom-repo-after-acceptance-112)).

## Troubleshooting

| Problem | Fix |
| --- | --- |
| Build error about Dalamud references or `DALAMUD_HOME` | Run the game with Dalamud once, or set `DALAMUD_HOME` (see section 1). |
| `dotnet` is not recognized | Install the .NET 10 SDK, then restart the terminal or VS Code. |
| The plugin isn't listed under Installed Dev Plugins | Check that the path in `/xlsettings` points at the `.dll` file itself and that the file exists. |
| The plugin is listed but won't load | Look in `/xllog` for the exception. After a game patch, Dalamud may need an update first (#14). |
| My change didn't take effect | Compare the build time in the `loaded (...)` line with your last build. If it's older, you built a different configuration from the one your dev location points at; point the location at Debug (section 3), then rebuild or reload. |
| The plugin icon is missing or outdated in `/xlplugins` | Dalamud caches each plugin's icon (including "no icon") the first time the installer shows it, and doesn't recheck on reload. Type `/xldev`, choose **Plugins → Clear cached images/icons**, then reopen `/xlplugins`. The icon comes from `images/icon.png` (512×512 square PNG) and is copied next to the DLL on build. |

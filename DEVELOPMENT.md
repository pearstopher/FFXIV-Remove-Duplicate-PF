# Development guide

How to build Remove Duplicate PF on your PC, load it into the game, and check that a change works before opening a PR.

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
5. **Remove Duplicate PF** should be listed. Enable it.

Dalamud remembers the location, so you only do steps 2 and 3 once. If you also install the released version later, disable one of the two: never run both at the same time.

## 4. Check that it loaded

Type `/xllog` to open the Dalamud log. Type `RemoveDuplicatePF` in its filter box to show only this plugin's messages. You should see:

```
[RemoveDuplicatePF] Remove Duplicate PF loaded
```

Disable the plugin in `/xlplugins` and you should see `... unloaded`. Red errors (`ERR`) or exceptions mentioning the plugin are bugs.

The same log is saved to `%APPDATA%\XIVLauncher\dalamud.log`. That file is useful to attach to an issue.

## 5. The edit and test loop

1. Change the code.
2. Run `dotnet build`.
3. In `/xlplugins` > **Installed Dev Plugins**, click the plugin's **reload** button, or disable and re-enable it. The new build is loaded without restarting the game.
4. Test the change in game, and watch `/xllog`.

If the build fails because the DLL is in use, disable the plugin in game, build, then enable it again.

### Testing the Party Finder behaviour

Once the filtering features land (#7 to #10):

- Open Party Finder from the Duty menu. Categories with lots of advertising (for example venue ads) usually contain exact duplicates.
- Compare the list with the plugin enabled and disabled. Each set of identical listings should collapse into one with a coloured `[N]` prefix, and the window title should show the number of listings combined.
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

## Troubleshooting

| Problem | Fix |
| --- | --- |
| Build error about Dalamud references or `DALAMUD_HOME` | Run the game with Dalamud once, or set `DALAMUD_HOME` (see section 1). |
| `dotnet` is not recognized | Install the .NET 10 SDK, then restart the terminal or VS Code. |
| The plugin isn't listed under Installed Dev Plugins | Check that the path in `/xlsettings` points at the `.dll` file itself and that the file exists. |
| The plugin is listed but won't load | Look in `/xllog` for the exception. After a game patch, Dalamud may need an update first (#14). |
| My change didn't take effect | Make sure you rebuilt the same configuration (Debug or Release) that your dev plugin path points to, then reload. |

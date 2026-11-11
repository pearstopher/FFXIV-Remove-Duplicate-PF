# In-game testing guide

How to run a test session so the results can be checked two ways: by what you see in game, and by the plugin's lines in `dalamud.log`, which Claude can read directly.

First-time setup (installing the SDK, adding the dev plugin) is in [DEVELOPMENT.md](../DEVELOPMENT.md). The test steps themselves are in [manual-tests.md](manual-tests.md).

## Before a session

1. **Build.** Run `dotnet build` from the repo root.
2. **Check the dev location points at Debug.** In `/xlsettings` > **Experimental** > **Dev Plugin Locations**, the path should end in `bin\x64\Debug\RemoveDuplicatePF.dll`. If it ends in `Release`, either change it to Debug or always build with `dotnet build -c Release`. Plain `dotnet build` never updates the Release DLL.
3. **Quiet other plugins' logging.** `dalamud.log` stops being written once it reaches **100 MB** in a game session (#32). Plugins with debug logging on can fill that in a few hours. Before testing, turn off debug logging in noisy plugins, or disable them for the session:
   - **VFXEditor:** turn off its debug logging (`LogDebug` and `LogVfxDebug` in its config).
4. **Start or restart the game.** Each launch starts a fresh `dalamud.log` (the previous one's last 10 MB moves to `dalamud.old.log`). If the game has been running for a while with noisy plugins, restart it so the file has room.
5. **Set the log level.** In `/xllog`, set the level dropdown to **Debug+**. This one setting controls both `/xllog` and `dalamud.log`, and it's saved, so you only need to do it once.
6. **Confirm the right build loaded.** In `/xllog`, filter for `RemoveDuplicatePF` and check the newest line:

   ```
   Remove Duplicate PF loaded (Debug build, built <time of your last build>, from ...\bin\x64\Debug\RemoveDuplicatePF.dll)
   ```

   If the time is older than your last build, the game is running a stale build. Reload the plugin in `/xlplugins`, or fix the location (step 2).

## During a session

1. **Note the start time** (your PC clock is fine).
2. Open [manual-tests.md](manual-tests.md) and run the section for the feature being tested, in order.
3. For each step, note **pass** or **fail**. For anything odd, note the **time** and what you saw (a screenshot helps).
4. Don't change `/xllog` filters or levels mid-session.

## After a session

Reply in the chat (or in the PR or issue) with:

```
Tested: <feature or issue number>
Window: <start time> – <end time>
Build line: <the "loaded (...)" line from /xllog>
Results:
  Step 1: pass
  Step 2: fail – <what you saw, and when>
  ...
```

With the time window, Claude can read `%APPDATA%\XIVLauncher\dalamud.log`, filter for `RemoveDuplicatePF`, and check the plugin's own record of what it did. For example, each page's `PF result set #…` line shows how many listings were hidden and which duplicate groups they belonged to, and the `PF on screen` and `PF prefix` lines show which page the title and prefixes are using.

If the game is still running, there's no need to close it. The log is written as you go.

## If the log file has nothing for your session

- **The file's last line is before your session:** the 100 MB limit was hit earlier in this game session. Restart the game and repeat the session (see "Before a session", steps 3 and 4).
- **Only `Information` lines, no `Debug`:** the `/xllog` level isn't **Debug+**.
- **No `RemoveDuplicatePF` lines at all:** the plugin isn't enabled, or the dev location points somewhere else. Check `/xlplugins`.

# QuestHelper v0.3.0 — first region/task organiser test

The user's latest screenshots proved v0.2.x can inject HTML into the **generic** Orbus client, but its blue navigation bar was mispositioned on Achievements. v0.3.0 **removes it entirely**, and targets the real \`missions-container\` instead of the always-present \`missions-tab\` navigation button.

## What should appear

When Inventory → Missions is selected, missions should be organised by **region first, activity second**, while keeping all the game's original mission icons and matching its parchment/font conventions. Headers should collapse when PowerUI native click binding is supported; when not supported, they stay expanded.

Mission ID mapping covers 152 unambiguous IDs from the user's Missions sheet, with live Mission data fallback. The report logs how many currently visible mission icons were found and categorised. Unknowns remain visible.

## Build without publishing to RM (safer first)

Close the game, update the clone and run:

\`\`\`powershell
git pull origin main
dotnet build .\src\OrbusQuestHelper\OrbusQuestHelper.csproj -c Release "-p:GameDir=$env:LOCALAPPDATA\OrbusRebornModdingInstances\Modded Orbus"
\`\`\`

If that compiles, close Orbus, back up any installed QuestHelper DLL, and put \`src\OrbusQuestHelper\bin\Release\net6.0\OrbusQuestHelper.dll\` inside the **specific game's** \`BepInEx\plugins\` folder for testing. RebornManager stable releases still require \`.\release.ps1\` after verification. To test against a generic local installation, supply its path via \`-p:GameDir=...\`.

## In-game check

1. Open the normal Missions tab, then scroll through the first and last regions. There should be **no** Weekly/Regular/Top buttons or blue UI panels.
2. Verify the list contains the same original icons as before and icon hover tooltips still work.
3. Click a region heading to check whether it collapses (if supported). Try an activity heading as well.
4. Switch to Achievements and back. Achievements must remain unchanged, and every mission icon must still be present.
5. Close/reopen the menu, or change zones. Nothing should disappear; the grouping should reapply when missions refresh.
6. Record whether each client works: generic original vs FourOfSpades modified client.

The game is the source of truth for mission state. Do **not** submit/turn in any mission just to test the mod.

## Report

If the game page is unchanged or the layout breaks, attach the active instance's \`BepInEx\LogOutput.log\`. Look for:
- \`[QuestHelper] v0.3.0\`
- \`[QuestHelper] PowerUI runtime: ...\`
- \`[QuestHelper] Region/task layout applied: ...\`, or
- \`[QuestHelper] Missions container not found...\`
- \`[QuestHelper] No vendor-mission tiles detected...\`

The new code uses build-neutral PowerUI reflection, but **neither variant has been compiled or VR-tested by the assistant**. Any failed native delegate support must leave groups expanded, and an unknown DOM should leave the vanilla page untouched.

### Disable / recover

Close Orbus, edit \`BepInEx\config\com.horizon.orbus.questhelper.cfg\`, under \`[Missions]\` set \`EnableRegionTaskGrouping = false\`, then restart. To completely revert, uninstall QuestHelper from that game instance (leave OrbusRebornCamera installed).

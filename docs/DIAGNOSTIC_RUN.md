# QuestHelper v0.3.3 — region/task organiser and missing-UI bugfix

v0.3.0 log: plugin registered successfully; a missing-icons warning appeared, then it reported `Native page 1 is not Missions`. The guard was incorrect: `currentInventoryPage` is numeric and cannot be compared with the string `missions`. v0.3.3 drops that guard and uses a scoped recursive PowerUI element walk if IL2CPP lists cannot be enumerated normally. It also logs the presence of `vendor-mission` in the Missions container HTML and a few element class names, without dumping arbitrary player HTML.

The user's latest screenshots proved v0.2.x can inject HTML into the **generic** Orbus client, but its blue navigation bar was mispositioned on Achievements. v0.3.3 **removes it entirely**, and targets the real \`missions-container\` instead of the always-present \`missions-tab\` navigation button.

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
- \`[QuestHelper] v0.3.3\`
- \`[QuestHelper] PowerUI runtime: ...\`
- \`[QuestHelper] Region/task layout applied: ...\`, or
- \`[QuestHelper] Missions container not found...\`
- \`[QuestHelper] No vendor-mission tiles detected...\`

The new code uses build-neutral PowerUI reflection, but **neither variant has been compiled or VR-tested by the assistant**. Any failed native delegate support must leave groups expanded, and an unknown DOM should leave the vanilla page untouched.

### Disable / recover

Close Orbus, edit \`BepInEx\config\com.horizon.orbus.questhelper.cfg\`, under \`[Missions]\` set \`EnableRegionTaskGrouping = false\`, then restart. To completely revert, uninstall QuestHelper from that game instance (leave OrbusRebornCamera installed).


## v0.3.3 native grid repair (after scuffed screenshots)

The v0.3.1 log confirmed **56 original mission icons discovered; 48 had region matches; 53 had task matches; 41 expandable headers**, but the layout was visually broken: original weekly/level headings persisted at the top, icons were jumbled over them, and "Highsteppe / Critter Capture" appeared outside the bottom of the physical menu.

Two specific source-level issues were fixed:
1. The original contents were being moved into an archive that was still appended to `missions-container`. On the legacy PowerUI renderer this "hidden" archive continued consuming/displaying layout. v0.3.3 keeps it DETACHED (not part of the render tree) and makes the new root the only visible mission content within the existing scroll container.
2. Quest icons have native float-based layout in `inventory-box-lowres`. v0.3.3 sets the native `vendor` class on its root, gives section containers a clear rule, and appends a row-clearer only after all the original icons have been placed in each task group.

Additionally, the grouped root must contain exactly the number of original native `vendor-mission` icon nodes before the modification is accepted; otherwise the mod attempts to restore the vanilla DOM. Original hover/click handlers remain on original elements. We still need to test scrolling, heading clicks, switching tabs, and whether the game renderer visually honours detached archives.

**If ANY icons or text appear outside the parchment or overlap, disable the organiser** (`EnableRegionTaskGrouping = false`) and attach screenshots plus only the `[QuestHelper]` lines from the BepInEx log. Do not turn in missions while the UI looks broken.


## v0.3.3 empty layout follow-up (2026-10-19)

The user's v0.3.2 `LogOutput(5).log` showed successful loading/registration, **no crash**, and exactly one early message that `missions-container` had no `vendor-mission` nodes yet (native list type: `Il2CppSystem.Collections.Generic.List<PowerUI.Element>`, sampled DOM nodes=1, no HTML mission markers). It did **not** contain a successful grouping report or a reason for abandoning layout after the page was populated.

Likely source issue: in v0.3.2, a new requirement that the PowerUI `style.clear` setter succeed on a detached row-clearing element silently returned from `Apply` before any modifications. The new `className = vendor` setter likewise could be missing in an older generated interop. These are **hypotheses until the next log confirms the exact stage**.

v0.3.3 now uses `setAttribute("class",...)` / `setAttribute("style",...)` when the runtime exposes those APIs, with CLR setter fallback; reports all known early-stage failures via one-time `[QuestHelper] Apply stopped BEFORE changes: ...` notices; logs when the Missions DOM becomes populated and a low-frequency `[QuestHelper] DOM heartbeat` for the live document.

The original UI must be preserved on rejected preparation. If the new layout still fails, **send the new LogOutput.log rather than blind screenshots**. Look for the first `Apply stopped BEFORE changes`, `Mission icon DOM populated`, or `Region/task layout applied` entry. Do not turn in quests while the menu is malformed.

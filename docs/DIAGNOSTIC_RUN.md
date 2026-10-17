# First VR diagnostic test

1. Build and install OrbusQuestHelper.dll using the README instructions. Remove any old OrbusQuestDiagnostics.dll prototype to prevent duplicate scans.
2. Launch OrbusVR normally with BepInEx enabled.
3. Open Missions. Scroll through your active missions, including critter, salvage, fishing and event objectives. Leave the screen open for 10 seconds and take screenshots.
4. Open the physical story journal and inspect several pages including an active NPC/collection objective. Take screenshots.
5. Optional manual snapshot: focus the Windows game window and press Ctrl+Alt+K while the desired page is open. RebornCam's F-key controls are unaffected.
6. Close the game. Upload text reports from BepInEx/config/OrbusQuestHelper/Diagnostics/ with screenshots.
7. Review reports before sharing: game-visible labels could include personal text.

Reports only identify candidate scene hierarchies and native types; they do not yet reveal all mission IDs or prove journal button editability. If there is a freeze, remove the DLL and provide the relevant BepInEx log.

# QuestHelper v0.1.2 — combined UI diagnostics

The user's v0.1.1 test confirmed QuestHelper loaded, but the scanner never captured: the old implementation required a scene literally named "Overworld". OrbusVR Community Edition instead loads zones such as ZoneHighsteppe, PlayerHouse, and instanced scenes. **v0.1.2 removes that scene-name gate entirely.**

This is a read-only UI discovery mod. It surveys loaded Unity objects and their native IL2CPP component types (including SuperTextMesh), lists ranked candidates and their hierarchies, and does not alter gameplay, missions, or journal data. Custom SuperTextMesh text content is NOT read yet.

## Test through RebornManager

1. Build and publish v0.1.2 with the normal local release.ps1 workflow. Then exit Orbus and update QuestHelper through RebornManager.
2. Launch Orbus normally. Automatic snapshots now run every ~20 seconds regardless of scene name, including Highsteppe, PlayerHouse, other zones and character select. Up to 40 snapshots can be taken in one session.
3. Open **inventory > Missions**, hover several mission icons, scroll, and open the story **Journal**. Keep each menu visible across at least one 20-second automatic snapshot.
4. To capture a particular menu, focus the Windows Orbus window and press Ctrl+Alt+K to append an immediate manual snapshot. Up to 30 manual captures are supported.
5. Close the game and upload the **newest one** file ending in _QuestHelperSession.txt from:

       BepInEx\config\OrbusQuestHelper\Diagnostics\

All automatic and manual snapshots are appended to one session file with timestamps and separators. Old v0.1.0 reports ending in _Missions.txt or _Journal.txt are not relevant to the current test.

The first snapshot creates the report (about 20 seconds after game start), not when the plugin loads. Look for a "[QuestDiag] Appended snapshot" line in BepInEx/LogOutput.log. If there is a "Capture failed" warning instead, provide that log. If there are no snapshot messages after 30 seconds of gameplay, send the log so we can investigate further.

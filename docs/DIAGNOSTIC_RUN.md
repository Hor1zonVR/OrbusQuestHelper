# QuestHelper v0.1.1 diagnostic test — combined session log

v0.1.0 produced separate repeated text files and mostly found unrelated terrain meshes named _Quest. v0.1.1 inspects custom native IL2CPP game UI components (including SuperTextMesh) and records ranked UI candidates, inactive templates and nearby hierarchies. It is **read-only** and does not change quests or menus.

## Test through RebornManager

1. Publish the updated v0.1.1 source with local release.ps1, then close Orbus and update QuestHelper in RM. The v0.1.1 tag has not been released yet at the time of this change.
2. Launch OrbusVR, enter the Overworld and open **inventory > Missions**. Scroll and hover different mission icons to show the tooltips.
3. Open the physical journal and flip through multiple pages containing active story objectives.
4. The mod automatically takes UI snapshots about every 20 seconds once the Overworld is loaded, up to 40 times in one run. Both auto and manual captures are **appended to one text file** for that game session, with timestamps and clear separators.
5. To capture a specific moment, focus the Windows game window and press **Ctrl+Alt+K** while the Missions or Journal UI is open. Up to 30 manually requested snapshots can be appended to the same file.
6. Close Orbus. Upload just the **newest** file ending in _QuestHelperSession.txt, located at:

       BepInEx\config\OrbusQuestHelper\Diagnostics\

The combined session file is created with the first successful snapshot; no file is created on initial plugin load. Older v0.1.0 separate _Missions.txt / _Journal.txt files can be ignored or deleted.

Reports enumerate Unity game objects and native component types. They do not yet read custom SuperTextMesh text or prove which internal quest methods can safely be called. If a scan fails or freezes the game, remove the DLL and send the BepInEx log.

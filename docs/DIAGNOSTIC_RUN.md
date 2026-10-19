# QuestHelper v0.1.6 — dynamic player menu and native VR input

The v0.1.5 report from 2026-10-19 17:42–17:45 confirmed the mod discovered both Wacki.ViveUILaserPointer components. There were 350 candidate physics hit changes but no EventSystem selection changes. Physics rays repeatedly hit terrain, buildings, tools, and quest trigger colliders instead of the hovered Missions icons.

New discovery: PlayerRigv4/Player Menu/TVScreenContent changed from one child to 60 and then 104 children, mainly unnamed mesh objects. These are not confirmed mission entries. The game may render menus with a custom mesh pipeline, rather than Unity UI Button components.

v0.1.6 changes:
- Observe TVScreenContent changes automatically every ~2 seconds, logging direct-child count, sample mesh names, materials and component types.
- Filter ray hits to likely UI targets, including the Player Menu, journal, map, dialog, and the UI-Interactable layer. Ignore world terrain noise.
- Record native IL2CPP class method signatures and field TYPES for Wacki.ViveUILaserPointer, active BaseInputModule implementations and EventSystem. This does not read object field values or invoke game methods.
- Continue writing a single file per session ending in _UiInteractions.txt.
- Ctrl+Alt+K still takes a manual active Canvas/Selectable snapshot.

This is an experimental read-only diagnostic, not the finished Missions overhaul. The native input inspector may report unavailable metadata if the runtime omits its exports.

## Test
1. From your local QuestHelper repository: git pull origin main; then .\release.ps1. Close Orbus and update QuestHelper in RM.
2. Launch the game, open Inventory > Missions, hover the approximately six weekly missions at the top, then scroll down and hover several regular missions.
3. Keep the player menu open at least 20 seconds and switch tabs a couple of times. Open the physical journal afterwards, if convenient.
4. Send the newest BepInEx/config/OrbusQuestHelper/Diagnostics/*_UiInteractions.txt report.

Look for sections labelled PLAYER MENU CONTENT CHANGED and NATIVE VR POINTER / INPUT MODULE METADATA, plus filtered VR RAY TARGET events.

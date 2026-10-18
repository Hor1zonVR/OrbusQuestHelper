# QuestHelper v0.1.1 diagnostic test

v0.1.0 was selecting unrelated terrain meshes whose names end in _Quest, and repeatedly reporting the same JournalCaptureSystem objects. The first 16 reports showed SuperTextMesh on the journal capture templates but did not identify the actual Missions tab or page content.

v0.1.1 is a *read-only UI discovery* pass. It scans active and inactive transforms in loaded scenes for actual native IL2CPP types and SuperTextMesh, lists ranked component/object candidates, and inspects neighbouring hierarchy. No quest state is read or changed. Text displayed through the custom SuperTextMesh component is not decoded yet.

## Test using RebornManager

1. Publish v0.1.1 using the local release.ps1 script, then close Orbus and refresh RM to update QuestHelper on your test instance.
2. Launch Orbus and open **inventory > Missions**. Keep it visible while you inspect several mission icons and tooltips.
3. Open the physical Quest journal and flip through pages with active story objectives.
4. The mod saves **at most three** automatic snapshots after the Overworld loads: approximately 25 seconds after plugin initialization, then 50 seconds apart. A screen is only captured if it is open when the snapshot happens.
5. If one of the menus was missed, focus the Windows game window and press **Ctrl+Alt+K** while the relevant UI is open for a manual snapshot.
6. Close the game. Upload only the *new* files ending in _UI.txt from BepInEx/config/OrbusQuestHelper/Diagnostics/ along with screenshots; do not resend the v0.1.0 spam.

If the scanned classes still don't identify the specific menu widgets, the next step is a narrower metadata-driven probe into MagicalActual inventory/journal components. Do not change game UI until relevant methods are verified.

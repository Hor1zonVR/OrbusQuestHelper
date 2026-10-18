# QuestHelper v0.1.3 — targeted Mission and Journal diagnostics

## Why a new pass?

The user confirmed the inventory Missions tab was OPEN in the v0.1.2 test. That report still showed unrelated PlayerHouse books, tutorial QuestCollider objects and Wardrobe InventoryIcons because the ranked top-220 and hierarchy cap filled before the actual Missions interface. An absence from the prior report does NOT establish that the Missions UI is inaccessible.

v0.1.3 retains read-only scanning and adds independent, high-priority sections to the single session file:

- MISSION / QUEST UI COMPONENTS: native class types and full paths, excluding scenery QuestCollider.
- INVENTORY / MENU COMPONENTS: menu and inventory objects, excluding house wardrobe.
- ACTIVE CANVAS ROOTS: Unity canvas objects active when captured.
- ACTIVE UI TEXT: MISSION/INVENTORY KEYWORDS: UnityEngine.UI.Text labels, with paths.
- FOCUSED MISSION / INVENTORY UI HIERARCHIES: direct child structure of candidates, not random high-scoring books.
- Existing generic scan and the working journal capture remain for cross-checking.

**The scanner still does not read custom SuperTextMesh text or quest progress and does not change any quest data.** It is source code only until built and published.

## Next test

1. With Orbus closed, use git pull origin main followed by .\release.ps1 in the cloned repository. Update QuestHelper through RebornManager. v0.1.3 uses the same public stable release workflow.
2. Launch Orbus and open **inventory > Missions**. Keep that tab open for 15–25 seconds while the mod captures several snapshots (approximately every five seconds). Hover missions to bring up their tooltips.
3. Open the physical Journal for another 15 seconds, turning a page.
4. Exit Orbus and send the NEWEST single BepInEx/config/OrbusQuestHelper/Diagnostics/*_QuestHelperSession.txt file.

We need to see whether the UI is constructed by normal Unity Canvas elements, gameplay-specific MagicalActual behaviours or a different rendering pipeline. The targeted report is designed to distinguish those possibilities; it does not assume a particular mission API exists.

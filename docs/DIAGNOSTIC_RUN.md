# QuestHelper v0.1.5 — VR UI Interaction Diagnostics

## Why change?

The weekly missions live at the top of the **same scrollable Missions page** as the regular missions (about 6 weekly and 46 regular, user estimates). In earlier diagnostics the inventory page was open, but broad scene ranking repeatedly reported unrelated scenery, books and wardrobe components instead of the icons actually being hovered.

Rather than repeatedly dump thousands of unrelated objects, v0.1.5 uses **read-only interaction-oriented inspection**:

- Discover the two hand UI pointers: PlayerRigv4/Controller (left|right)/Grabber/UIPointer.
- Poll physics raycast candidates in the pointer direction ~7 times/sec and report only when the hit object changes; record the complete parent hierarchy, native IL2CPP components, nearby sibling names and short descendant trees.
- Also report Unity EventSystem.currentSelectedGameObject changes (if supported), which may detect selection or clicks even when an icon has no physics collider.
- Manual Ctrl+Alt+K in the focused Windows game window captures active Unity Canvas and Selectable names for canvas elements that raycasts miss.
- **One new _UiInteractions.txt report file per game session**. Automatically flush buffered observations every ~1.5 seconds.
- The v0.1.4 bulk scanner is no longer activated. Older _QuestHelperSession.txt files can be ignored.

**Limitations:** Raycast targets are candidates, not guaranteed actual UI hover callbacks. Standard Unity Canvas buttons without physics colliders might not register as physics hits; EventSystem only exposes selection and does not necessarily report hover. If the laser input module uses a custom internal raycaster, the next step will be to instrument its actual event methods based on native metadata.

The scanner never invokes quest methods or consumes clicks. It does not capture chat and input field text intentionally. Review log text before sharing.

## Test

1. Publish v0.1.5 with the existing .\release.ps1 script and update through RebornManager.
2. Enter Orbus. Open inventory > Missions.
3. Aim your **left** laser pointer at several weekly mission icons, then scroll down and point at regular mission icons. Repeat with the right pointer.
4. Hover one mission long enough to display its tooltip, and click an icon if that normally shows further information. No specific input is required by the mod.
5. Open the Journal and hover its bookmark and page controls. Press Ctrl+Alt+K on the PC with game focus once while Missions is open, if possible.
6. Exit the game. Send the newest file ending in _UiInteractions.txt from BepInEx/config/OrbusQuestHelper/Diagnostics/.

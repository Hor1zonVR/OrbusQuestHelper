# QuestHelper v0.1.4 — Missions UI discovery and component inventory

The latest v0.1.3 report logged 28 snapshots (23:05:13–23:07:28). The user estimates about 46 regular missions and 6 weekly missions (approximate comparison target, not verified by Unity data). No matching mission icon grid was identified. \`PlayerRigv4/Avatar/Quests\` was inactive and had an empty Content grid. The "activeCanvas" count of around 1,841 was misleading because v0.1.3 matched CanvasRenderer, not just real Canvas components.

v0.1.4 adds:
- A separate, accurate count of native UnityEngine.Canvas components, and named paths for active Canvas objects outside the wardrobe.
- Counts and paths of MagicalActual.InventoryIcon components **outside** wardrobe/transmog interfaces.
- A frequency inventory of MagicalActual, ExtUI, PlayfulSystems and Wacki native component type names (not mission entry data).
- Existing journal/mission scene hierarchy observations, active text discovery and a single combined diagnostics file.

The scanner remains entirely read-only. It does NOT yet read or modify quest progress, and still doesn't decode custom SuperTextMesh text.

## Next quick test

1. Run \`git pull origin main\` and \`.\release.ps1\` locally, then update QuestHelper through RM with Orbus closed.
2. Open inventory > Missions, keep it open around 20 seconds (four 5-second snapshots). Hover regular missions and the weekly missions in turn.
3. Open and close the inventory to create a before/after comparison if convenient.
4. Upload the latest \`BepInEx/config/OrbusQuestHelper/Diagnostics/*_QuestHelperSession.txt\` report.

This should clarify if mission UI entries are scene Canvas/InventoryIcon objects, or if we must inspect a different game menu implementation.

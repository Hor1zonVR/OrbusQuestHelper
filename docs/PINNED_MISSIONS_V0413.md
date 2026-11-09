# QuestHelper v0.4.13 candidate — pinned objectives and level-bar placement

**Status:** source change on a review branch; **not a released DLL**. Requires CI / compilation with a real game interop set and live VR test on Spades. No server request or progress/reward modification has been added.

## Four reported symptoms and the actual cause

| Symptom | Finding | Candidate change | Remaining validation |
|---|---|---|---|
| Auto tracker reattached to VR headset | v0.4.12's `Auto` condition explicitly prefers `eye.stereoEnabled` | `Auto` now follows the native level/XP **world pose**, but the QuestHelper `TextMesh` remains detached so native scale changes cannot enlarge it; only explicit `Headset` anchors to the camera | Verify at headset scale, during Silver/Gold tier changes and after lobby transitions |
| `98/10000` instead of useful progress | A **real** Spades capture mission sample exposed `ChecklistItem.timesToKill=10000`, `killCount=21` and empty `ChecklistItem.description` | Prefer the next **verified native** Bronze/Silver/Gold requirement when `Mission.progress` agrees with the checklist count. If no usable small milestone exists, show the **true earned count** with an action unit (e.g. `98 captured`), not `98/10000` | Confirm actual native reward requirements for your pinned missions and verify stage transitions |
| Long mission story displayed on HUD | `Mission.description` is narrative fallback when `ChecklistItem.description` is empty | Prefer actionable checklist text, then two previously verified exact aliases, then an actual imperative in the mission description, then the known mission **activity category** (e.g. `Capture critters`, `Brew potions`, `Defeat monsters`) | Exact target species/item names are **not known for every quest**; the catalogue only has ID→activity/region |
| Progress requires reopening Missions | The v0.4.12 `MissionHudDriver` already polls `GetMission` / Spades `ceh.ljn` every 0.85 s independently of the UI | Polling reduced to **0.5 s** to pick up **locally refreshed** native counters sooner | If the Spades mission record remains stale until a server request is made, this is **not fixed**. A suitable obfuscated Spades entry point for the base game's `RequestMissions` / `DataRequest` mechanism must be identified before any bounded read-only background refresh is safe |

## A newly verified source: official Community server mission definitions

The official [Community Edition Reborn server's `content/missions` directory](https://github.com/OrbusVR-Community/reborn-server/tree/main/content/missions) contains **15 JSON files and 298 authored mission records**: 154 records have explicit IDs (153 distinct IDs), with **one duplicate ID, `ltmkill3`**. The 144 others lack stable authored IDs and must be handled using live mission data / generated server IDs. The legacy QuestHelper catalogue contains 152 non-ambiguous fixed IDs, so it was already close to the full known set.

**Important correction:** Tier requirements are **increments**, not three separate absolute targets. In the source, `capture16` has Bronze **10**, Silver **+8**, Gold **+12**, so target totals are **10/18/30**. Meanwhile, `checklist[0].timesToKill` may be **10,000**, which is not an actual next reward target. Version 0.4.13's `MissionGoalPolicy` now accumulates the native tier requirements. It never treats the ordinary checklist's 10,000 cap as a valid next milestone, and it doesn't start showing `/100` after a small hunt's Gold is already complete.

**Exact labels, without shipping game content:** `MissionDefinitionIndex` can optionally read the JSON from **your own local official server installation** (no network access, no files copied into the public repository). It looks up fixed mission IDs and a limited, verified target vocabulary. It also reads the sibling `content/items/item-serverdata.txt` name table so numeric potion/material IDs can become readable objectives. Examples of locally resolvable labels are `Capture lizards`, `Capture squirrels`, `Gather Blade Cactus`, and `Drink Healing Potions`. Unknown/ambiguous targets remain generic.

To enable this, **with the game closed**, edit `BepInEx/config/com.horizon.orbus.questhelper.cfg` after running the mod once:

```ini
[Missions]
LocalServerMissionsFolder = C:\\Path\\To\\reborn-server\\content\\missions
```

Use the **directory containing the 15 JSON files**, not their parent. Restart OrbusVR. A bounded log reports only the number of definitions loaded (normally **152 unambiguous fixed IDs** with matching source), never the path, mission descriptions or player information. The imported definitions affect **labels only**; live progress and tier values still come from the game's native `Mission` object. This optional configuration is not currently editable through RebornMenu's numeric/bool/enum-only mod settings panel.

The official content is distributed under the [OrbusVR Community Edition license](https://github.com/OrbusVR-Community/reborn-server/blob/main/LICENSE), which restricts extracting/repackaging assets for use outside the Software. QuestHelper does **not** package these definition files or a wholesale extracted content table; each user reads their own local installation.

## Base-game refresh research

The Community `PlayerMenu.UpdateMissions()` `0x5DC7F0` constructs an `ExtUI.RequestMissions` and invokes a `PlayerCharacter.DataRequest` pathway. The inspected native control flow first checks the local `missionsNeedsUpdate` state and returns without sending when that flag is false. Blindly calling `UpdateMissions()` repeatedly is therefore **not** a solution. This method also changes `lorequest-content`, not the main mission-grid `missions-container`.

For Spades, first map the equivalent valid request/response path by behaviour; do **not** guess an obfuscated method name or modify the native Missions page just to trigger a network refresh. Community server content files tell us what missions **are**, but not when Spades requests new **player progress**.

## Scope and safety rules

- No fake target numbers or quest counts. `MissionGoalPolicy` refuses to treat 10,000 as a verified *next small milestone*.
- No automatic mission purchase/turn-in, reward claim, obfuscated RPC guesses or unsolicited server requests.
- QuestHelper does not modify the game's XP bar, original mission HTML, mission objects, progression or native quest handlers.
- Saved pins, favourites, grouped Mission UI, and player configuration remain intact.
- `Headset` remains an **explicit** alternative. If your existing config stores `TrackerAnchor=Headset`, select `Auto` or `LevelHud` in Settings → Mods → QuestHelper to put it beside the top XP bar.

## Offline regression cases

`tools/MissionObjectiveRules` compiles only the pure catalogue, label formatter and milestone policy. It tests:

1. Real known Lizards/Ruined Potions aliases.
2. Native `Capture 5 Desert Lizards (3/5)` becoming `Capture Desert Lizards`.
3. Story paragraphs falling back to an activity/action, not clipped narrative text.
4. `98/10000` choosing verified Bronze **100**, or showing **98 captured** when not confirmed.
5. Bronze→Silver→Gold goal selection.
6. Normal `3/8` counters remaining unchanged.

Run `dotnet run --project tools/MissionObjectiveRules/MissionObjectiveRules.csproj -c Release` with .NET 8. The GitHub mission HTML safety workflow also runs it.

## Live Spades test, after an explicit preview build

1. Save your previous **0.4.12** plugin DLL and settings for rollback.
2. Build **0.4.13** with `release.ps1 -GameDir <your Spades client directory>` only after reviewing/merging this branch; the branch by itself is not installed.
3. In the Mods tab, set `Tracker Placement = Auto`; pin a capture and a hunting/brewing quest.
4. Join the world, close Missions and look at the **native level/XP bar**, not a floating camera-relative label.
5. Test normal progress, then Bronze → Silver. The tracker must stay small and in the same location and never display an unverified `/10000` as the next target.
6. Test a quest with an empty checklist description: the HUD must show an action category instead of a long story.
7. Progress a mission **without opening Missions**. If the counter does not change, note the count, open Missions, then note whether it jumps. That is proof of stale local mission data requiring a genuine Spades refresh call.
8. Leave to lobby then return, and verify the tracker disappears/reappears without staying attached to the headset.

### Needed for full Spades server-refresh support

A version-matched Spades IL2CPP method mapping or an authorized read-only diagnostic identifying the method that sends the ordinary `ExtUI.RequestMissions` request when the game opens Missions. Then implement **rate-limited** refresh with lifecycle checks, error backoff and no calls during loading or the lobby. Community's known method isn't enough to infer the Spades obfuscated name.

**Do not advertise this preview as a complete fix for network-side stale mission data.**

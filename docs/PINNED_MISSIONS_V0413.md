# QuestHelper v0.4.13 candidate — pinned objectives and level-bar placement

**Status:** source change on a review branch; **not a released DLL**. Requires CI / compilation with a real game interop set and live VR test on Spades. No server request or progress/reward modification has been added.

## Four reported symptoms and the actual cause

| Symptom | Finding | Candidate change | Remaining validation |
|---|---|---|---|
| Auto tracker reattached to VR headset | v0.4.12's `Auto` condition explicitly prefers `eye.stereoEnabled` | `Auto` now follows the native level/XP **world pose**, but the QuestHelper `TextMesh` remains detached so native scale changes cannot enlarge it; only explicit `Headset` anchors to the camera | Verify at headset scale, during Silver/Gold tier changes and after lobby transitions |
| `98/10000` instead of useful progress | A **real** Spades capture mission sample exposed `ChecklistItem.timesToKill=10000`, `killCount=21` and empty `ChecklistItem.description` | Prefer the next **verified native** Bronze/Silver/Gold requirement when `Mission.progress` agrees with the checklist count. If no usable small milestone exists, show the **true earned count** with an action unit (e.g. `98 captured`), not `98/10000` | Confirm actual native reward requirements for your pinned missions and verify stage transitions |
| Long mission story displayed on HUD | `Mission.description` is narrative fallback when `ChecklistItem.description` is empty | Prefer actionable checklist text, then two previously verified exact aliases, then an actual imperative in the mission description, then the known mission **activity category** (e.g. `Capture critters`, `Brew potions`, `Defeat monsters`) | Exact target species/item names are **not known for every quest**; the catalogue only has ID→activity/region |
| Progress requires reopening Missions | The v0.4.12 `MissionHudDriver` already polls `GetMission` / Spades `ceh.ljn` every 0.85 s independently of the UI | Polling reduced to **0.5 s** to pick up **locally refreshed** native counters sooner | If the Spades mission record remains stale until a server request is made, this is **not fixed**. The obfuscated equivalent of Community `PlayerMenu.UpdateMissions` / `RequestMissions` must be identified before any bounded read-only background refresh is safe |

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

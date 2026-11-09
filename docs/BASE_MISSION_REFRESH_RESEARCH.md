# Base-game mission refresh and objective data: native evidence

**Research:** November 9, 2026. **Evidence:** user-held Community/base OrbusVR Ghidra `GameAssembly.dll` export and matching readable IL2CPP `dump.cs`. The RVA addresses below belong to that native snapshot (base `0x180000000`), **not** to a verified Spades binary. No live refresh request was sent or verified during this research. Raw proprietary decompilation remains private.

## 1. The original game's mission-request path is visible

| Named method | RVA | Direct native evidence |
|---|---|---|
| `PlayerMenu.UpdateMissions()` | `0x5DC7F0` | Finds/clears **lorequest-content** element, constructs `ExtUI.RequestMissions`, serializes it to text (JSON serialization according to earlier independent analysis) and calls `PlayerCharacter.DataRequest(string, Action<string>)`. This function **does not itself render the full inventory Missions grid**. |
| `ExtUI.RequestMissions..ctor()` | `0x68FFD0` | Writes byte `10` to `RequestMissions.requestType` (object offset `+0x10`). This is a **request-payload discriminator**, not a network opcode. |
| `PlayerCharacter.DataRequest(string, Action<string>)` | `0x377C90` | Constructs a network request with payload and response correlation ID, and calls `NetEntity.CallServerComponent` **`0x2F2910`** with **PlayerCharacter component method ID 10**. |
| `PlayerCharacter.DataRequest(string)` | `0x377DC0` | Overload constructs a response-holder object and uses the same component method ID 10 network-send mechanism. |
| `PlayerCharacter.HandleDataRequestResponse(float,string)` | `0x37C600` | Looks up request ID in response/callback tables at native player offsets `+0x2A8` / `+0x2B0`, resolves the corresponding callback or result container, then removes completed lookup entries. |
| `PlayerMenu.doUpdateInventory()` | `0x5DFEB0` | Builds the main inventory Missions icon grid from local mission data and writes into `missions-container`; **different operation from `UpdateMissions()`**. |

The two **10** values are distinct: the serialized `RequestMissions.requestType=10` tells the data-request handler what content is requested; the `PlayerCharacter` network component method ID **10** transmits a generalized data request. Other requests also use component method 10 with different payloads.

### A genuine limit to the interpretation

The observed `UpdateMissions` path affects **lorequest-content**, so we cannot yet prove that repeating it periodically fetches **every** normal mission checklist counter. That must be checked by comparing returned data and the native `Mission` object before/after the ordinary Missions tab loads. Do not launch repeated requests from QuestHelper based only on the function name.

### Recommended Spades bridge (no blind obfuscation guesses)

1. Reproduce the base-game request and response layout in a controlled Community client. Log only method/request-type/timestamp and whether local `Mission` data actually changes; avoid tokens and player data.
2. Find the Spades `ExtUI.RequestMissions` payload model (if retained) **by its concrete fields/constructor signature**, and the data-request helper **by its method signature and call sites**, rather than trying every obfuscated `ng` method.
3. Before enabling auto-refresh, confirm a request reproduces the game's own result and does not require a lorequest page to be open.
4. Only then add rate limiting, lifecycle gating, error backoff and a disabled-by-default diagnostic flag for preview testing.

**Status:** request construction and general response handling statically mapped; **background normal-mission refresh not demonstrated**.

## 2. Mission ID is not mission definition

Verified managed data structures in the Community/base dump:

```text
Mission
  id: string
  type: string
  description: string
  status: string
  progress: float
  bronze, silver, gold: RewardTier
  checklist: ChecklistItem[]

ChecklistItem
  id: int
  description: string
  completed: bool
  timesToKill: int
  killCount: float

RewardTier
  requirement: float
  nameOfRewardTier: string
  progressValue: float
```

- An ID such as `capture10`, `hunter1` or `jester16` identifies a **mission record**, *not* a requested count. Any digits in the identifier are not a reliable kill/capture/drink target.
- `ChecklistItem.timesToKill` is the **native checklist target**, and `ChecklistItem.killCount` is current checklist progress. The player logs demonstrated a valid seasonal capture mission with `timesToKill=10000` and an empty checklist description.
- `RewardTier.requirement` determines Bronze/Silver/Gold thresholds; do **not** substitute the maximum lifetime target for the next earned reward threshold.
- `Mission.description` can be narrative, not a command. Empty `ChecklistItem.description` means the exact target is not available **from that field**, not that it is safe to guess.
- The base game also has `PlayerCharacter.missionMonsters: MissionMonster[]` at offset `0x6D0`. Each `MissionMonster` contains `monsterType` and `zone`; `OrbusHealthBar.CheckKillMissionMonster` **`0x303660`** iterates this array, comparing its values to target-related fields. This provides **another real monster-label lead**, but there is **no established mapping from a particular pinned mission ID to a particular entry**. Do not falsely label a quest from that array alone.

## 3. The curated catalogue today

`MissionCatalog.cs` contains **152 distinct** identifiers with region/activity classifications derived from the user's earlier data work. This is a **classification map**, not an authoritative complete quest definition database.

See `references/mission-objective-atlas.json`: it exposes the known IDs in a machine-readable form with `target_name: null` and `required_count: null` until supported by independently verified quest data. A separate curated objective-name mapping can be added when precise target names are confirmed for a stable mission identifier.

The captured Spades Missions UI once displayed **62 original icons** (54 level missions and 8 weekly) under that player's current availability. This is not the total number of all possible missions. Some weekly IDs are generated and differ between weeks. Do not equate the 152 classifications with every possible seasonal/weekly mission.

## 4. What to collect from actual missions

For each available mission on a consenting test account, record only:

- mission ID; activity/type; whether a weekly-generated ID;
- verified **action** and exact target name, with the native field or trusted data row supporting it;
- **checklist item index / integer ID**, `timesToKill`, `killCount` and completion state;
- Bronze/Silver/Gold `requirement` values and which tier was current;
- client build and whether a menu-open or a real state event changed the data.

An independent quest objective atlas should **distinguish static definition facts from changing live progress**. Never cache one user's `killCount` as the fixed target for all players; never overwrite server-owned objectives.

## 5. Release decision

The related [v0.4.13 candidate PR](https://github.com/Hor1zonVR/OrbusQuestHelper/pull/1) fixes default positioning and clearer/fairer cached-data presentation. It **does not claim** that base `UpdateMissions` has already been shown to refresh all Spades missions. See [background-refresh investigation issue](https://github.com/Hor1zonVR/OrbusQuestHelper/issues/2) for the remaining verification steps.

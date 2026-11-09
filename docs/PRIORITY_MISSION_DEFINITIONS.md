# QuestHelper — Monster, Fishing, Jester and Event source-backed objectives

**Source revision:** [OrbusVR Community Reborn Server, 330d1e8](https://github.com/OrbusVR-Community/reborn-server/tree/330d1e8934ea5c09e6732c592ce15c4fc09b3b3e/content/missions), 8 definition files under `content/missions`. This research was added on **2026-11-09**. These definitions belong to the public Community/Reborn server snapshot, not to a confirmed current Spades build.

## Coverage

| Group | Repeatable missions without stable IDs | Level missions with IDs | Total |
|---|---:|---:|---:|
| MonsterHunter | 28 | 30 (`hunter1`–`hunter30`) | **58** |
| Fisher | 27 | 14 (non-contiguous `fishing*` IDs) | **41** |
| Jester | 14 | 17 (non-contiguous `jester*` IDs) | **31** |
| EventHelper | 20 | 20 (non-contiguous `event*` IDs) | **40** |
| **Total** | **89** | **81** | **170** |

Our earlier **16 capture missions** are in [their separate guide](CAPTURE_SERVER_DEFINITIONS.md), so together the current reviewed datasets cover **186 mission definitions**, although descriptions and gameplay behaviour still require cross-build validation.

## How to determine exactly what a mission requires

- **Monster (`kills`)**: decode `checklist.ids[]` and, for `category=killlist`, respect each separate native `ChecklistItem.description`, `killCount` and `timesToKill`. For `hunter10`, the source has separate checklist targets for **Undead (5), Dungeon Troll (2), Lich King (2)**. The current pinned HUD now favours specific native labels instead of the general story title.
- **Fishing (`fishcaught` vs `fishcaughtweight`)**: one counts fish, the other counts **total catch weight**. The source does **not** establish the displayed measurement unit, so the HUD says weight rather than inventing kg/lb. Examples: `fishing9` is **Sunfish** (9 Bronze, +5 Silver, +7 Gold); `fishing6` is **fish by weight near Kingsport** (15, +10, +20).
- **Jester**: the server uses `harvest`, `brewpotion`, `consume`, `equipmentsalvage` and `earndram`. `jester27` requires **brewing Runesight Potion** (1, +1, +1). `jester29` requires **drinking Ruined Potions in Highsteppe** (3, +5, +5). `jester13` has two separate harvestable checklist entries (**Reedflute** and **Blade Cactus**).
- **Event (`eventparticipation` vs `eventgold`)**: an `eventgold` mission requires completing a public event at **Gold standard**. This is **not** earning the Dram currency. `event18` requires Gold-standard completion of the Lamavora ruins event (2, +1, +2), and `event29` requires Gold-standard public events anywhere (2, +1, +3).

**Source values:** `bronze.requirement`, `silver.requirement`, `gold.requirement` represent **per-tier increments** in these definitions. For a compatible *cumulative* progress counter, the targets are Bronze, Bronze+Silver, and Bronze+Silver+Gold. The plugin checks that `Mission.progress` and a single native `ChecklistItem.killCount` are comparable before displaying a cumulative goal.

**Do not mistake `timesToKill=100`, `1000` or `10000` for the next reward requirement.** Those are checklist upper bounds for many single-objective missions. An exception is a **multi-entry killlist**, where each entry has its own small native quota; QuestHelper uses those per-entry targets instead of a fabricated shared milestone.

## Packaging and implementation

The code includes a compact [170-entry source-derived index](../references/priority-missions-objectives.json) and the `OfficialPriorityMissions` classifier in `src/OrbusQuestHelper/Missions`. It stores **original concise action labels**, the actual level mission IDs, type, increment values, and **UTF-16 FNV-1a fingerprints of descriptions** (rather than copying the full source's narrative text into the DLL). A known mission is matched first by exact level ID and type, otherwise by an unambiguous type/description fingerprint. UI labels prefer game-supplied checklist names where available.

**Known collision:** Two `JesterMissions.json` Dram missions have precisely the same description but **different** tiers (1,000+1,000+1,000 versus 3,000+1,500+2,000). Without a stable ID, the built-in index **declines to choose one**; the HUD must read the live server's requirement fields. A fallback `/10000` would be wrong.

**Priority of values:** native `Mission.bronze/silver/gold` → optional local server definition file → bundled snapshot metadata. The plugin **never writes** progress or claims rewards. For mission names, explicit native checklist text wins; matching exact reference descriptions take precedence over generic local importer labels, while a modified local source can override a weaker ID-only fallback.

## Offline tests and limitations

The separate `tools/MissionObjectiveRules` test builds on .NET 8, without game DLLs. It iterates the [170-entry atlas](../references/priority-missions-objectives.json) to verify action names and all tier increments, guards the two ambiguous Dram entries, and checks killlists, fish-weight labels, potions, Gold-standard events and legitimate high-value Dram goals.

**Still not demonstrated in game:**

- Whether the current Spades game build uses these exact definitions without modifications.
- Whether the game's `Mission.progress` resets between reward claims or remains cumulative in every activity.
- The precise real-time server-refresh request needed to update cached mission counters without opening the Missions tab; see [background-refresh investigation](https://github.com/Hor1zonVR/OrbusQuestHelper/issues/2).
- The fractional display unit of weight-based fishing missions.

**For a live release:** compile against the chosen game's interop DLLs and test representative quests from all four groups across Bronze/Silver/Gold transitions, in VR. The research branch does not itself install or release a DLL.

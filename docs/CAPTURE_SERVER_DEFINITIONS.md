# Capture objectives: 16 missions from real Reborn-server definitions

**Source:** sixteen capture-mission definition objects provided by the user on **2026-11-09**, including `description`, `type=capture`, optional `ids`/weights, allowed `zones`, and Bronze/Silver/Gold `requirement` fields. The objects have **no mission `id` field**. This guide is an original interpretation and concise paraphrase, **not** a redistribution of raw game source.

## Actual stage requirements

Values below are *incremental* per reward tier. Cumulative totals assume the game's `Mission.progress`/`ChecklistItem.killCount` accumulates over tiers; the existing progress-scale check prevents a display goal if native counters disagree.

| # | Capture task | Zone restriction | Bronze | Silver adds | Gold adds | Cumulative reward milestones |
|---:|---|---|---:|---:|---:|---|
| 1 | Mice | Highsteppe underground Market | 6 | 3 | 5 | 6, 9, 14 |
| 2 | Parathime Finches | Traders Road/Zone3 | 6 | 3 | 5 | 6, 9, 14 |
| 3 | Any critters near Highsteppe | Zone3 | 6 | 3 | 5 | 6, 9, 14 |
| 4 | Squirrels | Zone3 | 6 | 3 | 5 | 6, 9, 14 |
| 5 | Any critters in Flooded Rainforest | Zone4 | 8 | 5 | 7 | 8, 13, 20 |
| 6 | Obnobi Lizards | Zone4 | 10 | 8 | 12 | 10, 18, 30 |
| 7 | Any critters in Lamavora Battlefield | Zone1 | 7 | 6 | 8 | 7, 13, 21 |
| 8 | Skunks | Zone1 | 8 | 6 | 6 | 8, 14, 20 |
| 9 | Green Crested Fowl | Zone1 | 8 | 6 | 6 | 8, 14, 20 |
| 10 | Hulthine's Basin birds | Zone2 | 6 | 5 | 6 | 6, 11, 17 |
| 11 | Any critters anywhere | No zone filter | 10 | 7 | 8 | 10, 17, 25 |
| 12 | Lizards and toads | No zone filter | 10 | 8 | 8 | 10, 18, 26 |
| 13 | Any critters in Wastelands | Zone5 | 8 | 5 | 8 | 8, 13, 21 |
| 14 | Hairless (mutated) skunks | Zone5 | 8 | 5 | 8 | 8, 13, 21 |
| 15 | Mutated toads | Zone5 | 8 | 5 | 8 | 8, 13, 21 |
| 16 | Any critters anywhere (advanced) | No zone filter | 15 | 10 | 10 | 15, 25, 35 |

**No mission-ID assumption:** there is no proof which `captureN` identifier maps to which of these records. Never use array order to assign IDs.

## What actually counts?

The source's `checklist.ids` contains specific target variants. Examples:

- Ordinary **mouse** `mouse` contributes **1**; `mouse-rare` contributes **3**.
- Ordinary Parathime Finch `bird` contributes **1**; `bird-rare` contributes **3**.
- Lamavora Green Crested Fowl `bird2` contributes **1**; `bird2-rare` contributes **3**.
- Hulthine's bird `bird3` contributes **1**; `bird3-rare` contributes **3**.
- Wastelands hairless skunk `corrupt1` contributes **1**; `corrupt1-rare` contributes **3**.
- Wastelands mutated toad `corrupt2` contributes **1**; `corrupt2-rare` contributes **3**.
- The mixed reptile mission uses `lizard` value **1** and `corrupt2` value **3** in the supplied server data. Don't silently relabel `corrupt2` as a generic, ordinary toad variant.
- `ids: []` means the mission does not restrict to specific critter-template IDs; `zones` may still restrict the capture.

**Consequently, a `6` requirement means six *weighted progress points*, not necessarily six individual captures.** Two rare mice with `value=3` meet six points.

## How QuestHelper uses these records

1. `OfficialCaptureObjectives` recognizes exactly these **16 distinctive description prefixes** only when `Mission.type == "capture"`. It returns a concise action label, the region and the three incremental tier amounts. It does not invent a mission ID.
2. `MissionObjectiveLabels` prefers this verified description match over falling back to a long narrative or a generic “Capture critters” label.
3. `MissionProgressReader` reads **live native** `Mission` reward requirements first. Only when absent does it consult the optional local-server JSON importer, then this compact source mapping.
4. `MissionGoalPolicy` computes the **cumulative next milestone** (Bronze, Bronze+Silver, Bronze+Silver+Gold), provided mission and checklist progress are demonstrably comparable.
5. `timesToKill=10000` (or `100`) is a broad checklist upper bound in these definitions. QuestHelper should not treat it as the next reward threshold. Server progress is never edited.

### Adding other missions

Set `LocalServerMissionsFolder` to the existing `content/missions` directory of **your own server**. The loader now accepts mission JSON records **with or without `id`** and can map exact description text to action and tier requirements. It attempts to resolve numeric potion/herb IDs through the local `content/items/item-serverdata.txt` file.

The full local JSON data stays on the user's computer; QuestHelper only reads it. Different/modified server data must not be silently taken as applicable to unrelated clients.

## Test coverage and open questions

The offline `MissionObjectiveRules` checks all 16 descriptions/labels and Bronze/Silver/Gold increments, confirms rare-mouse weights and validates a synthetic **ID-less** server file. The tests do not claim that a current Spades build uses identical server content.

Still unverified:

- Exact `captureN` ID → definition correspondence.
- Whether certain mission types reset `Mission.progress` on tier collection.
- Live Spades server-pushed quest freshness without opening Missions.
- Whether non-capture mission types have the same stage/increment semantics.

See [Base-game mission refresh research](BASE_MISSION_REFRESH_RESEARCH.md) and [QuestHelper v0.4.13 draft PR](https://github.com/Hor1zonVR/OrbusQuestHelper/pull/1).

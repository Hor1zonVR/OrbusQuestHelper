# QuestHelper v0.4.8 — Favourites, pinning and top-HUD quest progress

## What is implemented in this experimental update

- Original Mission icons, onmousedown/hover handlers, native rewards,
  buying/turning-in and all original quest data are unchanged.
- The old custom-coloured **By Region / By Activity** selectors are
  replaced with native Orbus `achievement-tab-control` buttons.
- Each original mission tile is placed in a narrow presentation wrapper
  and receives two **separate** `compact option` controls below it:
  `Star` / `Star*` and `Pin` / `Pin*`. Only our controls receive
  new native event handlers.
- Starred quests appear once at the beginning under **Favourites**,
  not duplicated under the usual region/activity category.
- Up to **three** different mission IDs can be pinned. Selections are
  saved by BepInEx under `[Pinned Missions]` and persist across relaunches.
  This first version is **local-installation scoped**, not character-specific.
- The optional `[HUD] ShowPinnedMissions=true` creates a tiny separate
  quest tracker adjacent to Orbus's level label. It does not mutate the
  existing label, XP bar or any game HUD elements.
- Counters refresh every ~0.85 seconds using actual
  `MagicalActual.ChecklistItem.killCount` and `timesToKill`.
  The label is taken from the **actual objective description**. For example,
  an observed `Capture 35 Mice` at native 12/35 becomes `Mice 12/35`.
  No fixed creature names or fabricated targets.
- If a mission has no numeric checklist entry or native access fails,
  the tracker says that progress is unavailable, not `0/35`.
- The top-HUD position and font rendering are **experimental** and
  require a screenshot/headset verification on the user's actual client.

## Verified IL2CPP class mappings from both local dumps

| Readable Community Edition | Renewed/Spades | Purpose |
|---|---|---|
| `MagicalActual.PlayerCharacter` | `ceh` | Local player |
| `PlayerCharacter.Instance` | `ceh.blfr` | Active player instance |
| `PlayerCharacter.GetMission(string)` | `ceh.ljn(string)` | Read mission |
| `PlayerCharacter.myMissions` | `ceh.bfad` | Read-only mission collection fallback |
| `PlayerCharacter.myPlayerAvatar` | `ceh.betm` | Avatar with HUD |
| `PlayerAvatar.topHud` | `cbn.bebt` | Existing top HUD |
| `TopHud.levelLabel` | `clf.bhch` | Exact level text anchor |
| `MagicalActual.Mission.checklist` | Same named field | Current objectives |
| `ChecklistItem.description` | Same named field | Objective title |
| `ChecklistItem.killCount` | Same named field | Current progress |
| `ChecklistItem.timesToKill` | Same named field | Required total |

This is grounded in the user's Community `dump(1).cs` and Renewed
`dump.cs`; it does not prove the live VR text scale/placement yet.

## First in-game test (no manual DLL installs)

1. From any PowerShell folder, run:

   `& "$env:USERPROFILE\OneDrive\Documents\OrbusQuestHelper\release.ps1" -GameDir "$env:LOCALAPPDATA\OrbusRebornModdingInstances\Modded Orbus"`

2. Refresh **RebornManager > Client Mods** and update QuestHelper.
3. Launch Orbus, open **Inventory > Missions**.
4. Check the new native Region / Activity tabs.
5. Star one critter quest. The icon should move to the Favourites
   section, without disappearing or becoming duplicated.
6. Pin three different critter quests. The native controls should show
   `Pin*`, and the top HUD by your level should show their actual
   objectives and live progress if the game exposes those values.
7. Capture one critter. Confirm the current count changes from the
   game's real checklist value. Try a fourth pin; it should not replace
   or add a fourth until one existing pin is removed.
8. Close/reopen Missions and relaunch the game; selections should persist.
9. Check original mission hover, quest-details, purchase/turn-in and
   reward interactions still work unchanged.

**If any original mission icon is missing or its native interaction
changes, stop using this experimental build and reinstall v0.4.7.**

If progress appears but is too small/far from the level label, send a
headset screenshot and the `[QuestHelper/HUD]` log lines. The offset
and native font choice can be adjusted without changing mission reads.
If the new page fails to appear at all, send `[QuestHelper]` and
`[QuestHelper/Pins]` logs; the source's fail-closed transformer
will keep original Missions content when its validation fails.

## Deliberate limitations

- Current implementation uses `TextMesh` with the level label's
  available Unity font for the new child HUD. This is **anchored to**
  Orbus's existing level label, not a replacement for it. It is not a
  PowerUI overlay.
- A checklist objective can contain an activity other than critter
  capture. Only objectives with real numeric count fields are displayed.
- Generated weekly mission IDs are not stable across renewals; pinning
  those needs a separate identity scheme in a later version.
- BepInEx PIN data is instance-local. Per-character pins can be added
  after verifying how the game exposes the current character ID safely.
- The native Star/Pin tiles may need width/spacing polish once seen in VR.
- Source was updated on GitHub, but **cannot be compiled using the
  user's locally generated Unity IL2CPP assemblies in this environment**.

## Scope and safety

The HUD never invokes purchase, abandon, reward, mission-commit or network
methods. It reads only the active user's own current mission data.
Native quest tiles are copied byte-for-byte and the transformer verifies
the full original mission tile multiset after each change.

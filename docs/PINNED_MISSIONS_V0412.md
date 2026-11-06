# QuestHelper v0.4.12 — pinned tracker scaling / independent progress polling

## Why the tracker became huge on a reward-tier change

The earlier TopHud/level-label mode parented the custom TextMesh to the
native level UI transform and divided its local scale by the parent's
lossyScale to compensate for the tiny original SuperTextMesh hierarchy.
When Orbus rebuilt or re-scaled its XP/level display around a reward
transition, that inverse scaling could make QuestHelper's tracker
appear much larger or far from its intended HUD location.

v0.4.12 no longer parents the tracker to the game's XP/level transform:
- `Auto` prefers an active stereo VR headset camera while the game's
  original TopHud AND level label are active. The tracker is a compact
  headset child positioned above and toward the left, with fixed local
  glyph scale.
- Explicit `LevelHud` uses an independent, unparented GameObject; it
  reads the original label's world position and rotation every frame.
  It never uses reciprocal `lossyScale` or writes any native HUD element.
- The old gameplay-HUD lifecycle checks remain: no tracker in lobby
  or loading, and no camera fallback without a real active TopHud.
- Existing HUD config values and mission pin IDs are unchanged.

## Progress without repeatedly opening Inventory > Missions

Previously, `MissionHudPresenter.Tick()` was called only from the
`MissionViewSwitcher` component, and that component was registered only
when the optional HTML Missions organiser was enabled and had installed
successfully. v0.4.12 gives the tracker its own `MissionHudDriver`,
registered on every normal plugin launch independent of the organiser.

The driver's read-only tick observes native
`PlayerCharacter.GetMission(id)` / `ceh.ljn(id)` checklist counters
every 0.85 seconds while the player is in-game. Pinned IDs still
persist via the existing BepInEx config. No Missions-tab open is needed
for the HUD component to start or to reread *already refreshed* native
mission records. The source now logs when a pinned reward target
transitions (e.g. Bronze to Silver) without logging mission IDs.

**Important client-data limit:** This does not fabricate or force fresh
server mission data. If the Spades client only fetches updated mission
records through a request when the Missions tab is opened, the local
`GetMission` object can still be stale. We have confirmed Community's
`PlayerMenu.UpdateMissions()` issues an `ExtUI.RequestMissions` request
but have NOT verified its obfuscated Renewed/Spades equivalent. This
release does NOT guess that method or spam requests. A separate
version-matched server-refresh integration may be necessary after
examining the post-tier Spades logs or IL2CPP mapping.

## VR test

1. Install/update QuestHelper v0.4.12 using `release.ps1` + RebornManager.
2. In RebornMenu, set Tracker Placement to Auto if it isn't already.
3. Pin the Lizards quest (existing pins should survive the update).
4. Start Orbus and watch the HUD *without opening Missions*. If the
   local mission record is already populated, a compact pinned line
   should show. If unavailable, opening Missions once is okay.
5. After the initial mission fetch, close Missions, capture lizards,
   and watch count changes without reopening the tab.
6. Advance Bronze → Silver, then keep capturing. The tracker must
   remain compact; neither your Level 30 label nor quest text should
   be enlarged or shifted by QuestHelper.
7. Try an explicit LevelHud placement if needed: the tracker should
   remain unparented and world-scale stable during level bar rebuilds.
8. If progress only updates after opening Missions, send the fresh
   LogOutput.log lines starting `[QuestHelper/HUD]` and record the
   counts immediately before and after opening Missions. That would
   identify a stale local mission-data source, separate from HUD drawing.

All mission HTML transformations, original click/purchase/reward handlers,
pins, favourites and NativeMissionEditingSuspended safety interlock are
unchanged. The actual gameplay result still requires a live headset test.

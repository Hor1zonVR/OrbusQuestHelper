# OrbusQuestHelper: Missions implementation research (2026-10-19)

This document summarises an offline inspection of the user's *privately uploaded* generated BepInEx IL2CPP `Assembly-CSharp.dll` and `global-metadata.dat`. **Do not commit/distribute the game DLLs, raw metadata, ZIP, or extracted proprietary UI markup.** These are original observations and names, not redistributed game source.

## Confirmed: PlayerMenu and PowerUI

The real Missions interface is handled by the game's global `PlayerMenu` class (238 methods, 60 fields). Important methods: `updateMissionTab()`, `UpdateMissions()`, `GetMissionIcon(mission)`, `missionHover(e)`, `missionStopHover(e)`, `missionClick(e)`, `ClearMissionsToolTips()`. Relevant fields: `document`, `missionsNeedsUpdate`, `missionClicked`, `tooltipMissionId`, `currentMissionId`.

Metadata contains actual HTML-style string literals for the Missions screen, including `missions-tab`, `missions-container`, and section identifiers:
- `PlayerMenu-AvailableWeeklyMissions-header`
- `PlayerMenu-AvailableLevelMissions-header`
- `PlayerMenu-YourWeeklyMissions-header`
- `PlayerMenu-YourLevelMissions-header`
- `PlayerMenu-YourMissions-header`
- `PlayerMenu-NewMissionsAvailable`

Mission icons use HTML-style `onmousedown` and `onmouseover` handlers bound to `PlayerMenu` functions, plus `data-missionid-` markers. Their exact DOM structure must be confirmed at runtime.

The prior Unity runtime scanner found `PlayerRigv4/Player Menu/TVScreenContent` and its growing number of unnamed `MeshRenderer` children with `PowerUI/StandardUI` materials. These are **rendered PowerUI geometry**, not confirmed mission instances. Further world-raycast or generic canvas scanning is unnecessary.

## Confirmed: Game mission data types

`MagicalActual.Mission` fields: `id`, `type`, `description`, `status`, `currentReward`, `levelRequirement`, `progress`, `pvpBonus`, `classLock`, `none`, `bronze`, `silver`, `gold`, `checklist`, `overrideIcon`. Methods include `get_CurrentReward()`, `get_NextReward()`.

`MagicalActual.Missions` fields: `missions`, `availableCredits`. `MagicalActual.PlayerCharacter` fields include `myMissions`; methods include two `GetMission` overloads (`index` and `id`) as well as state-changing operations like `TurnInMission` and `BuyVendorMission` that QuestHelper **must not call automatically**.

User estimates ~46 regular and ~6 weekly missions, shown together on one scrollable page with weekly at the top. Treat these as a comparison target, not data verified by the scanner. `Mission` has no obvious explicit `weekly` or `region` field, so don't invent these classifications.

## PowerUI DOM methods are available

`PowerUI.Document`: `getElementById(id)`, `getElementsByClassName(className)`, `getElementsByAttribute(property,value)`, `createElement(tag)`, `get_innerHTML()`.

`PowerUI.Element`: `get_id()`, `get_className()`, `get_innerHTML()`, `get_childNodes()`, `get_parentNode()`, `appendChild(element)`, `removeChild(element)`, `scrollTo(x,y)`, `get_scrollTop()`, mouse-over/out, click events.

`PowerUI.UI` has a `document` field and `LoadHTML(resource)`, but **do not assume** its static document belongs to the VR player menu. Verify the live document ownership first.

## Next implementation plan

1. Build a **minimal, read-only, menu-specific DOM probe**, replacing broad diagnostic methods. Identify the live PlayerMenu document. When the Missions tab is open, check the known DOM IDs, count actual visible mission tiles by category and inspect safe, non-chat attributes. No calls that change missions.
2. Correlate UI mission IDs to `PlayerCharacter.myMissions` / `Mission` data via generated interop field and method types. Determine weekly vs regular, completion/reward states. No guesswork.
3. Implement an optional **client-only UI organiser** using the existing PowerUI DOM. Preserve native hover/click/turn-in handlers, scroll behaviour and mission IDs; provide a toggle and safe fallback. Add region grouping once a grounded mission-to-region mapping exists.

The archive's generated DLLs are metadata wrappers rather than decompilable original C# method implementations. Unity asset files were indexed but not included; zero standalone loose HTML/CSS/JS files were found.

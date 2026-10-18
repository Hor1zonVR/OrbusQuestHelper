# OrbusQuestHelper

A client-side quality-of-life mod for **OrbusVR Reborn Community Edition** using **BepInEx IL2CPP** on the Windows PC client.

**Current version: 0.1.5 — interaction diagnostics only.** Missions sorting, journal navigation, event notifications and fishing guidance are planned, not implemented.

## Planned features

- Expandable mission regions and categories, prioritising the current region.
- Regular and weekly missions together, keeping weekly missions easy to access.
- Accurate completion, ready-to-turn-in and Bronze/Silver/Gold reward states.
- Optional story-journal Track buttons, compass and world beacon.
- Relevant live event reminders, fishing guidance and location information.

## Current diagnostic scanner

The initial broad scene inspectors produced lots of unrelated scenery results. The new default is **UI interaction tracing**: locate both hand UI pointers, examine physics raycast candidates only when they change, monitor Unity EventSystem's selected UI object and capture active Canvas/Selectable hierarchies on Ctrl+Alt+K. It also notes sibling and component types around a pointer target so we can identify the actual scrollable Missions grid.

**This is read-only**: it does not consume controller input, invoke quests, change UI or modify progress. Neither physics rays nor EventSystem selection guarantee every custom VR hover event will be captured; see docs/DIAGNOSTIC_RUN.md.

One file per session: BepInEx/config/OrbusQuestHelper/Diagnostics/*_UiInteractions.txt.

## Build locally and publish (same workflow as RebornCam)

Install Git for Windows, the .NET 8 SDK and GitHub CLI. Authenticate once using gh auth login.

From your clone:

    git pull origin main
    .\release.ps1

The script automatically uses the detected MODTEST installation as compile references without changing its files, builds OrbusQuestHelper.dll, pushes the committed source and publishes a **stable** GitHub Release with that DLL (no interactive prompts). Pass -GameDir if the default game is not present. For a GitHub preview ignored by RebornManager, run .\release.ps1 -Preview.

Increment the BepInPlugin version in src/OrbusQuestHelper/Plugin.cs, commit and publish for each subsequent update. A release is available publicly to RebornManager users; test locally before stable distribution when possible.

## RebornManager

The mod is in [RM's mod catalogue](https://github.com/Hor1zonVR/OrbusRebornManager/blob/main/catalog/mods.json). RM installs the latest **stable** release's OrbusQuestHelper.dll to a selected instance and updates it when auto-updates are enabled and the game is closed.

## Development

See AGENTS.md, docs/ROADMAP.md and docs/DIAGNOSTIC_RUN.md. The old broad scanner remains in source for reference but is not instantiated. The community map and fishing data are reference material, not yet bundled into the mod without review and permission.

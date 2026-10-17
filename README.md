# OrbusQuestHelper

A client-side quality-of-life mod for **OrbusVR Reborn Community Edition** using **BepInEx IL2CPP** on the Windows PC client.

**Current version: 0.1.0 — diagnostics only.** Mission sorting, journal navigation, event notifications and fishing guidance are planned, not implemented.

## Planned features

- Expandable mission regions, subdivided by mission type, with completion progress.
- Current-region filtering and ready-to-turn-in objectives.
- Optional story-journal Track buttons for NPCs and collectible locations.
- In-world direction, compass, map markers and optional waypoints.
- World-event alerts matched to the player's actual active missions.
- Fishing assistance with lures, ingredient sourcing, locations and cross-catch warnings.

## First milestone: read-only UI discovery

The initial DLL scans active scene objects while you browse the Missions menu and physical story journal. It records candidate Unity object names, hierarchy, native IL2CPP component type names and supported text labels. **It does not change game UI, move the player, alter quest progress or invoke quest callbacks.**

Reports are written to BepInEx/config/OrbusQuestHelper/Diagnostics/. Optional manual snapshot: Ctrl+Alt+K while the Windows game window has focus. See docs/DIAGNOSTIC_RUN.md.

## Build locally

Install the .NET 8 SDK (target framework net6.0). Launch the game with BepInEx at least once to generate interop assemblies.

In PowerShell, from this repository's root:

    $game = "$env:USERPROFILE\Downloads\OrbusVR-Reborn-Community-PC-MODTEST\OrbusVR-Reborn-Community-PC"
    dotnet build .\src\OrbusQuestHelper\OrbusQuestHelper.csproj -c Release "-p:GameDir=$game"
    Copy-Item .\src\OrbusQuestHelper\bin\Release\net6.0\OrbusQuestHelper.dll (Join-Path $game 'BepInEx\plugins') -Force

Remove any earlier OrbusQuestDiagnostics.dll prototype from plugins before testing, to prevent double scanning.

## GitHub Releases and RebornManager

The RebornManager catalogue references each mod's own GitHub repository and downloads the latest stable release containing one DLL that matches its assetPattern. This repository includes a manual preview/stable publishing workflow that **requires separately configured private build references** (docs/RELEASES.md).

The planned catalogue entry is in docs/MANAGER_CATALOG_ENTRY.json. **It has not been added to RebornManager's public catalogue yet**; first verify a stable version in-game. The initial diagnostics version should remain a preview.

## Data and attribution

Community-supplied Orbus data sheets and fishing notes are reference material, not automatically redistributable public assets. Verify data accuracy, permission and server compatibility before including derived tables in future releases. Do not upload proprietary game assets or interop DLLs to this public repository.

See docs/ROADMAP.md and AGENTS.md.

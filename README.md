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

## One-command local release — same as RebornCam

Build with your own BepInEx interop DLLs, push the committed source, and upload OrbusQuestHelper.dll to a GitHub Release:

    git pull origin main
    .\release.ps1

The script confirms the GitHub account, branch, clean working tree and version, locates the modded Orbus game for compile references, builds the DLL, pushes main and creates the release. The default is **stable** (with a PUBLISH confirmation) so RebornManager can install and update it. For a build that RM must not install:

    .\release.ps1 -Preview

For game copies stored in a RebornManager instance instead of Downloads, pass the game folder when launching the script:

    .\release.ps1 -GameDir "C:\path\to\your\OrbusVR-Reborn-Community-PC"

Increment the BepInPlugin version in src/OrbusQuestHelper/Plugin.cs before each new release, and commit it. The initial diagnostics DLL has not yet been compiled or validated in-game.

## RebornManager integration

The catalogue entry is already live in [OrbusRebornManager/catalog/mods.json](https://github.com/Hor1zonVR/OrbusRebornManager/blob/main/catalog/mods.json). The manager installs only the latest **stable** release that contains exactly one asset matching OrbusQuestHelper.dll. It checks for updates when the catalogue/release check runs; automatic installation of newer versions happens when auto-updates are enabled and Orbus is closed. Until the first stable DLL release exists, QuestHelper will appear unavailable.

A separate optional GitHub Actions publishing workflow is also available, but it needs private reference configuration; **local release.ps1 does not**. See docs/RELEASES.md.

## Data and attribution

Community-supplied Orbus data sheets and fishing notes are reference material, not automatically redistributable public assets. Verify data accuracy, permission and server compatibility before including derived tables in future releases. Do not upload proprietary game assets or interop DLLs to this public repository.

See docs/ROADMAP.md and AGENTS.md.

# OrbusQuestHelper

A client-side quality-of-life mod for OrbusVR Reborn Community Edition (Windows PC, BepInEx IL2CPP).

**Current source: v0.2.0 — first functional PowerUI Missions navigation.** The original UI is not replaced, and mission progress/rewards/turn-ins are never modified by this feature.

## v0.2.0: Weekly / Regular / Top

QuestHelper now accesses the game's actual PlayerMenu.document using generated Assembly-CSharp interop, rather than scanning unnamed Unity meshes. It adds a small in-game QUESTHELPER navigation strip to the existing Missions tab, with **Weekly**, **Regular** and **Top** VR-clickable controls. Known weekly/regular section headers are accented.

The native mission tiles, hover tooltips and mouse-down reward interactions are left entirely intact. This is intentionally the first safe UI improvement; full collapsible categories and region sorting need live DOM testing plus a reliable mission-to-region mapping.

**Untested:** Event binding and exact section scroll coordinates must be checked in the user's VR client. If the controls don't appear or don't respond, send BepInEx/LogOutput.log and a screenshot. No more generic auto-scanning is running.

## Planned next steps

- Verify native DOM section headings and VR clicks on QuestHelper's own controls.
- Add region/type grouping, collapsed category states, and turn-in highlights without altering mission data or rewards.
- Add optional journal tracking, compass directions, world events and fishing assistance.

## Enable or disable

Quick navigation is enabled by default. To turn it off, edit the generated config file at BepInEx/config/com.horizon.orbus.questhelper.cfg, change EnableQuickNavigation to false under the Missions section, and restart the game.

## Build / publish using your usual workflow

From the OrbusQuestHelper repository on your PC:

    git pull origin main
    .\release.ps1

The script uses your game's generated BepInEx interop, builds the DLL, and publishes a stable GitHub Release when successful. Quit Orbus before updating the mod through RebornManager.

## Development information

- docs/MISSIONS_IMPLEMENTATION_RESEARCH.md — verified PlayerMenu, PowerUI and MagicalActual Mission APIs from the local game metadata.
- docs/DIAGNOSTIC_RUN.md — v0.2.0 test instructions and known limitations.
- docs/ROADMAP.md — later functionality.

The previous broad snapshot and UI interaction diagnostic source remains available in the repository for reference but is no longer enabled in the plugin.

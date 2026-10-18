# OrbusQuestHelper roadmap

## v0.1 Diagnostics
- Read-only Mission and Journal Unity object hierarchy survey
- Native IL2CPP component type identification
- Manual and bounded automatic snapshots
- Compare logs with VR screenshots
- v0.1.1: filter false-positive terrain _Quest meshes, discover native SuperTextMesh components, and limit redundant reports

## v0.2 Mission organisation
- Determine actual mission state and type APIs from diagnostics
- Expandable region > mission type categories
- Accurate Bronze, Silver and Gold progress
- Current-region focus and turn-in indicators

## v0.3 Journal and navigation
- Investigate safe Track button integration
- Local-client compass, target beacon and optional map marker
- Location metadata for NPCs, collectibles and quest objectives
- Avoid pathfinding claims: direct compass direction is not a walking route

## v0.4 Fishing assistant
- Lure recommendations based on verified community/game data
- Cross-catch warnings, spawn regions and ingredient sourcing
- Optional in-journal reference

## v0.5 Public event assistant
- Validate event schedule versus Community Edition live timing
- Match event types and regions to the player's active missions
- Optional alerts, closest teleport and clear event Gold requirements

Never modify server-side mission state or interfere with the VR rig. Validate all location records before presenting them as authoritative.

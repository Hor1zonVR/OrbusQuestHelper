# OrbusQuestHelper roadmap

## v0.1 Diagnostics
- v0.1.5: event-oriented cursor raycasts, EventSystem selection and manual canvas survey; one interaction log per session
- Read-only Mission and Journal Unity object hierarchy survey
- Native IL2CPP component type identification
- Manual and bounded automatic snapshots
- Compare logs with VR screenshots
- v0.1.1: filter false-positive terrain _Quest meshes, discover native SuperTextMesh components, and limit redundant reports
- v0.1.4: separate real Canvas from CanvasRenderer; enumerate non-wardrobe inventory icons and game component types to locate Missions UI

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

## v0.2.0 initial PowerUI integration
- Implemented actual in-game Weekly / Regular / Top navigation buttons and section accents using the original PlayerMenu.document.
- No original mission tile handlers, quest state, rewards or claims are changed.
- Validate PowerUI delegate bridge and exact live section header/scroll hierarchy in VR; then implement collapsible grouping and region mapping.

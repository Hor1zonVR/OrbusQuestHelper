# OrbusQuestHelper

A client-side mission and quest quality-of-life mod for **OrbusVR Reborn Community Edition**, running under BepInEx IL2CPP on PC VR.

## Current development version: v0.3.0 — Region → Activity Missions

**The old experimental blue QUESTHELPER navigation / Weekly / Regular / Top bar is completely removed.**

QuestHelper v0.3.0 reorganises the game's **existing, original mission icon elements** inside the Missions tab into sections:

- **Region**, such as Highsteppe, Lamavora, Hulthine's Basin, Flooded Rainforest, Wastelands, Battlegrounds or Dungeons.
- **Task**, such as Monster Hunting, Critter Capture, Catch Fish, Fishing by Weight, Gathering, Dragon Racing, Public Events or other activities that genuinely exist in the player's mission list.
- **Original native mission icons**, without recreating their rewards, sprites, hover/click handlers or gameplay state.

Unlike v0.2.x, section headings use plain HTML and inherit **the game's own menu font, parchment background, colours and dimensions**. No new coloured overlays or separate navigation controls. A heading is collapsible when the game's PowerUI API accepts the native pointer callback; otherwise, the group stays fully expanded.

The UI is modified only after detecting an actual Missions document with original \`vendor-mission\` entries. Empty tabs, unrelated Achievements/Talents/Equipment pages, and unsupported PowerUI layouts fall back to vanilla.

### Mission classification

The user's OrbusVR data workbook includes a Missions table. The mod contains a compact, local, read-only catalogue of **152 distinct mission IDs** with unambiguous region/task mappings. One duplicate ID with conflicting mission definitions was intentionally excluded. For other missions, QuestHelper reads the live mission's type, description and checklist (including an explicit zone if available) and uses conservative classification. A missing zone is never treated as proof that a mission belongs to the current region. Unknown region or task entries remain visible under a fallback group.

No game descriptions, rewards or raw proprietary game assemblies are embedded in the repository.

### Safety and compatibility

- Does not call \`TurnInMission\`, \`BuyVendorMission\`, mission completion, account or server APIs.
- Moves existing PowerUI mission nodes rather than cloning them; native callbacks stay attached to the original nodes.
- Archives the original Mission section elements temporarily and restores their original parentage and order upon leaving Missions or disabling the feature.
- Handles both old \`PowerUI.Document/Element\` and newer \`PowerUI.HtmlDocument/HtmlElement\` via runtime reflection, avoiding BepInEx startup type-resolution failures from hardcoded mismatched API references.
- Includes one-time, targeted log explanations for missing DOM elements or unsupported builds, rather than huge generic UI scans.
- **Live VR validation is still needed**: the v0.3.0 code has not been compiled or verified inside either client yet. First-test issues may require revisions.

## Build and test on Windows

With Orbus closed:

\`\`\`powershell
git pull origin main
.\release.ps1
\`\`\`

The release script defaults to \`%LOCALAPPDATA%\OrbusRebornModdingInstances\Modded Orbus\` for its generated interop references; pass \`-GameDir "C:\path\to\generic\Orbus"\` if your test is on a different installation. It uploads a release used by RebornManager if the build succeeds. Publishing a stable release affects other users, so consider \`-Preview\` while testing; previews aren't offered in RM and must be installed manually.

To build a DLL **without publishing**:

\`\`\`powershell
dotnet build .\src\OrbusQuestHelper\OrbusQuestHelper.csproj -c Release "-p:GameDir=$env:LOCALAPPDATA\OrbusRebornModdingInstances\Modded Orbus"
\`\`\`

The resulting DLL is at \`src\OrbusQuestHelper\bin\Release\net6.0\OrbusQuestHelper.dll\`.

## Configuration

\`BepInEx/config/com.horizon.orbus.questhelper.cfg\` → \`[Missions]\` → \`EnableRegionTaskGrouping = false\` to disable the organiser. The old \`EnableQuickNavigation\` setting, if present, is ignored.

## Technical notes

- [Region / task implementation design](docs/REGION_TASK_MISSIONS_DESIGN.md)
- [Mission UI static research](docs/MISSIONS_IMPLEMENTATION_RESEARCH.md)
- [First test and recovery instructions](docs/DIAGNOSTIC_RUN.md)
- [Roadmap](docs/ROADMAP.md)

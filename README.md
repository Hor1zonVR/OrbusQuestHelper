# QuestHelper v0.4.0 — experimental HTML-first Missions grouping

**Offline verification succeeded on the actual captured Missions page:** 49/49 original mission icons and hover/status markup preserved, with 37 known regions and all 49 tasks classified. The uploaded grouped preview was checked for duplicate entries. The pure transformer and runtime scoping gates passed **14/14 GitHub Actions checks**.

v0.4.0 now includes a **default-off** Harmony prefix targeting the legacy `PowerUI.Element.set_innerHTML(string)` call for the exact native `missions-container` element. This changes only the HTML before the game's own renderer parses it. The previous broken node-reparenting component is still completely disabled. **The hook has not yet been compiled against the user's installed game or tested in VR.**

**Please DO NOT run `release.ps1` to publish a stable RebornManager update until the experiment is validated.** We now have a local test installer that backs up the existing DLL and config switch without publishing anything. See [v0.4.0 opt-in test and restore instructions](docs/HTML_ORGANISER_V040_LOCAL_TEST.md).

---
## Captured Missions template validated — 2026-10-20

The v0.3.6 one-shot original Missions HTML capture succeeded: **49 icons, 14,477 characters**, using `div.vendor > div.vendor-reputation` inside the native 20px-padded Missions container.
All 49 mission icons are direct children of `vendor-reputation`. They are preceded by a native image-backed "New Missions Available" notice and a vanilla "Your Level Missions:" heading.
The **offline-only** Region → Task transformer now preserves the notice verbatim, replaces only familiar level/weekly headings, retains all original icon markup and parent wrappers, and recognises `vendor-reputation`.
The existing catalogue classifies all 49 captured mission IDs by task and 37 by verified region; the 12 unknown regions remain Unassigned.
GitHub Actions compiled the new transformer and passed **11/11 regression tests**, including tests modelling the exact captured wrapper shape and the notice. The real original HTML and log were not committed.
The next safety gate is running the actual private captured HTML through `tools/MissionHtmlLab` on the user's Windows PC. **The game itself remains unchanged in v0.3.6.**

See [the updated offline transformer progress](docs/MISSIONS_HTML_TRANSFORM_PROGRESS.md).

---
# QuestHelper v0.3.6 — safe original Missions HTML capture and offline grouping tests

**Current status:** The old DOM-moving organiser is still completely disabled. v0.3.6 adds a standalone lossless Region → Task HTML transformer and a **one-time read-only capture** of the original `missions-container` HTML so we can validate it against the actual game. **No in-game grouping or visual changes are enabled yet.**

The offline transformer **built successfully and passed 8/8 GitHub Actions regression checks**. It preserves each original mission tile's HTML verbatim and refuses unsupported layouts. The new game-side HTML capture has **not yet been compiled or tested in VR**.

After publishing v0.3.6 using the existing `git pull origin main` and `.\release.ps1` workflow, launch the generic game, open **Inventory → Missions**, wait about ten seconds, then find the resulting `*_MissionsOriginal.html` in the active instance's `BepInEx/config/OrbusQuestHelper/Diagnostics` folder. Share privately, not on GitHub. The capture switches itself off after succeeding; the game's original UI stays unchanged.

See [v0.3.6 implementation and capture instructions](docs/MISSIONS_HTML_TRANSFORM_PROGRESS.md) and [the proven native HTML call chain](docs/NATIVE_MISSIONS_HTML_CALL_CHAIN.md).

---

## Safe offline Missions rendering research (2026-10-20)

**New native-code finding:** In the generic-client GameAssembly.dll, `PlayerMenu.doUpdateInventory()` generates the mission icon HTML and writes it to `missions-container.innerHTML`. This is proven through the IL2CPP metadata usage mapping and exact native call sites. `PlayerMenu.UpdateMissions()` instead sends the mission data request and updates `lorequest-content`. Read [the native Missions HTML call-chain report](docs/NATIVE_MISSIONS_HTML_CALL_CHAIN.md). This gives us a pre-render interception target; it is not yet a verified safe runtime hook, and v0.3.5's safety mode remains active.



**The active QuestHelper plugin remains v0.3.5 SAFE MODE.** No in-game region/task layout modification is enabled. Earlier repeated DOM reparenting attempts caused duplicate icon rendering and freezes; do not enable the old UI organiser.

We've now inspected the original game's IL2CPP string literals and exact generated PowerUI method signatures. The results are in [Missions rendering investigation](docs/POWERUI_MISSIONS_RENDERING_INVESTIGATION.md), including a correction: the `PlayerMenu-Your...-header` keys were **not** confirmed to be DOM element IDs.

A **standalone, read-only [.NET 8 Orbus UI Inspector](tools/OrbusUiInspector/README.md)** is included for inspecting both generic and FourOfSpades generated interop assemblies without launching Orbus, changing its UI, or uploading game files. This is independent of `release.ps1`, and no QuestHelper DLL release is required to use it.

The proposed future solution is to investigate how `PlayerMenu.UpdateMissions()` builds the HTML and, if a verified safe hook exists, reorganise **the string before the PowerUI renderer sees it**. The required call relationship has **not** yet been verified, and the safe-mode plugin will stay unchanged until it has.

# OrbusQuestHelper

A client-side mission and quest quality-of-life mod for **OrbusVR Reborn Community Edition**, running under BepInEx IL2CPP on PC VR.

## Current development version: v0.3.5 — safety hotfix

**Mission UI editing is temporarily disabled in v0.3.5, including for users whose existing config says enabled.** The v0.3.4 game log shows mission tile counts doubling every ~2 seconds: **56 → 112 → 224 → 448**, then reaching the 512-node scan cap. Each pass reported `insertBefore unsupported`. The earlier rollback tried to append **all** native mission tiles into their original parents even when **zero tiles had been moved**. This appears to duplicate PowerUI nodes, grow the scroll list and freeze the game.

v0.3.5 intentionally does **not register the MissionMenuOrganizer component**. A second hard safety check in its engine prevents accidental invocations from changing the menu, and the rollback code is corrected to restore only actually moved elements. The grouping algorithm and mission metadata are retained for future rework, but the custom Region → Task layout is **not active** in this hotfix. Relaunch Orbus with the updated DLL to reset any damaged in-memory PowerUI state; no game files or mission data were changed.

We will not enable mutation again just to test another speculative DOM rearrangement. The next development step is read-only inspection of the game's actual PowerUI insertion/removal semantics and scroll container CSS, and/or an isolated UI prototype.

### Previous experimental version: v0.3.4 — Region → Activity Missions

**v0.3.4 layout repair (not yet tested in-game):** v0.3.3 successfully grouped 56 mission icons, but the video showed the old mission list still painted above the parchment and the grouped rows rendering below the panel. This version stops moving the vanilla markup to a detached archive. It places the region/task root **before the first original child inside the live missions-container**, hides original wrapper sections **while they remain inside the live document**, and preserves their display values and each original icon's parent/sibling placement for rollback. It also resets the scroll position to the top of the new list and safely restores original wrappers if the grouped root disappears during a menu refresh. The goal is to preserve native scroll clipping and eliminate duplicate render geometry, but the user's next visual test is still required.

### Earlier development notes



**Confirmed legacy compatibility bug:** inspecting the actual generic client's generated \`Assembly-CSharp.dll\` method tables showed that \`PowerUI.Css.ElementStyle\` exports \`set_height\`, but **not \`set_clear\`**, and \`PowerUI.Element\` exports \`set_className\`, but **not \`setAttribute\`**. v0.3.2's mandatory \`clear:both\` setting therefore failed and quietly skipped building the interface.

**v0.3.3 fix:** Mission categories are now laid out in measured, explicit-height rows that contain the game's original floating quest tiles; no unsupported \`clear\` property or \`setAttribute\` call is required. Row height comes from the game's own existing \`ElementStyle.height\` API. The original DOM stays detached while the grouped list is shown, and all original icons and mission state are preserved. Precise pre-mutation failure messages and a 20-second DOM heartbeat make future problems visible in \`BepInEx/LogOutput.log\`.

**Still unverified:** compiling v0.3.3 on Windows and how the resized rows look and behave in VR. Do not assume perfect layout or newer-client compatibility until those tests have succeeded.

**Bugfix (v0.3.3):** v0.3.0 loaded correctly but showed nothing: it misinterpreted the game's numeric `currentInventoryPage` field as a textual Missions tab identifier and relied exclusively on a brittle `getElementsByClassName` collection reader. v0.3.3 removes that invalid guard, searches the native PowerUI element tree as fallback, and logs a concise, non-sensitive structure summary if the original mission tiles still cannot be discovered. It will not rearrange mission icons lacking native IDs.

**The old experimental blue QUESTHELPER navigation / Weekly / Regular / Top bar is completely removed.**

QuestHelper v0.3.3 reorganises the game's **existing, original mission icon elements** inside the Missions tab into sections:

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
- **Live VR validation is still needed**: the v0.3.3 code has not been compiled or verified inside either client yet. First-test issues may require revisions.

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

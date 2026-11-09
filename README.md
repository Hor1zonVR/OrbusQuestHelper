# QuestHelper v0.4.13 candidate — more reliable pinned objectives

**New official content research:** The official Community server [mission definition files](https://github.com/OrbusVR-Community/reborn-server/tree/main/content/missions) contain 298 records, including 152 unambiguous fixed IDs. This preview can **optionally read your own installed** `content/missions` and sibling `content/items/item-serverdata.txt` to render more specific objective names (e.g. Capture squirrels, Drink Healing Potions), while preserving native progress. Configure `[Missions] LocalServerMissionsFolder` in the BepInEx config after first launch; no game assets are included in this mod. Reward-tier requirements are incremental, not independent totals; tier math and tests have been corrected.


**Review branch only, not a released DLL.** Auto placement returns to the detached world-space level/XP bar rather than a headset child; long-term 10,000-count tasks display the real earned count unless a usable small reward milestone is verified; mission labels use checklist actions and trusted activity categories rather than narrative paragraphs. Local mission polling runs every 0.5 seconds.

**Spades background server refresh is still unverified.** If the game's own mission records are stale until the Missions tab triggers a server request, faster local reads cannot solve that. We must map the exact native Spades refresh/request method, not guess obfuscated API names. See [v0.4.13 changes and test plan](docs/PINNED_MISSIONS_V0413.md).

---


---

## QuestHelper v0.4.12 — stable pinned HUD across reward tiers

The pinned mission tracker no longer attaches its Unity TextMesh as a
child of Orbus's native XP/level UI, preventing the enormous overlay
that could appear after the Lizards quest reached Silver. Auto now
prefers a compact active stereo headset anchor while still requiring
the game's live TopHud; LevelHud is a separate world-space object that
reads the level label position without changing its hierarchy or scale.

A dedicated `MissionHudDriver` now updates pinned quest values
independently of the Inventory > Missions HTML organiser. Existing
saved pins are polled from the native `PlayerCharacter.GetMission`
object every 0.85 seconds while in the world; first-time Missions
loading may still be needed if the game hasn't populated its local
records. The mod never fabricates progress or sends server requests.

**Remaining verification:** On Spades, opening Missions may be the
only way the client requests new mission data from the server. If
counts are stale despite the always-on local reader, the verified
Spades equivalent of Community `PlayerMenu.UpdateMissions()` needs
to be mapped before background mission refresh is added. No
unverified obfuscated calls or repeated server requests are used.

Run `git pull origin main` then `release.ps1` to publish the usual
RebornManager DLL. Full live VR test and limitations are documented
in `docs/PINNED_MISSIONS_V0412.md`.

# QuestHelper v0.4.11 — lobby HUD lifecycle fix

Returning to the main menu removes Orbus's gameplay TopHud. Earlier QuestHelper versions could interpret that as a reason to attach the pinned-mission tracker to the camera, causing the text to float immediately in front of the user in the lobby.

QuestHelper now checks that **both the native gameplay TopHud and its levelLabel are active** before rendering a pinned tracker, whether placement is Auto, LevelHud or Headset. It also checks the previous anchor every frame, deactivating and destroying any leftover tracker immediately when the player leaves the world. It then automatically reconstructs the tracker on re-entry. Saved pins, favourites, preferred position and size are untouched.

The previous ±1m horizontal/±0.75m vertical ranges, Lizards/Ruined Potions labels, and Pinned Missions section remain included. No original HUD elements are modified. Requires a local build and lobby → world → lobby → world verification.

---

# QuestHelper v0.4.11 — clearer quest names and wider HUD adjustments

User-verified narrative quests now get useful short tracker labels:
**"Now that it's known..." → Lizards** and
**"Keeping a stockpile..." → Ruined Potions**. These aliases only match
those distinct mission descriptions. Other quests still use native
ChecklistItem and Mission descriptions, with generic catalogue fallback.
Objective counts and Bronze/Silver/Gold progress are unchanged.

RebornMenu's tracker offsets now allow **-1.00m to +1.00m horizontally**
(previously ±0.20m) and **-0.75m to +0.75m vertically** (previously
±0.20m). Existing values and defaults are preserved. Adjust live from
Settings → Mods → QuestHelper. Mission titles ending in a colon also
no longer display an extra colon in the tracker.

Source ready; requires a local release build and headset test.

---

# QuestHelper v0.4.10 — pinned quest priority, compact level HUD and remaining counts

**Based on in-game screenshots from v0.4.9**, the quest text was too large and floated around the view. The new build reduces the text glyph size to roughly one-quarter and anchors it next to the **actual Level / XP label**, rather than relying on a camera-relative location whenever Auto is selected.

- **Pinned Missions** is a section at the top of Inventory > Missions, above Favourites. Each icon remains in exactly one section even if pinned and starred, and stays pinned across Region/Activity filter switches.
- Native `ChecklistItem.description` can be empty (as verified in the user's prior logs); the tracker now falls back to `Mission.description`, then the native mission type. No more bare `Objective` placeholders. Exact creature names can only be displayed if the game exposes them through one of these sources.
- Count lines now show progress **and how many remain**, such as `Sewer Rat: 5/8 - 3 left`. If a real Bronze/Silver/Gold reward requirement is on the same numeric scale as the checklist, the tracker can display `N to Bronze` instead of misleadingly presenting the entire event target as the next milestone. The full event target may still differ from the next reward milestone.
- New RebornMenu controls: **Tracker Left / Right** and **Tracker Up / Down** (metres) alongside text size and placement. Existing pins are preserved.
- Native buttons, quest data and the Level / XP UI remain untouched. This update still needs a local IL2CPP compile and in-headset validation.

**Publish/test** via the usual `release.ps1 -GameDir` and update through RebornManager. If the quest label is still generic, share the next `[QuestHelper]` log and a hover screenshot of that quest so the source of the exact species name can be identified.

---

# QuestHelper v0.4.9 — Activity view pin fix and visible tracker experiment

**Fix 1:** Star and Pin controls no longer disappear after selecting **By Activity**. The v0.4.8 Activity rendering branch forgot to wrap original mission icons with QuestHelper's own controls; this has been corrected without changing the game-generated mission icon HTML.

**Fix 2:** the `[QuestHelper/HUD]` log confirmed a real checklist objective and successful `TopHud.levelLabel` attachment in v0.4.8, but text wasn't visible. The new renderer avoids inheriting the tiny local scale of the game's `SuperTextMesh`; it checks whether the original local nameplate is hidden or excluded from the stereo camera and can use a small headset HUD fallback. `Settings → Mods → QuestHelper` exposes **Tracker Placement** (`Auto`, `LevelHud`, `Headset`) and **Tracker Text Size**.

The readable Community dump defines `TopHud` with `trackingTarget`, `xpBar`, `levelLabel` and status fields; Renewed/Spades has the equivalent obfuscated `clf` class with `bhch` as the level label. Those mappings are supported by the user's test log, but neither proves the visibility of the local player's native label in the headset.

The native event binder now logs only three successful PowerUI listener signatures, rather than hundreds of repeated messages when 39 missions each have Star and Pin buttons.

**Needs VR testing:** the first priority is verifying a visible and correctly sized tracker. If `Auto` doesn't show it, choose `Headset` in RebornMenu and send the new `[QuestHelper/HUD]` log line showing the actual anchor, render flags and parent scale. Existing missions and original level/status UI are never modified.

---

# QuestHelper v0.4.8 — native favourites, pins and optional level HUD

**First experimental implementation:** the Missions page now uses native Orbus-style Region / Activity tabs, with **Star** and **Pin** controls for each original mission icon. Starred quests appear together in Favourites. You can pin **up to three missions**, and QuestHelper reads their real `ChecklistItem.killCount` / `timesToKill` values for a compact optional tracker beside Orbus's existing level label.

This uses the user's readable Community and obfuscated Renewed/Spades IL2CPP dumps to map actual player, quest and top-HUD classes. It never invents progress, never modifies the game's quests/rewards, and leaves the original level UI untouched. Native pin choices are stored locally in BepInEx. The new HUD positioning, Star/Pin button layout and all VR interactions still **need in-game verification**; the proven previous release is v0.4.7.

[What's new, exact native class mappings and test/rollback instructions](docs/PINNED_MISSIONS_V048.md).

---

# QuestHelper v0.4.6 — Quest Tracking Phase 1

**The Missions organiser is working in-game on Spades, including the native Region / Activity controls. Quest tracking itself is still in development.**

This version adds an automatic **one-shot, read-only native mission-data probe**. When you open Inventory → Missions, QuestHelper checks up to three existing regular mission records for genuine status, progress and checklist fields. It never modifies objectives, rewards, native quest-icon markup or the VR rig. The result is written to a handful of lines starting with `[QuestHelper.TrackProbe]` in your local BepInEx log. If Spades has renamed or hidden these APIs, QuestHelper logs that instead of inventing progress.

**No config edits.** Once the current Spades data model is verified, the next development steps are a safe **Track / Untrack** interaction on the Missions page and an optional compact HUD that displays only real, live quest progress. This release does not yet add a tracking HUD.

[Tracking Phase 1 and what to test](docs/QUEST_TRACKING_PHASE1.md).

---

# QuestHelper v0.4.4 — interactive Missions view selector

QuestHelper v0.4.3 was **confirmed working in-game on the latest Spades client**, automatically transforming the original Missions page into **By Region** with **all 62 original icons**. Its only reported UI problem was the **View: Region | Activity** selector: the game log reported `Native PowerUI view selector events unavailable`. This is an event-binding failure, not a failed mission transformation.

v0.4.4 improves the native selector binding. The generic client's `OnMouseDown` remains supported. Spades' `Dom.EventTarget.addEventListener` has many different delegate and overload signatures; the binder now handles two- and three-parameter native variants, restricts callback types to compatible `Dom.Event`/`Dom.MouseEvent` types, and listens for both VR `click` and `mousedown`. A controller event queues a mode change until the next Unity Update so we never replace the page mid-click. Successful listeners are retained, partial failures do not duplicate callbacks, and failed registrations retry at most every ten seconds while the same UI nodes remain active. The log now gives the attempted native delegate signature and an error reason if binding still fails.

**No configuration edits for ordinary players.** Install the stable v0.4.4 DLL through RebornManager and switch on the Missions parchment. The user's existing `By Region` display, old BepInEx config, native quest hover markup, weekly tasks, and all mission icons remain unchanged.

**Verification:** 20/20 lossless HTML tests, managed dual-client hook regression, and 5/5 tests compiling the actual `PowerUiRuntime` event binder against synthetic DOM event APIs. These are regression/syntax checks; **real Spades controller-click behaviour still requires an in-game test**. If the selector fails, provide a fresh `BepInEx/LogOutput.log` containing the new detailed `[QuestHelper]` event-binding diagnostics. The build must be compiled with the matching Spades IL2CPP interop by `release.ps1 -GameDir <Spades instance>`; GitHub's automated build credentials are not configured.

---

# QuestHelper v0.4.3 — automatic grouping on Community and Spades

**Install through RebornManager and launch Orbus. No config editing is required.** The mod now automatically installs the appropriate native HTML hook on both Community Edition (`PowerUI.Element`) and Spades Edition (`Dom.Element`). Existing `EnableHtmlRegionTaskGrouping=false` and `SpadesMode=Observe` entries from earlier QuestHelper versions do not block the new automatic setup.

The compact native **View: Region | Activity** interface is unchanged. By Region, the new Spades weekly missions appear together under **Weekly Missions**. By Activity, the recognisable weekly fishing, event, monster-hunting and salvaging quests appear alongside their regular counterparts, including a single **Fishing** activity. Unrecognised weekly activities remain visible under **Weekly Tasks**, with no guessed quest location.

The real Spades capture confirmed **62 distinct original icons: 8 weekly and 54 level missions**. All 54 level IDs already exist in the shared QuestHelper catalogue. The capture also confirmed the original `ng.eqj` / `ng.eqo` mouse handlers and the game's original nested parchment HTML. Every native mission tile is copied byte-for-byte; no icon duplication, source event changes, DOM reparenting, or quest-state changes.

**Verification:** 20/20 offline regression tests passed, including a synthetic 62-mission Spades fixture and managed dual-client hook smoke tests. The source is ready for a game-instance build and live test; a successful offline test does not yet prove that Spades' real `missions-container` identifier matches the hooked setter's target or that the in-game selector responds correctly.

For developers only: `[Missions] AutoOrganiseMissions=false` opts out, and `[Investigation] ReadOnlySpadesProbe=true` temporarily disables Spades transformations for diagnostic logging. Players need neither option. Do not publish captured personal Missions HTML to GitHub.

---

# QuestHelper v0.4.2 — Spades compatibility investigation

The latest Spades client does **not** use the generic game's `PowerUI.Element` / `PowerUI.Document` types. It uses `PowerUI.HtmlElement` / `PowerUI.HtmlDocument`, inheriting the HTML setter from `Dom.Element.set_innerHTML(string)`. QuestHelper v0.4.2 includes a **second Harmony interception target** for that modern API while retaining the working generic-client implementation.

**The modern hook is read-only by default** (`[Missions] SpadesMode = Observe`) and only installed if `EnableHtmlRegionTaskGrouping = true`. It records a bounded number of original HTML setter details, and `[Investigation] CaptureMissionHtmlOnce = true` saves one private pre-render Spades HTML sample if mission markers are detected. Spades' old `PlayerMenu.document` is absent; the modern path uses the observed HTML element's owning document as a fallback.

The `SpadesMode = Transform` option is implemented but **must not be used until the exact original Spades mission markup is validated**. The modern DOM hook has passed managed tests, but it has not been run in the real Spades client. Do not confuse a working managed test with a verified in-game mod.

The generic client's compact **Region | Activity** organiser remains unchanged, with all old unsafe DOM reparenting permanently disabled. See [Spades compatibility and test instructions](docs/SPADES_CLIENT_COMPATIBILITY.md).

---

# QuestHelper v0.4.1 — compact Missions organiser

**By Region | By Activity:** v0.4.1 replaces the long nested Region → Task lists with **one level of headings**. In **Region** view, all missions for a region appear together, sorted by activity so similar icon tiles remain adjacent, with no extra activity headings. In **Activity** view, all missions of one activity are together across all regions, with no region subheadings. Both fishing-by-weight and catch-fish missions share one **Fishing** heading; battleground objective variants share one **Battlegrounds** heading.

A compact native-font **`View: Region | Activity`** selector appears above the quests. Native PowerUI mousedown handlers switch the HTML once on click; the selected view is saved for next launch. Original icons, native hover callbacks, mission status classes, native header notice, and parchment layout are preserved. The old unsafe DOM reparenting remains disabled.

The existing **`[Missions] EnableHtmlRegionTaskGrouping = true`** setting still enables the organiser. New **`[Missions] GroupingView = Region`** (or `Activity`) chooses the starting view or acts as a fallback if the game's native view click binding is unavailable. Set the config when Orbus is closed, then restart.

**Testing:** GitHub Actions passed **17/17 lossless-HTML regression tests** and the managed hook/binding smoke test. The older v0.4.0 hook was confirmed working by the user in VR. The new native selector clicks and compact layout still need a gameplay check.

---
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

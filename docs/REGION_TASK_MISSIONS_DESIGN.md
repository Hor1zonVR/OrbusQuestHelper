# QuestHelper: native-styled, region-first Missions redesign

Status: implementation specification based on player feedback on v0.2.0 and direct inspection of both game's generated IL2CPP assemblies (2026-10-19). This is **not yet a released or tested feature**.

## Player requirement

Replace the experimental navy QUESTHELPER / Weekly / Regular / Top strip entirely. The original Orbus Missions screen should look and behave like Orbus's own UI (same layout, font, colours, hover, transitions, scroll and reward icons).

The actual hierarchy is:

Region (e.g. Highsteppe, Flooded Rainforest, Lamavora, Hulthine's Basin, Wastelands)
  -> Task/activity (Monster Hunting, Fishing, Salvaging/Gathering, Critter Capture, Dragon Racing, Public Events, Dungeons, etc., but **only if missions of this type exist**)
    -> Original live mission entries, including their status, progress and rewards

Weekly and regular missions are NOT navigation categories or extra top-level tabs; they should appear under their real region/activity. Preserve an original weekly badge where relevant, not a separate group. Current region first if a reliable local zone can be read; otherwise preserve stable region order. "General / Multiple Regions" and "Unassigned" are legitimate fallback groups when no region is established. Avoid guessing based on mission level, vendor location, or UI mesh count.

Prefer collapsible region headings and optionally collapsible activity groups, using a game-native header/control style. Persist expansion state locally by region+task when practical; never write state to server. A show-all/reset option can be considered later but NOT new Weekly/Regular/Top navigation controls.

## Verified game entry points

- Orbus's original UI is `PlayerMenu`, PowerUI HTML-like DOM, not Unity UI buttons. `PlayerMenu.document`, `PlayerMenu.updateMissionTab`, `PlayerMenu.UpdateMissions`, `PlayerMenu.GetMissionIcon`, `PlayerMenu.missionHover`, `PlayerMenu.missionStopHover`, and `PlayerMenu.missionClick`.
- The actual template strings contain `class='vendor'`, `class='textbutton'`, `class='inventory-box-lowres'`, `class='vendor-mission'`, `data-missionid-` markers and original `onmouseover='PlayerMenu.missionHover'` / `onmouseout='PlayerMenu.missionStopHover'` handlers, as well as native section headings. Reuse **existing HTML element classes and original mission nodes** instead of injecting custom blue styles or rewriting original icon callbacks.
- `MagicalActual.PlayerCharacter.Instance` and `PlayerCharacter.GetMission(int)` / `GetMission(string)` are present in the generic build's generated metadata.
- `MagicalActual.Mission` has `id`, `type`, `description`, `status`, `progress`, `checklist` and tiered rewards. `MagicalActual.MissionMonster` has `monsterType` and **`zone`**, which is a promising source of region assignments for combat objectives. Verify what `Mission.checklist` contains at runtime instead of asserting that the field always points to `MissionMonster`.
- Region names may also be explicit in mission descriptions or objective data. Never hardcode an ID-to-region assumption without confirming it.

## Two client builds / assembly compatibility

- **Generic / MODTEST base client:** older `PowerUI.Document`, `PowerUI.Element`, `PowerUI.OnDomEvent` in its generated `Assembly-CSharp.dll`; the v0.2.x experimental navigation actually appeared here, proving DOM changes can render.
- **FourOfSpades modified client via RebornManager:** `PlayerMenu` in `Assembly-CSharp.dll`, but PowerUI lives in `Assembly-CSharp-firstpass.dll` with a newer `PowerUI.HtmlDocument`, `PowerUI.HtmlElement`, DOM event/action interfaces; the prior DLL built against MODTEST cannot resolve `PowerUI.Element` on this build.
- A single published mod must identify the runtime API before invoking a build-specific adapter; do not copy DLLs between installations. Build/test against each matching interop. Use a safe no-op instead of startup TypeLoadException on an unsupported client variant. Consider separate adapter assemblies loaded conditionally or a reflection-based abstraction; do not blindly reference the old and new PowerUI types in a shared injected component.

## Implementation order and safety

1. Determine mission entry DOM selector and parent relationship for the current Missions page on the generic client; inspect only that document, not world objects. Confirm actual mission identifiers against `GetMission`.
2. Implement a region-and-task classification function driven by known objective data, with explicit unknown/multi-region fallback; keep weekly/regular metadata separate from grouping keys. Write a compact local report of unresolved mission ID/type/description only when requested, no chat/player/account data.
3. Reuse the native heading and icon DOM nodes; preserve identity and original `PlayerMenu.missionHover` and click handlers, scroll and tooltip behaviour. Do not call `TurnInMission`, `BuyVendorMission`, reward or progress mutation APIs.
4. Apply changes after the game's normal mission HTML refresh, idempotently; preserve the scroll position and open groups. Avoid rendering or regenerating hundreds of PowerUI meshes every frame.
5. Validate generic client visually and behaviorally first, then adapt to FourOfSpades using its own `HtmlDocument` / `HtmlElement` API and matching interop. Do not publish a stable release until the chosen build at least compiles and loads. Set a feature toggle and rollback path.

**Explicitly rejected:** unrelated blue menu bar, Weekly/Regular/Top shortcut buttons, recreating the entire Missions page with mismatched fonts or generic browser UI, and guessing region/task names from counts or screen location.

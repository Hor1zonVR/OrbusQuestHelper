# OrbusQuestHelper — Missions rendering investigation

Status: **offline static research, 2026-10-20**. The v0.3.5 safety lock remains enabled. No runtime hook or DOM mutation has been authorised by these findings.

## Private inputs examined

- User-provided generic/MODTEST IL2CPP interop: `Assembly-CSharp.dll` (11,319,296 bytes) and `Assembly-CSharp-firstpass.dll` (2,510,336 bytes)
- User-provided generic native `global-metadata.dat` (11,211,688 bytes; IL2CPP metadata v24)
- FourOfSpades modified-client active interop: `Assembly-CSharp.dll` (7,611,904 bytes) and `Assembly-CSharp-firstpass.dll` (7,934,976 bytes)
- Prior v0.3.3/v0.3.4 gameplay logs and screenshots, showing 56 recognised mission icons and the failure modes when DOM nodes are reparented.

This report includes **original technical findings and API names only**. Never commit or redistribute the underlying game DLLs, metadata files, dumps of proprietary markup, player mission state or extracted Unity assets.

## Confirmed: original game-side entry points

Both client builds define the global `PlayerMenu` type, including:

| Method | Verified ECMA-335 signature | Purpose (inferred from name; call relationship NOT yet proven) |
|---|---|---|
| `updateMissionTab` | instance `void ()` | Likely updates Missions tab state/label |
| `UpdateMissions` | instance `void ()` | Strong candidate for constructing the Missions page |
| `GetMissionIcon` | instance `string (Mission)` | Produces mission icon asset path or markup; precise behaviour unknown |
| `missionHover` | instance `void (UI event)` | Native mission tooltip handler |
| `missionStopHover` | instance `void (UI event)` | Native tooltip removal |
| `missionClick` | instance `void (UI event)` | Native click handler; must preserve |
| `ClearMissionsToolTips` | instance `void ()` | Clears native tooltips |

`PlayerMenu.document` is exposed on both. Signatures above are from the actual method-definition signature blobs, not guessed from a runtime Unity hierarchy. The generated interop DLL contains native entrypoint forwarding stubs **rather than the original C++ method implementation**: method signatures alone cannot prove which PowerUI setters a game method calls.

## Confirmed: exact native HTML conventions (generic build)

IL2CPP **string literal table** (separate from type/method metadata) contains these short fragments, with literal indices:

| Literal index | Finding |
|---|---|
| 11171 | `missions-container` — actual DOM container name used by PlayerMenu code |
| 11280 | `PlayerMenu-AvailableWeeklyMissions-header` |
| 11282–11284 | The mission purchase-icon template uses `inventory-box-lowres`, `vendor-mission`, `data-missionid-`, native `missionClick`, `missionHover` and `missionStopHover` callbacks |
| 11285 | `PlayerMenu-AvailableLevelMissions-header` |
| 11287 | `PlayerMenu-YourMissions-header` |
| 11601–11604 | An `updateMissionTab() called` diagnostic string; the `missions-tab` identifier and displayed label |
| 14979 | `<div class='vendor'>` — native Missions wrapper |
| 14982–14986 | Native heading and accepted-mission icon markup; `YourWeeklyMissions` and `YourLevelMissions` text keys plus `vendor-mission` / `data-missionid-` classes |

**Correction to earlier research:** the `PlayerMenu-YourWeeklyMissions-header` and similar strings are **not proven HTML `id` attributes**. They appear as standalone strings next to heading markup and may be localisation/text keys. Do not use `getElementById` on them until runtime verification.

The game's existing HTML already establishes the correct font, colour, mission tile size, event handlers, and `vendor` layout. Do not recreate these with custom floating icon widgets or disconnected meshes.

## Confirmed: different PowerUI APIs

### Generic/MODTEST

- `PowerUI.Document`, `PowerUI.Element` are generated under `Assembly-CSharp.dll`.
- `PowerUI.Document.getElementById(string)` returns an Element.
- `PowerUI.Element.get_innerHTML()` returns string, `set_innerHTML(string)` takes one string.
- `PowerUI.Element.getElementsByClassName(string)`, `appendChild`, `removeChild`, `RequireLayout`, `scrollTop`, `childNodes` exist.
- `PowerUI.Element.insertBefore` is **not available** in this build. The v0.3.4 implementation calling it therefore guaranteed a failed insertion.
- The generic `PowerUI.Css.ElementStyle` has a height setter but no compatible clear setter. Do not assume browser CSS APIs are available as managed setters.

### FourOfSpades modified client

- `PlayerMenu` is still in the generated `Assembly-CSharp.dll`, but its modern PowerUI types are external and exported by `Assembly-CSharp-firstpass.dll`.
- `PowerUI.HtmlDocument` and `PowerUI.HtmlElement` are different CLR types from the generic `Document/Element`.
- The modern `PowerUI.HtmlDocument` has `get_innerHTML()` and `set_innerHTML(string)` signatures; `PowerUI.HtmlElement` has its own DOM APIs, including `appendChild` and layout/scroll functions.
- Don't compile a single injected MonoBehaviour with fields of both build-specific PowerUI types: earlier v0.2.0 failed with `TypeLoadException`.

## Failure modes already established, not to repeat

1. **v0.3.1**: the classifier found original mission entries, but moving native nodes into generic groups resulted in their rendered geometry appearing outside the physical parchment.
2. **v0.3.2**: unsupported CSS setter led to a silent pre-mutation return.
3. **v0.3.3**: explicit-height rows avoided that return but grouped content appeared below the actual board, while original mesh geometry lingered.
4. **v0.3.4**: `insertBefore` was unsupported. The rollback mistakenly appended original untouched icons every retry, doubling the detected count (56 → 112 → 224 → 448 → scanner cap) and freezing the client.
5. **v0.3.5**: safe mode; **no MissionMenuOrganizer component registered** and independent `NativeMissionEditingSuspended=true` interlock.

## Safer proposed architecture — NOT IMPLEMENTED / NOT VERIFIED

Rather than ever reparenting original rendered PowerUI `Element` instances again:

1. Establish a **read-only rendering call chain**: does `PlayerMenu.UpdateMissions()` generate or set the HTML string for `#missions-container`, and is it populated using `Element.set_innerHTML(string)` or another PowerUI method? The native method body was not included in user uploads, so this needs a narrowly scoped runtime observation or authorised inspection of the matching native binary.
2. Preserve a verbatim copy of the **original generated HTML in memory before PowerUI parses/renders it**. Only in an isolated prototype, consider transforming the HTML string into Region → Task groups while retaining the game's own icon template, translated labels, `class` tokens, data IDs and `PlayerMenu.missionClick` / hover handlers. No appendChild/reparent/removeChild/rollback.
3. Ensure the resulting HTML is assigned **only once per real mission refresh**, not in a two-second polling cycle. Add an idempotence hash and a strict maximum size/count. Never allow recursive HTML interception or duplicate render passes.
4. Do not touch other inventory tabs or the player menu globally: positively identify the exact Missions container and caller. Prefer a feature flag default **off**, with full fallback to vanilla before any user testing.
5. Test on generic client first, then implement a separate verified adapter for the modern HtmlDocument API. No automatic client-family guessing based on DLL size.

## Immediate next verification

An **offline method inventory tool** is included under `tools/OrbusUiInspector/`. It uses .NET 8's built-in metadata reader and, when supplied with a game directory, compares the generated type/method signatures without executing game code or copying game files. It is intentionally independent of the BepInEx plugin.

The important unanswered question is the **native call relationship between `PlayerMenu.UpdateMissions` and PowerUI's HTML setter**. Presence of both names is encouraging, but does not prove the interception point. No modifications to the game or the safe v0.3.5 plugin are justified until that has been checked.

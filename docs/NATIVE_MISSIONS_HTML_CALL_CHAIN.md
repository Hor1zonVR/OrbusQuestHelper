# Confirmed generic-client Missions HTML rendering path

Research date: 2026-10-20. Evidence: the user's privately provided generic/MODTEST GameAssembly.dll (PE32+ x64, 32,063,488 bytes, 2026-10-02 build timestamp, SHA-256 66ded256ea6ac6f4340956f43805bceeec6b4740cd4c5e3fcb3cd0ed5594ae65) and the matching previously uploaded global-metadata.dat (IL2CPP metadata v24). **Technical analysis only; not a live patch. No game binaries, raw markup, metadata, or native code are committed.**

## Native method address mapping

The generated metadata defines native IL2CPP method indices. We located a contiguous 67,651-pointer native method table at RVA 0x01603F00 and matched method-index entries against direct-call instructions in the x64 native code (image base 0x180000000).

| Method | Native RVA | Verified behaviour |
|---|---:|---|
| PlayerMenu.UpdateMissions() | 0x005DC7F0 | Finds the *lorequest-content* DOM element, calls set_innerHTML, constructs ExtUI.RequestMissions, serialises JSON, submits PlayerCharacter.DataRequest. **Not the full inventory Missions list renderer.** |
| PlayerMenu.GetMissionIcon(Mission) | 0x005D6840 | Called in the longer HTML-building function; handles mission icon representation. |
| PlayerMenu.doUpdateInventory() | 0x005DFEB0 | Contains multiple GetMissionIcon calls, HTML string-building operations, and the **actual Missions container write**. |
| PowerUI.Document.getElementById(string) | 0x00A96A10 | Called immediately before writing the completed Missions HTML. |
| PowerUI.Element.set_innerHTML(string) | 0x00BA9740 | Called to assign finished HTML to the target node. |
| PlayerMenu.updateMissionTab() | 0x00601C20 | Updates tab label/state, not the mission icon grid. |

### Exact Missions list insertion, independently verified

In PlayerMenu.doUpdateInventory(), the direct native call at RVA 0x005E6BBD invokes PowerUI.Document.getElementById using an IL2CPP string-literal reference at virtual address 0x181CE3E28. A later call at RVA 0x005E6BD4 invokes PowerUI.Element.set_innerHTML, passing the assembled string (held in the RBX register at that instruction).

The pointer reference 0x181CE3E28 maps through the native IL2CPP metadata usage array (array base 0x181C961D0, destination index 39819) to string literal index **11171: missions-container** in global-metadata.dat. This proves the specific DOM target—not merely the method names. The native setter is writing the generated Missions HTML into the game's existing native scroll container.

In contrast, the identifier resolved inside UpdateMissions() is **lorequest-content** (metadata usage destination 39809, literal 11209). This confirms that prior research treating UpdateMissions as the main mission-list renderer was incorrect.

Native method pointers and these RVA offsets are specific to this exact generic binary. They are not stable across game versions and must not be used blindly with the FourOfSpades modified client.

## Consequences for QuestHelper

This is the first verified **pre-render HTML string boundary** for the actual Missions list. The game uses its original PowerUI HTML templates for vendor-mission entries, data-missionid markers and native mission hover/click attributes. The existing verified mission catalogue (152 unambiguous IDs) can potentially group the generated mission snippets before the original PowerUI set_innerHTML operation processes them.

Do not reparent rendered DOM objects: v0.3.4's unsupported insertBefore + incorrect rollback doubled mission icons (56 to 112 to 224 to 448) and froze the game.

Safer implementation steps:
1. Create a **pure offline string transformer** that parses only native mission markup, groups by known Region then Task, retains each original mission tile unchanged, and reproduces the original template's styling conventions. Test against synthetic and, if privately available, locally captured markup. Reject unsupported or unbalanced input.
2. Compare mission ID **multisets** and count before/after. Every mission must occur exactly once, with original click/hover handlers unchanged. Unknown IDs must remain visible.
3. Verify Harmony/IL2CPP managed prefix can safely intercept the appropriate PowerUI HTML setter for the generic client **only when target element id is missions-container**. The setter is used extensively elsewhere, so indiscriminate global HTML interception is unsafe. A default-off feature flag, once-per-native-update guard and reentrancy protection are mandatory.
4. Test an opt-in read-only capture of *structure/counts only*, then test the pure transformer offline. Do not publish a mutating build until the safety constraints and rollback semantics are validated.
5. Support FourOfSpades separately with its newer HtmlDocument/HtmlElement interop; this generic binary does not establish that client's native call sites.

**Current mod state is unchanged:** QuestHelper v0.3.5 safe mode, NativeMissionEditingSuspended=true, no MissionMenuOrganizer component attached. Simply inspecting the DLL does not authorise re-enabling the old implementation.

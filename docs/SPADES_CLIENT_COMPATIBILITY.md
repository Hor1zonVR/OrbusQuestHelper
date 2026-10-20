# Spades latest version — report verified, v0.4.2 read-only hook test

User's `QuestHelper-Spades-Compat.txt` for the latest **My Orbus Renewed** RebornManager instance reports:
- `OrbusQuestHelper 0.4.1` installed, exactly one DLL.
- `EnableHtmlRegionTaskGrouping=false`, so previous run did **not** even attempt the organiser.
- Detected `PowerUI.HtmlDocument` / `PowerUI.HtmlElement` API, rather than generic `PowerUI.Document` / `PowerUI.Element`.
- A legacy `PlayerMenu` type was **not found** in the client's two primary generated interop assemblies.
- The modern HTML string setter exists on **`Dom.Element.set_innerHTML(string)`**, inherited by `PowerUI.HtmlElement`; the document also has its own `PowerUI.HtmlDocument.set_innerHTML(string)`. The native game's *actual call* still needs an in-game test.
- Modern `Dom.EventTarget.addEventListener` is available, although the correct delegate overload for view switching requires live verification.

**v0.4.2 source supports a modern, opt-in diagnostic path.** It chooses `Dom.Element.set_innerHTML` when the generic PowerUI class is missing and the modern HtmlDocument exists. It records only a few mission-like setter observations, with element type, ID, length and count of original mission markers. When `CaptureMissionHtmlOnce=true`, it saves one **private** original Spades mission-like HTML file. A fallback reference to the HTML element's `ownerDocument` assists future view switching because no `PlayerMenu.document` was found.

**Important safety configuration:** On a modern Spades client, `Missions.SpadesMode=Observe` is the default and **cannot rewrite the UI**. Only the explicitly requested `SpadesMode=Transform` opts into applying the same strict transformer. Do not select Transform before checking the actual HTML capture. The generic client's working code still uses its original setter hook and existing main grouping flag.

To test on the user's instance, first publish/build v0.4.2 via their usual release.ps1 if needed, then install in RebornManager. With the game closed, set the **Spades instance's** `BepInEx/config/com.horizon.orbus.questhelper.cfg`:

```ini
[Missions]
EnableRegionTaskGrouping = false
EnableHtmlRegionTaskGrouping = true
GroupingView = Region
SpadesMode = Observe

[Investigation]
CaptureMissionHtmlOnce = true
```

The main grouping flag must be **true** to register the modern read-only hook; Observe is an independent safety gate. Launch Spades, open Inventory → Missions and wait ~15 seconds, then close. Share:
- `BepInEx/LogOutput.log` from **My Orbus Renewed**, or just the `[QuestHelper]` lines.
- If created, `BepInEx/config/OrbusQuestHelper/Diagnostics/*_SpadesMissionsOriginal.html` **privately**, never in the public repo.

Interpretation:
- Hook-install log but no original HTML observations: Spades may write to `HtmlDocument.set_innerHTML` or another rendering path; investigate further instead of assuming the DOM setter is sufficient.
- Logs with `id='missions-container'` and original `vendor-mission` markers: exact native element and content boundary identified; validate captured HTML offline before Transform.
- Logs with different ID/shape: adjust the exact target/transformer *only* after confirming how to preserve every original mission entry.
- Hook patch failure: log exception and maintain vanilla UI. Never re-enable DOM reparenting.

**Verification:** Pure transformer 18/18 regression checks and both generic/Spades managed hook smoke tests passed. Those do not prove that the real Spades native game routes its mission HTML through the new observed setter.

---

# QuestHelper on FourOfSpades' OrbusVR client: compatibility investigation

Status: 2026-10-20. **Game-side Spades support not yet implemented.** Do not treat the generic client's native HTML setter hook as automatically compatible with Spades' build.

## First confirmed reason it currently does nothing

The generic client uses `PowerUI.Document` and `PowerUI.Element` in `Assembly-CSharp.dll`. The previously obtained FourOfSpades IL2CPP interop shows `PowerUI.HtmlDocument` and `PowerUI.HtmlElement` (under `Assembly-CSharp-firstpass.dll`).

`MissionHtmlPatch.InstallIfEnabled` currently refuses to install unless `PowerUI.Element` **and** `PowerUI.Document` exist. Therefore on a build with only the Spades types it **never installs the Missions HTML Harmony prefix**, regardless of `EnableHtmlRegionTaskGrouping=true`.

The existing `PowerUiRuntime` reflection adapter recognises both names in principle, but this does not overcome the organiser's explicit early exit. Its generic-only setter target is `PowerUI.Element.set_innerHTML(string)`. A modern `HtmlElement` may require a different method/signature, document access, and/or event binding implementation. The exact native rendering path remains unverified in the *latest* Spades client.

## Read-only test on the user's existing RebornManager Spades instance

From the OrbusQuestHelper source folder, in PowerShell:

```powershell
git pull origin main
.\probe-spades.ps1
```

If only one RebornManager folder name matches `spade` or `four`, the script picks it; otherwise it lists installed instances and asks the user to choose. You can specify `-GameDir "path"` directly.

The `tools/QuestClientProbe` standalone .NET 8 app reads **ECMA-335 metadata**, not game code execution, from:

- `BepInEx/interop/Assembly-CSharp.dll`
- `BepInEx/interop/Assembly-CSharp-firstpass.dll`

It reports names and signatures of PowerUI document/element methods, selected `PlayerMenu` methods and document accessors, the QuestHelper DLL present, the current settings, and only the last relevant QuestHelper log lines. It saves **`QuestHelper-Spades-Compat.txt`** locally (ignored by Git). No game DLL/interop binaries or user-specific logs are committed to the public GitHub repository.

Send this diagnostic text to the assistant privately. If the existing Spades build logs show a different PowerUI API or no `PlayerMenu` `document` getter, inspect the matching active interop further before trying hooks.

## Next compatibility implementation (pending evidence)

1. Confirm which PowerUI types and actual setter methods are available in the latest Spades interop. Do not assume `set_innerHTML` is identical.
2. If a safe pre-render string setter exists, test a narrowly scoped Harmony prefix for that **specific modern type**. Keep generic client hook intact. Do not globally rewrite unrelated UI markup.
3. Read/capture Spades' original Missions HTML **only** if the app exposes a matching populated `missions-container`. Its HTML/template might be different; validate the original icon multiset and event handlers with the pure offline transformer.
4. Keep the native Region/Activity display + hover handlers and one-time view selector. Adapt event binding only to supported modern Dom event APIs.
5. Build separately against that Spades instance's current interop. Test in game. Report clearly whether this version works on generic, Spades, or both.

**Never revert to the old v0.3.4 native DOM reparenting/appendChild/insertBefore implementation**, which previously duplicated mission entries repeatedly and froze the game.

The metadata inspector successfully compiled and smoke-tested in GitHub Actions; this is **not** an in-game compatibility fix or published v0.4.2 yet.

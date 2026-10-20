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

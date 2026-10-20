# Orbus UI Inspector (offline, read-only)

This tool uses **.NET 8's built-in ECMA-335 metadata reader** to inspect `BepInEx/interop` for the game's actual class names and method signatures. It optionally examines a small whitelist of Missions-related string literal fragments in the local `global-metadata.dat`.

**It does not launch Orbus, invoke game methods, hook PowerUI, edit the UI, change any game files, contact GitHub, or publish any output.** Unlike the broken v0.3.x menu experiment, this script cannot duplicate mission entries: it has no access to the running game.

## From your OrbusQuestHelper PowerShell folder

First sync this inspector source (do **not** run `release.ps1`):

```powershell
git pull origin main
```

To inspect the currently configured RebornManager game:

```powershell
dotnet run --project .\tools\OrbusUiInspector -- --game "$env:LOCALAPPDATA\OrbusRebornModdingInstances\Modded Orbus"
```

To inspect your generic/MODTEST installation:

```powershell
dotnet run --project .\tools\OrbusUiInspector -- --game "$env:USERPROFILE\Downloads\OrbusVR-Reborn-Community-PC-MODTEST\OrbusVR-Reborn-Community-PC" --out ".\QuestUiInvestigation-Generic.txt"
```

The default output is `QuestUiInvestigation.txt` in the directory where the command was run. You can choose a different path using `--out`. The output is a text report suitable for sending privately in chat. It is **not** automatically uploaded or committed.

You can also supply explicit DLL paths rather than `--game`, and add `--metadata "C:\path\to\global-metadata.dat"` if the tool can't find it.

## What it does and doesn't prove

The report identifies exact methods including `PlayerMenu.UpdateMissions()`, `PlayerMenu.updateMissionTab()`, `PowerUI.Element.set_innerHTML(string)`, the modern `PowerUI.HtmlDocument.set_innerHTML(string)`, native DOM creation and scrolling APIs, and which assembly actually contains them.

**This isn't a decompiler of native Unity game logic.** BepInEx-generated interop DLLs contain wrapper signatures, not the native source for `UpdateMissions()`. To prove whether that method writes through `set_innerHTML`, we need a narrowly scoped read-only runtime observation or a separate authorised native-code investigation. The tool will not invent that relationship.

See `docs/POWERUI_MISSIONS_RENDERING_INVESTIGATION.md` for current findings and the proposed safer rendering architecture. QuestHelper's **v0.3.5 safety interlock stays enabled** throughout this work. No new plugin release is required just to use the inspector.

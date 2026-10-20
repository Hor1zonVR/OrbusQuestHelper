# QuestHelper v0.4.0: reversible experimental pre-render Missions grouping

**Experimental, not yet compiled on the user's Windows install or tested in VR.** Not published to the stable RebornManager channel. The source contains an opt-in Harmony prefix for the **generic/legacy PowerUI client** only.

## What we've verified

- User's v0.3.6 one-shot capture saved 14,477 characters of original HTML and 49 original mission icons.
- Original markup nests a padded DIV, DIV.vendor and DIV.vendor-reputation, with a native illustrated new-missions notice and 49 original mission tiles.
- The user ran the production offline transformer directly against the private captured file, and it returned `PASS: Lossless grouped HTML prepared`, `Original icons: 49; Output icons: 49`, `Matched region: 37; Matched task: 49`.
- The user supplied the grouped local HTML preview; inspecting it found exactly 49 distinct data-missionid entries, 45 `mission-none`, two `mission-bronze`, two `mission-silver`, eight expected region headings including Unassigned, and one intact native availability notice.
- GitHub Actions compiled/tested the pure transformer; **14/14** regression tests passed, including HTML-write target scoping and byte-for-byte native tile preservation.

## What's new and explicitly unverified

- `src/OrbusQuestHelper/Missions/MissionHtmlPatch.cs` installs a Harmony prefix for **PowerUI.Element.set_innerHTML(string)** when `Missions.EnableHtmlRegionTaskGrouping=true`.
- Only the **exact `missions-container` element** whose markup contains native mission tile markers passes the pure transform gate. The native call site has been proven by generic GameAssembly.dll research.
- The prefix mutates **only its incoming string argument** to replace the source markup before PowerUI renders it; no additional setter call, node reparenting, appendChild, rollback or Unity scene interaction is involved.
- Unknown or unexpected native HTML is returned unchanged. On a failed parsing/validation step, the feature suspends itself for the remainder of that game session.
- Previous unsafe `MissionMenuOrganizer` is **never registered** and `NativeMissionEditingSuspended=true` remains in source. The old `EnableRegionTaskGrouping` config is intentionally ignored.
- The hook is not installed at all unless the user explicitly opts in with a new config setting. On clients with modern HtmlDocument/HtmlElement (FourOfSpades), no hook is installed.
- **Unproven:** whether Harmony in this installed IL2CPP build can patch this native setter, whether the predicate can obtain `element.id` inside the prefix, and how PowerUI lays out multiple native `<p>` region/task headings within the existing parchment.

## Local-only test, with backup and restore

Use your actual installed/tested game directory. The v0.3.6 capture came from the user's `Camera Testing` BepInEx installation.

Close Orbus; in your OrbusQuestHelper repository PowerShell:

```powershell
git pull origin main
$game = "$env:USERPROFILE\OneDrive\Desktop\Camera Testing"
.\test-local-html.ps1 -GameDir $game -EnableExperiment
```

The script validates the BepInEx game folder and exactly one installed QuestHelper DLL, compiles v0.4.0 against that game's **own interop**, copies a dated backup to `BepInEx/QuestHelperBackups` (not BepInEx/plugins), inserts or enables `Missions.EnableHtmlRegionTaskGrouping=true`, and replaces the one installed QuestHelper DLL. **It does not run release.ps1 or publish a GitHub release.**

Start Orbus and open **Inventory → Missions**. Check whether native icons appear inside the parchment and whether both headings and hover tooltips remain correct. Send a screenshot/video and the `[QuestHelper]` lines from `BepInEx/LogOutput.log`.

Expected if hook works: log lines `HTML organiser HOOK INSTALLED`, `Native Missions innerHTML setter intercepted`, and `Native HTML transformed before render: icons=...`.

Expected if hook can't patch the game: `HTML hook could not install` or no `intercepted` log, and **vanilla UI unchanged**. A successfully installed Harmony hook is not proof that native calls execute its managed prefix.

**To restore immediately after closing Orbus:**

```powershell
$game = "$env:USERPROFILE\OneDrive\Desktop\Camera Testing"
.\test-local-html.ps1 -GameDir $game -Restore
```

The script disables the new config flag and copies the most recent backup DLL back to its original plugins location. Keep the backup until gameplay validation is complete. If the game freezes or grows its scroll panel, close it immediately and restore before further play.

**Do not publish stable v0.4.0 or enable this globally yet.** This is a controlled local first test of an unverified interop interception technique. The synthetic/offline checks cannot verify VR geometry or native Harmony hooking.

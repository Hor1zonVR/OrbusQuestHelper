## Real generic-client HTML capture: verified 2026-10-20

The user supplied a private one-shot original HTML capture plus the BepInEx log from v0.3.6. The game confirmed **49 mission icons in 14,477 characters**; the log confirms legacy Document/Element PowerUI, successful read-only snapshot, and no UI rewriting.

**Actual DOM structure:**
- Outer DIV has the original 20px side padding and 10px top padding.
- A nested DIV.vendor wraps DIV.vendor-reputation; **all 49 mission icons are direct children of vendor-reputation**.
- Two P nodes precede the mission tiles: one informational 'New Missions Available in Highsteppe' notice with an IMG; one legacy 'Your Level Missions:' section heading.
- 49 tiles use original inventory-box-lowres, data-boxtype-playermission, vendor-mission, and data-missionid classes plus mission-none/bronze/silver styles.
- All 49 entries have original onmouseover and onmouseout handlers; none in this capture has onmousedown. Do not invent click handlers.
- There are 49 distinct mission IDs. Four mission entries carry bronze/silver status.
- The existing MissionCatalog maps all 49 to activities/tasks; 37 to an explicitly recorded region. 12 must remain under Unassigned Region.

**Transformer correction:**
- Select exactly one live list wrapper that directly contains mission tiles; allow vendor-reputation (real generic client) as well as vendor (earlier tests).
- Preserve all original outer wrappers and padding verbatim.
- Preserve the informational notice, image and all original attributes verbatim, exactly once; replace only known legacy Weekly/Level headings.
- Copy every native mission tile verbatim, including icon paths, event attributes and status classes. Unknown regions remain visible in fallback groups.
- Refuse unrecognised wrapper contents, malformed markup or missing/duplicate/changed native icons.

**Automated verification:** GitHub Actions compiled the pure transformer and passed **11/11 regression tests**, including a synthetic 49-tile fixture modelled on the real wrapper and informational notice. The private original HTML, player data and full BepInEx log were NOT committed.

**Remaining work:** These tests validate a synthetic fixture matching the captured structure, not execution of the exact private HTML through the .NET transformer. Run the offline lab on that private capture to produce a local preview. Then develop a separately verified, default-off interception of the generic PowerUI HTML setter. Do not revive any native DOM reparenting. QuestHelper v0.3.6 remains in safe mode and no gameplay UI transformation is enabled.

---
# v0.3.6 — HTML-first Missions implementation (safe capture, offline transformer)

**Status as of 2026-10-20:** Pure Region → Task HTML transformer built; hosted GitHub Actions .NET 8 compilation and **8/8 regression checks passed** (workflow run 39356996073). Native game integration is **not enabled**. The v0.3.5 safety lock remains active in v0.3.6 and the broken MissionMenuOrganizer Unity component is not registered.

## What changed

**Transformer** — \`src/OrbusQuestHelper/Missions/MissionHtmlTransformer.cs\`

- Processes an original HTML string, not PowerUI/Unity objects. Never reparents live DOM nodes, calls set_innerHTML, or alters quest state.
- Extracts entire native \`<div class='... vendor-mission data-missionid-...'>...<img ...></div>\` entries as verbatim substrings, including every original onmousedown/onmouseover/onmouseout callback.
- Reorganises those exact original HTML snippets into **Region → Task**, using the existing 152 unambiguous ID mappings in \`MissionCatalog.cs\`. Unknown regions and tasks remain visible under clearly labelled fallback headings. Weekly is not a separate top-level category.
- Uses the game's known native \`<p style='color: #FFFFFF; font-size: 20px;margin-bottom:5px'>\` heading conventions and outer \`<div class='vendor'>\` wrapper. No custom font, blue bar, extra canvas, or new click handlers.
- Rejects unknown root/wrapper structure, unbalanced HTML, unexpected direct children, mission icons without IDs, interactive original headings, oversized markup, and repeated transformation.
- Checks the **full multiset of every original mission HTML tile** against the result. No lost, altered or duplicated entry is acceptable; otherwise the input is returned unchanged.
- The parser is intentionally conservative. It may reject the *real* generated game HTML until we see a full original sample. A refusal is preferable to repeating v0.3.4's runaway node duplication.

**Standalone offline lab** — \`tools/MissionHtmlLab/\`

- Runs on .NET 8 without Unity, game files, BepInEx, or internet dependencies.
- \`dotnet run --project .\\tools\\MissionHtmlLab -- --self-test\` runs eight safety checks.
- \`dotnet run --project .\\tools\\MissionHtmlLab -- --input .\\MissionsOriginal.html --output .\\MissionsGroupedPreview.html\` generates a local preview only when the input passes all guards. A rejected input is left unchanged; no output preview is written.
- GitHub Actions \`mission-html-checks.yml\` compiles/runs the tests on changes to transformer/catalog/lab. Workflow run 39356996073 succeeded with 8/8 checks.

**One-shot original HTML capture** — \`src/OrbusQuestHelper/Missions/MissionHtmlCapture.cs\`

- v0.3.6 registers **only a small read-only capture component** (never the old organiser).
- Reads only \`PlayerMenu.document.getElementById("missions-container").innerHTML\`, every 2.5 seconds until a populated, stable mission list has been seen on three consecutive checks.
- Saves **one** original HTML file to \`BepInEx/config/OrbusQuestHelper/Diagnostics/YYYYMMDD_HHMMSS_MissionsOriginal.html\` in the active game instance.
- Automatically sets the \`[Investigation]\` \`CaptureMissionHtmlOnce\` setting to false after saving, preventing repeated dumps on later launches.
- The captured markup is local only. **Do not upload it to GitHub**. It could contain in-game/user-specific content: share privately in chat only if comfortable. No game files, mission state, rewards or UI nodes are modified.

## How to make the one read-only capture (generic game)

1. Close the game; from your OrbusQuestHelper repository run \`git pull origin main\` and \`.\\release.ps1\` to publish v0.3.6 from your machine (the assistant cannot compile against your private BepInEx game interop).
2. Install/update through RebornManager, launch the **generic community client** and open Inventory → Missions. Wait about ten seconds without changing tabs. The UI should look **exactly as it normally does**—there are no region headings yet.
3. Exit Orbus. In the **game instance** BepInEx config folder, locate \`OrbusQuestHelper\\Diagnostics\\*_MissionsOriginal.html\`. Send the file here privately. If it doesn't exist, send only the \`[QuestHelper]\` lines from \`BepInEx\\LogOutput.log\` instead.
4. The next step is to run the **exact captured markup through the offline lab**, adjust the strict parser only where the native HTML actually requires it, and validate that all native mission entries are present exactly once. Only then design a narrowly scoped PowerUI hook for the generic game.
5. FourOfSpades client support must be independently checked against its **modern HtmlElement API**. Native RVA offsets from the generic client's GameAssembly.dll are not interchangeable.

## Scope and limitations

- The working grouping previously detected 56 UI icon elements and mapped 48 regions and 53 activities, but its *presentation code* was unsafe. This new implementation does **not** reuse that presentation mechanism.
- The synthetic tests prove losslessness for tested markup shapes, **not** full compatibility with unseen native markup.
- The exact prefix/wrapper format from the actual game is still needed. There is no enabled gameplay UI change yet, and no claim that the full sorted Missions page is finished.
- PowerUI \`set_innerHTML\` hooking is **not implemented** in v0.3.6. We will not install a speculative global setter hook while lacking the actual rendered source shape or a safe caller/target guard.

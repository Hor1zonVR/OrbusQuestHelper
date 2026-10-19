# QuestHelper v0.2.1 — fix BepInEx IL2CPP startup TypeLoadException

The user tested v0.2.0 on 2026-10-19. BepInEx logged:
- Loading [OrbusQuestHelper 0.2.0]
- System.TypeLoadException: Could not load type 'PowerUI.Element' from assembly 'Assembly-CSharp'
- Stack trace: ClassInjector.IsFieldEligible while registering MissionMenuOrganizer.

Cause and scope: v0.2.0 had a PowerUI.Element field directly on the injected MonoBehaviour, and BepInEx reflects its fields at startup. The generated Assembly-CSharp.dll previously uploaded from the separate MODTEST installation definitely contained the PowerUI.Element TypeDef, suggesting a live interop-assembly mismatch or a type-resolution problem when IL2CPP registers the class. The failure occurred BEFORE navigation UI code ran.

v0.2.1 moves the full PowerUI engine (which holds Element and delegate fields) into a **plain managed class**. The only injected MonoBehaviour holds a float timing field and calls the engine. This avoids the failing field-resolution path in ClassInjector. The plugin also logs the actual runtime Assembly-CSharp path and whether PowerUI.Document, PowerUI.Element, PowerUI.OnDomEvent and PlayerMenu can be resolved.

This should get the plugin through the original startup blocker, but the real UI event / layout code is not yet runtime verified. The user's RebornManager instance may have older/mismatched generated interop; if runtime type checks show missing types, regenerate matching interop in the active installation and compile against it rather than copying assemblies blindly between installations.

## Test
1. Exit Orbus; in local QuestHelper clone run git pull origin main, then .\release.ps1.
2. Update QuestHelper v0.2.1 in RebornManager.
3. Launch and open Inventory > Missions. The bar should appear if PowerUI types and mission DOM selectors resolve.
4. If absent, send the beginning (~90 lines) of BepInEx/LogOutput.log and all [QuestHelper] lines. This lets us distinguish component loading from missing runtime PowerUI types or wrong DOM identifiers.

UI changes are optional via EnableQuickNavigation in config. Existing missions, clicks and turn-in actions remain untouched.

## Active-instance interop mismatch found in v0.2.1 log (2026-10-19)

BepInEx loaded the plugin, but the running RebornManager instance's `Assembly-CSharp.dll` reported `PlayerMenu` present and `PowerUI.Document`, `PowerUI.Element`, `PowerUI.OnDomEvent` MISSING. The engine immediately raised `TypeLoadException` for `PowerUI.OnDomEvent`. The separate MODTEST `Assembly-CSharp.dll` included all three PowerUI types as actual TypeDefs, as directly confirmed by parsing the privately provided generated interop assembly metadata. The build script had still been defaulting to MODTEST.

`release.ps1` has now been corrected to prefer the RebornManager installation (`%LOCALAPPDATA%\OrbusRebornModdingInstances\Modded Orbus`) and prints SHA256 fingerprints of that assembly and MODTEST for comparison. Bare `dotnet build` uses the same new default. **This is a build-configuration-only correction; QuestHelper v0.2.1 DLL on GitHub is unchanged and remains unable to inject the UI until the missing types in the active game instance have been investigated.**

Next: retrieve the active instance's `BepInEx\interop\Assembly-CSharp.dll` privately (preferably zipped) to compare its TypeDef tables with the original MODTEST archive. Do not blindly copy MODTEST interop files into the active instance; BepInEx-generated assemblies should match the active game's native binary and global-metadata. No new release is required for this diagnostic step.

# OrbusQuestHelper developer guidelines

- Windows PC OrbusVR Reborn Community Edition; Unity 2018.4.30; BepInEx IL2CPP.
- Mod assembly: OrbusQuestHelper.dll. BepInEx ID: com.horizon.orbus.questhelper.
- Diagnostics first: do not change Missions or Journal UI before reviewing real diagnostics and screenshots.
- Preserve XR rig, controller interactions, player movement, network flow, quest progress and server data.
- This is client-side quality-of-life work: never spoof progress, rewards or completion.
- Keep independent from RebornCam and avoid using its existing function-key bindings.
- Prefer real IL2CPP metadata from reports over guessing class or method names.
- Diagnostic logs are local; no automatic uploads. Avoid credentials or input-field collection.
- Do not commit game assets, user diagnostics, or proprietary/reference DLLs.
- Keep community spreadsheets and fishing notes outside public source until provenance and redistribution are reviewed.
- Publish matching OrbusQuestHelper.dll assets only for versioned releases; stable only after in-game testing.
- Deliver complete contents of changed C# files when asked, not partial replacements.

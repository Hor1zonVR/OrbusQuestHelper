# Releasing QuestHelper

GitHub Actions runs on Windows and needs private BepInEx/IL2CPP reference assemblies. Never include proprietary game assets in this public repository.

## One-time Actions settings
Repository Settings > Secrets and variables > Actions:

- Variable QUESTHELPER_BUILD_REFS_REPO: owner/repo of the private reference bundle repository already used by RebornCam.
- Secret QUESTHELPER_BUILD_REFS_TOKEN: an access token scoped to read that private repository's Release asset; do not share it in chat.

The private repository should have tag refs-v1 and release asset RebornCamBuildRefs.zip containing the BepInEx/core and BepInEx/interop reference structure.

## Publish
Run Actions > Publish QuestHelper, choose preview. It compiles the real DLL and creates a prerelease tagged using the version in Plugin.cs. Preview releases do not appear as latest stable for RebornManager.

After in-game testing the *same commit*, run workflow again selecting stable to promote the prerelease. Bump the plugin version before preparing subsequent updates.

The entry is already published in OrbusRebornManager/catalog/mods.json. It will appear in RM, but cannot be installed until a stable GitHub Release supplies OrbusQuestHelper.dll. The first build is diagnostics-only and must be tested before being treated as a working QuestHelper release.

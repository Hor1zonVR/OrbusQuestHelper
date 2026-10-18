# Releasing OrbusQuestHelper

## Preferred: local PowerShell publisher (same as RebornCam)

Install .NET SDK 8, Git for Windows and GitHub CLI. Sign in once using:

    gh auth login

The modded Orbus installation must have BepInEx and generated IL2CPP interop DLLs. Clone the repo, commit any changes, and from the repo root run:

    git pull origin main
    .\release.ps1

Or choose the game folder for build references:

    .\release.ps1 -GameDir "C:\path\to\your\OrbusVR-Reborn-Community-PC"

The script reads the version from src/OrbusQuestHelper/Plugin.cs, checks for duplicate tags, automatically selects the detected MODTEST folder for compilation (or uses -GameDir), builds OrbusQuestHelper.dll, pushes main, and creates a **stable GitHub Release** with that DLL. There are no interactive prompts. Running release.ps1 intentionally publishes a public stable release offered to all RebornManager users.

For a preview release not visible to RebornManager:

    .\release.ps1 -Preview

**Increment the BepInPlugin version and commit before every new release.** Releasing another DLL with the same tag is blocked to keep version tracking reliable.

QuestHelper's catalogue entry is already live in OrbusRebornManager/catalog/mods.json. A published stable release containing a uniquely matching OrbusQuestHelper.dll is required before installation through RM can work.

## Optional: GitHub Actions workflow

The alternative GitHub Actions build requires private BepInEx/IL2CPP build references. The local publisher above does not.

Repository Settings > Secrets and variables > Actions:

- Variable QUESTHELPER_BUILD_REFS_REPO: private owner/repo containing build-reference asset.
- Secret QUESTHELPER_BUILD_REFS_TOKEN: narrowly scoped Contents read-only token for that repo (never share in chat).

The private repo must have a release tagged refs-v1 containing RebornCamBuildRefs.zip with the BepInEx/core and BepInEx/interop DLLs used for compilation.

Then run Actions > Publish QuestHelper, selecting preview. Test it, then promote to stable from the same commit. Avoid creating a release locally and via Actions for the same version.

## Testing and safety

The first QuestHelper version is diagnostics-only and hasn't been validated on the current Community Edition. Consider testing locally or publishing a preview before releasing stable to RebornManager. Review diagnostics before sharing; they can contain visible game labels. Never commit generated interop libraries, proprietary game assets or private community data.

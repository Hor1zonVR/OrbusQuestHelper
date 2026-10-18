# OrbusQuestHelper - local build + GitHub Release publisher.
# Mirrors the OrbusRebornCamera release.ps1 workflow.
# Build on this PC using generated BepInEx IL2CPP references, push main,
# then upload OrbusQuestHelper.dll to a GitHub Release used by RebornManager.
#
# Usage:
#   .\release.ps1
#   .\release.ps1 -GameDir "C:\path\to\modded\OrbusVR-Reborn-Community-PC"
#   .\release.ps1 -Preview  # optional GitHub prerelease (ignored by RM)

param(
    [string]$GameDir = "",
    [switch]$Preview
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repo = "Hor1zonVR/OrbusQuestHelper"
$root = $PSScriptRoot
$project = Join-Path $root "src\OrbusQuestHelper\OrbusQuestHelper.csproj"
$pluginFile = Join-Path $root "src\OrbusQuestHelper\Plugin.cs"
$dll = Join-Path $root "src\OrbusQuestHelper\bin\Release\net6.0\OrbusQuestHelper.dll"

Set-Location $root

function Check-Result {
    param([string]$Step)
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed (exit code $LASTEXITCODE). No release was published."
    }
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "      OrbusQuestHelper Release Tool" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

foreach ($tool in @("git", "dotnet", "gh")) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "Missing required tool: $tool"
    }
}

& gh auth status
Check-Result "GitHub authentication"

if (-not (Test-Path $project) -or -not (Test-Path $pluginFile)) {
    throw "Run this script from the cloned OrbusQuestHelper repository."
}

$branch = (& git branch --show-current).Trim()
Check-Result "Reading branch"
if ($branch -ne "main") { throw "Switch to the main branch before publishing." }

$origin = (& git remote get-url origin).Trim()
Check-Result "Reading origin"
if ($origin -notmatch '[/\:]Hor1zonVR/OrbusQuestHelper(\.git)?$') {
    throw "The origin remote is not $repo. Refusing to publish to the wrong repository."
}

$changes = & git status --porcelain
Check-Result "Checking working tree"
if ($changes) {
    throw "Uncommitted changes found. Commit your source changes before running release.ps1."
}

$source = Get-Content $pluginFile -Raw
$pattern = '(?s)\[BepInPlugin\(\s*"[^"]+"\s*,\s*"[^"]+"\s*,\s*"(?<version>\d+\.\d+\.\d+)"'
$match = [regex]::Match($source, $pattern)
if (-not $match.Success) { throw "Could not find plugin version in Plugin.cs." }

$version = $match.Groups["version"].Value
$tag = "v$version"

& git fetch origin main --tags
Check-Result "Fetching remote tags"

$remoteTag = & git ls-remote --tags origin "refs/tags/$tag"
Check-Result "Checking remote tag"
$localTag = & git tag --list $tag
Check-Result "Checking local tag"
if ($remoteTag -or $localTag) {
    throw "$tag already exists. Increment the version in Plugin.cs before publishing again."
}

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $default = Join-Path $env:USERPROFILE "Downloads\OrbusVR-Reborn-Community-PC-MODTEST\OrbusVR-Reborn-Community-PC"
    if (Test-Path (Join-Path $default "BepInEx\interop\Il2Cppmscorlib.dll")) {
        Write-Host "Default modded Orbus install found:" -ForegroundColor Yellow
        Write-Host $default
        $entered = (Read-Host "Press Enter to use it, or paste your RebornManager instance game folder").Trim().Trim('"')
        $GameDir = if ($entered) { $entered } else { $default }
    } else {
        $GameDir = (Read-Host "Paste your modded Orbus game folder (with BepInEx)").Trim().Trim('"')
    }
}

if ([string]::IsNullOrWhiteSpace($GameDir)) { throw "A game folder is required." }
$GameDir = [System.IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables($GameDir.Trim().Trim('"'))
)

foreach ($relative in @(
    "BepInEx\core\BepInEx.Core.dll",
    "BepInEx\core\BepInEx.Unity.IL2CPP.dll",
    "BepInEx\core\Il2CppInterop.Runtime.dll",
    "BepInEx\interop\Il2Cppmscorlib.dll",
    "BepInEx\interop\UnityEngine.CoreModule.dll",
    "BepInEx\interop\UnityEngine.TextRenderingModule.dll"
)) {
    if (-not (Test-Path (Join-Path $GameDir $relative))) {
        throw "Required BepInEx build reference missing: $relative in $GameDir"
    }
}

Write-Host "Version: $tag" -ForegroundColor Green
Write-Host "Game references: $GameDir"
Write-Host ""

Write-Host "[1/4] Building QuestHelper DLL on your PC..." -ForegroundColor Yellow
& dotnet build $project -c Release "--no-incremental" "-p:GameDir=$GameDir"
Check-Result "QuestHelper build"
if (-not (Test-Path $dll)) { throw "Build succeeded, but OrbusQuestHelper.dll was not found." }

Write-Host "[2/4] Preparing GitHub stable release..." -ForegroundColor Yellow
if ($Preview) {
    Write-Host "PREVIEW: RebornManager will ignore this prerelease." -ForegroundColor Yellow
} else {
    Write-Host "STABLE: RebornManager will offer this version to EVERY QuestHelper user." -ForegroundColor Yellow
    $confirm = Read-Host "Type PUBLISH to upload $tag as a stable release"
    if ($confirm -cne "PUBLISH") {
        Write-Host "Cancelled. Built DLL kept at $dll" -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "[3/4] Pushing committed source to GitHub..." -ForegroundColor Yellow
& git push origin main
Check-Result "Pushing main"
$commit = (& git rev-parse HEAD).Trim()
Check-Result "Reading commit"

Write-Host "[4/4] Creating GitHub Release $tag..." -ForegroundColor Yellow
$args = @(
    "release", "create", $tag, $dll,
    "--repo", $repo,
    "--target", $commit,
    "--title", "OrbusQuestHelper $tag",
    "--notes", "OrbusQuestHelper $tag. Early versions are experimental quest and journal diagnostics; mission grouping, tracking, fishing and event helpers are planned."
)
if ($Preview) { $args += "--prerelease" }
& gh @args
Check-Result "GitHub Release creation"

$json = & gh release view $tag --repo $repo --json tagName,isPrerelease,assets,url
Check-Result "Verifying GitHub Release"
$release = $json | ConvertFrom-Json
if ($release.tagName -ne $tag -or
    -not @($release.assets | Where-Object { $_.name -eq "OrbusQuestHelper.dll" }).Count) {
    throw "Release was created, but the expected DLL could not be verified."
}

Write-Host ""
Write-Host "RELEASE PUBLISHED: $tag" -ForegroundColor Green
Write-Host "DLL: OrbusQuestHelper.dll"
Write-Host "URL: $($release.url)" -ForegroundColor Cyan
if ($Preview) {
    Write-Host "This is a preview, so RebornManager will NOT install it." -ForegroundColor Yellow
} else {
    Write-Host "Close Orbus and refresh/check mods in RebornManager to install or update." -ForegroundColor Green
}

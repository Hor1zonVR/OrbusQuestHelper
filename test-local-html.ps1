# Local, opt-in v0.4.0 HTML organiser test. Does NOT publish a release.
# Usage: .\test-local-html.ps1 -GameDir "C:\path\to\Orbus" -EnableExperiment
# Undo:  .\test-local-html.ps1 -GameDir "C:\path\to\Orbus" -Restore
param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [switch]$EnableExperiment,
    [switch]$Restore
)

$ErrorActionPreference = 'Stop'
if ($EnableExperiment -eq $Restore) {
    throw 'Choose exactly one: -EnableExperiment OR -Restore.'
}

$GameDir = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($GameDir))
$core = Join-Path $GameDir 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'
$pluginDir = Join-Path $GameDir 'BepInEx\plugins'
$configPath = Join-Path $GameDir 'BepInEx\config\com.horizon.orbus.questhelper.cfg'
$backupDir = Join-Path $GameDir 'BepInEx\QuestHelperBackups'

if (!(Test-Path -LiteralPath $core -PathType Leaf) -or
    !(Test-Path -LiteralPath $pluginDir -PathType Container)) {
    throw "Not a valid installed Orbus/BepInEx game folder: $GameDir"
}
if (Get-Process -Name vrclient -ErrorAction SilentlyContinue) {
    throw 'Orbus is running. Close the game fully before installing/restoring a plugin.'
}

$existing = @(Get-ChildItem -LiteralPath $pluginDir -Recurse -File -Filter 'OrbusQuestHelper.dll')
if ($existing.Count -ne 1) {
    throw "Expected exactly one installed QuestHelper DLL; found $($existing.Count). Do not install a duplicate plugin."
}
$destination = $existing[0].FullName

function Set-Feature([bool]$value) {
    if (!(Test-Path -LiteralPath $configPath -PathType Leaf)) {
        throw "Missing QuestHelper BepInEx config: $configPath. Launch the current plugin once before testing."
    }

    $original = Get-Content -LiteralPath $configPath -Raw
    $desired = if ($value) { 'true' } else { 'false' }
    $pattern = '(?m)^EnableHtmlRegionTaskGrouping\s*=\s*(?:true|false)\s*$'
    if ([regex]::IsMatch($original, $pattern,
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $updated = [regex]::Replace(
            $original, $pattern, "EnableHtmlRegionTaskGrouping = $desired",
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    } else {
        # v0.3.6 won't yet have this key. Add it under its existing Missions
        # section (never append a second Missions section).
        $match = [regex]::Match($original, '(?m)^\[Missions\]\s*\r?\n')
        if (!$match.Success) {
            throw 'Missing [Missions] section in QuestHelper config; cannot enable experiment safely.'
        }
        $at = $match.Index + $match.Length
        $updated = $original.Insert($at, "EnableHtmlRegionTaskGrouping = $desired`r`n")
    }
    if ($updated -ne $original) {
        Set-Content -LiteralPath $configPath -Value $updated -Encoding UTF8
    }
    Write-Host "Config: EnableHtmlRegionTaskGrouping=$desired" -ForegroundColor Cyan
}

if ($Restore) {
    if (!(Test-Path -LiteralPath $backupDir -PathType Container)) {
        throw "No backup directory found: $backupDir"
    }
    $backup = Get-ChildItem -LiteralPath $backupDir -File -Filter 'OrbusQuestHelper_*.dll' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $backup) { throw 'No QuestHelper backup DLL exists.' }
    Set-Feature $false
    Copy-Item -LiteralPath $backup.FullName -Destination $destination -Force
    Write-Host "RESTORED: $($backup.FullName)" -ForegroundColor Green
    Write-Host 'QuestHelper experimental HTML grouping is OFF.'
    return
}

# Install only with explicit -EnableExperiment. Never push to the RM
# stable channel without gameplay validation.
$project = Join-Path $PSScriptRoot 'src\OrbusQuestHelper\OrbusQuestHelper.csproj'
$buildDll = Join-Path $PSScriptRoot 'src\OrbusQuestHelper\bin\Release\net6.0\OrbusQuestHelper.dll'
if (!(Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "QuestHelper project missing: $project"
}
Write-Host 'Building experimental v0.4.0 against THIS game instance...' -ForegroundColor Yellow
& dotnet build $project -c Release --no-incremental "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $buildDll -PathType Leaf)) {
    throw 'Build failed; no existing game plugin was changed.'
}

New-Item -Path $backupDir -ItemType Directory -Force | Out-Null
$backupPath = Join-Path $backupDir ("OrbusQuestHelper_" +
    (Get-Date -Format 'yyyyMMdd_HHmmss') + ".dll")
Copy-Item -LiteralPath $destination -Destination $backupPath
Write-Host "Backed up existing DLL: $backupPath" -ForegroundColor Cyan

try {
    Set-Feature $true
    Copy-Item -LiteralPath $buildDll -Destination $destination -Force
} catch {
    Copy-Item -LiteralPath $backupPath -Destination $destination -Force
    Write-Warning 'Installation error: previous DLL restored automatically.'
    throw
}
Write-Host 'LOCAL EXPERIMENT INSTALLED (nothing published to GitHub Releases).' -ForegroundColor Green
Write-Host "Game: $GameDir"
Write-Host 'Start Orbus, open Inventory > Missions, then send the result and LogOutput.log.'
Write-Host 'If the UI misbehaves, close Orbus and run the same script with -Restore.'

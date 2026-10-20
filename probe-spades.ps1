# Read-only FourOfSpades compatibility inspection.
# Run in the QuestHelper repository: .\probe-spades.ps1
# Optional: .\probe-spades.ps1 -GameDir "C:\full\path\to\Spades instance"
# Never patches QuestHelper, starts the game, or changes RebornManager instances.
param([string]$GameDir = "")

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'tools\QuestClientProbe\QuestClientProbe.csproj'
if (!(Test-Path -LiteralPath $project)) {
    throw 'QuestClientProbe project is missing. Run git pull origin main first.'
}

if (-not $GameDir) {
    $root = Join-Path $env:LOCALAPPDATA 'OrbusRebornModdingInstances'
    if (!(Test-Path -LiteralPath $root)) {
        throw "RebornManager instance directory not found: $root. Rerun with -GameDir and the actual instance path."
    }

    $all = @(Get-ChildItem -LiteralPath $root -Directory |
        Where-Object {
            Test-Path (Join-Path $_.FullName 'BepInEx\interop\Assembly-CSharp.dll')
        } | Sort-Object Name)

    if ($all.Count -eq 0) {
        throw 'No generated BepInEx interop assemblies found in RebornManager instances. Start the Spades instance once, or rerun with -GameDir.'
    }

    $matching = @($all | Where-Object { $_.Name -match 'spade|four' })
    if ($matching.Count -eq 1) {
        $GameDir = $matching[0].FullName
        Write-Host "Automatically selected Spades instance: $GameDir"
    }
    else {
        Write-Host 'Choose the RebornManager instance running the LATEST Spades client:'
        for ($i = 0; $i -lt $all.Count; $i++) {
            Write-Host ("  [{0}] {1}" -f ($i + 1), $all[$i].Name)
        }
        $choice = Read-Host 'Type the corresponding number'
        $selected = 0
        if (![int]::TryParse($choice, [ref]$selected) -or
            $selected -lt 1 -or $selected -gt $all.Count) {
            throw 'Invalid instance selection. No files changed.'
        }
        $GameDir = $all[$selected - 1].FullName
    }
}

$GameDir = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($GameDir))
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\interop\Assembly-CSharp.dll'))) {
    throw "Instance not found or its BepInEx interop is missing: $GameDir"
}

$output = Join-Path $PSScriptRoot 'QuestHelper-Spades-Compat.txt'
Write-Host "Inspecting $GameDir" -ForegroundColor Cyan
Write-Host 'Read-only metadata scan; there will be no changes to Orbus or BepInEx.'
& dotnet run --project $project -c Release -- --game $GameDir |
    Tee-Object -FilePath $output

if ($LASTEXITCODE -ne 0) {
    throw "Compatibility inspector failed (exit $LASTEXITCODE). Please send the printed error."
}
Write-Host ""
Write-Host "COMPLETE: $output" -ForegroundColor Green
Write-Host 'Upload QuestHelper-Spades-Compat.txt privately in this chat.'

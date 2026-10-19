# Read-only local OrbusVR UI investigation bundle.
# No game modifications, no network calls, and no GitHub uploads.
# Usage: .\tools\Collect-OrbusUiEvidence.ps1
#        .\tools\Collect-OrbusUiEvidence.ps1 -GameDir "C:\path\to\OrbusVR-Reborn-Community-PC"
#        .\tools\Collect-OrbusUiEvidence.ps1 -OutputPath "C:\Users\me\Desktop\orbus-ui-evidence.zip"

[CmdletBinding()]
param(
    [string]$GameDir = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $GameDir = Join-Path $env:USERPROFILE "Downloads\OrbusVR-Reborn-Community-PC-MODTEST\OrbusVR-Reborn-Community-PC"
}
$GameDir = [System.IO.Path]::GetFullPath($GameDir)
if (-not (Test-Path -LiteralPath $GameDir -PathType Container)) {
    throw "Game folder not found: $GameDir. Use -GameDir with your RebornManager instance game folder."
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "OrbusQuestHelper_UiEvidence.zip"
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$interop = Join-Path $GameDir "BepInEx\interop"
if (-not (Test-Path -LiteralPath $interop -PathType Container)) {
    throw "Missing BepInEx\interop. Launch your modded PC game once or use the correct -GameDir."
}
$assemblyCSharp = Join-Path $interop "Assembly-CSharp.dll"
if (-not (Test-Path -LiteralPath $assemblyCSharp -PathType Leaf)) {
    throw "Missing generated Assembly-CSharp.dll at $assemblyCSharp. Check your BepInEx interop folder."
}

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("OrbusUiEvidence_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
$inventory = New-Object System.Collections.Generic.List[string]
$included = New-Object System.Collections.Generic.List[string]

function Add-SafeFile {
    param(
        [Parameter(Mandatory=$true)][string]$Source,
        [Parameter(Mandatory=$true)][string]$ArchiveRelativePath,
        [long]$MaxBytes = 100MB
    )
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { return }
    $file = Get-Item -LiteralPath $Source
    if ($file.Length -gt $MaxBytes) {
        $inventory.Add("SKIPPED OVERSIZE: $ArchiveRelativePath ($($file.Length) bytes)")
        return
    }
    $dest = Join-Path $stage $ArchiveRelativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $dest -Force
    $included.Add("$ArchiveRelativePath ($($file.Length) bytes)")
}

try {
    Write-Host "Collecting read-only Orbus UI evidence from your local game..." -ForegroundColor Cyan
    $inventory.Add("OrbusQuestHelper UI static investigation - $(Get-Date -Format s)")
    $inventory.Add("Paths in this archive are relative; no user folders, configs, saves or log files are included.")
    $inventory.Add("Generated interop DLLs are signatures/stubs, not original game source implementations.")
    $inventory.Add("")

    $interops = @(Get-ChildItem -LiteralPath $interop -Filter "*.dll" -File)
    $inventory.Add("=== GENERATED INTEROP DLL NAMES ===")
    foreach ($file in ($interops | Sort-Object Name)) {
        $inventory.Add(("{0} | {1} bytes" -f $file.Name, $file.Length))
    }
    $inventory.Add("")

    # The most useful generated type metadata for MagicalActual mission classes
    # and any PowerUI/HTML/CSS renderer interfaces.
    Add-SafeFile -Source $assemblyCSharp -ArchiveRelativePath "interop\Assembly-CSharp.dll" -MaxBytes 100MB
    foreach ($dll in $interops) {
        if ($dll.Name -match '^(PowerUI|Nitro|Blaze|Loonim|Assembly-CSharp-firstpass)(.*)\.dll$') {
            Add-SafeFile -Source $dll.FullName -ArchiveRelativePath ("interop\" + $dll.Name) -MaxBytes 100MB
        }
    }

    $dataDirectories = @(Get-ChildItem -LiteralPath $GameDir -Directory -Filter "*_Data")
    $inventory.Add("=== GAME DATA ASSET / RESOURCE FILE INDEX ===")
    foreach ($data in $dataDirectories) {
        $inventory.Add("Data directory: " + $data.Name)
        $meta = Join-Path $data.FullName "il2cpp_data\Metadata\global-metadata.dat"
        Add-SafeFile -Source $meta -ArchiveRelativePath "metadata\global-metadata.dat" -MaxBytes 100MB

        $assetCount = 0
        $looseCount = 0
        $files = @(Get-ChildItem -LiteralPath $data.FullName -Recurse -File -ErrorAction SilentlyContinue)
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($data.FullName.Length).TrimStart('\')
            $extension = $file.Extension.ToLowerInvariant()
            $isAsset = $extension -in @('.assets','.ress','.bundle','.unity3d','.resource','.html','.css','.js','.bytes') -or
                $file.Name -match '(?i)powerui|mission|journal|quest|inventory|menu'
            if ($isAsset -and $assetCount -lt 10000) {
                $inventory.Add(("{0}\{1} | {2} bytes" -f $data.Name, $relative, $file.Length))
                $assetCount++
            }

            # Copy only small standalone UI-style resources, never huge .assets
            # or game data bundles; those can be selected for extraction later.
            $isLooseWebFile = $extension -in @('.html','.css','.js') -or
                ($extension -eq '.bytes' -and $file.Name -match '(?i)html|css|menu|mission|quest|ui')
            if ($isLooseWebFile -and $file.Length -le 2MB -and $looseCount -lt 75) {
                $safeName = ($data.Name + "_" + $relative) -replace '[\\/:*?"<>|]', '_'
                Add-SafeFile -Source $file.FullName -ArchiveRelativePath ("loose-ui\" + $safeName) -MaxBytes 2MB
                $looseCount++
            }
        }
        $inventory.Add("Indexed relevant asset names: " + $assetCount)
        $inventory.Add("Included small loose UI resources: " + $looseCount)
    }

    if ($dataDirectories.Count -eq 0) {
        $inventory.Add("WARNING: No *_Data folder found; no metadata or Unity assets were indexed.")
    }
    $inventory.Add("")
    $inventory.Add("=== FILES INCLUDED IN ARCHIVE ===")
    foreach ($item in $included) { $inventory.Add($item) }

    [System.IO.File]::WriteAllLines((Join-Path $stage "file-inventory.txt"),
        $inventory.ToArray(), [System.Text.Encoding]::UTF8)
    $outDirectory = Split-Path -Parent $OutputPath
    if (-not (Test-Path -LiteralPath $outDirectory)) {
        New-Item -ItemType Directory -Force -Path $outDirectory | Out-Null
    }
    if (Test-Path -LiteralPath $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Force
    }
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $OutputPath -CompressionLevel Optimal
    $zip = Get-Item -LiteralPath $OutputPath
    Write-Host "Created: $($zip.FullName)" -ForegroundColor Green
    Write-Host ("Archive size: {0:N2} MB" -f ($zip.Length / 1MB))
    Write-Host "Upload this ZIP privately in chat, NOT to a public GitHub repo."
    Write-Host "It contains generated game assemblies and game metadata; review contents before sharing."
}
finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}

#Requires -Version 5
[CmdletBinding()]
param(
    [switch]$Zip,
    [switch]$Dist,
    [switch]$Run,
    [switch]$NoRelaunch,
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot      = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectCsproj = Join-Path $repoRoot "src\GetHub.csproj"
$distDir       = Join-Path $repoRoot "GetHub_Dist"
$dataDir       = Join-Path $distDir "data"
$exePath       = Join-Path $distDir "GetHub.exe"
$dotnet        = "C:\Program Files\dotnet\dotnet.exe"
$version       = (Get-Content (Join-Path $repoRoot "VERSION")).Trim()
$tempDataBackup = Join-Path $env:TEMP "GetHub_data_backup_$([Guid]::NewGuid().ToString('N'))"

if (-not (Test-Path $dotnet))        { throw ".NET SDK not found at $dotnet" }
if (-not (Test-Path $projectCsproj)) { throw "Project not found: $projectCsproj" }

# If GetHub is running, close it (will optionally relaunch after build)
$wasRunning = $false
$running = Get-Process -Name GetHub -ErrorAction SilentlyContinue
if ($running) {
    $wasRunning = $true
    Write-Host "[stop] closing running GetHub.exe..." -ForegroundColor Yellow
    $running | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    # Give it up to 5s to exit gracefully, then kill
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline -and (Get-Process -Name GetHub -ErrorAction SilentlyContinue)) {
        Start-Sleep -Milliseconds 200
    }
    $stillUp = Get-Process -Name GetHub -ErrorAction SilentlyContinue
    if ($stillUp) {
        Write-Host "[stop] force-killing GetHub.exe (didn't exit gracefully)" -ForegroundColor DarkYellow
        $stillUp | Stop-Process -Force
        Start-Sleep -Milliseconds 300
    }
}

# --- preserve user data across rebuilds ---
$hadData = Test-Path $dataDir
if ($hadData) {
    Write-Host "[preserve] backing up existing data/ folder" -ForegroundColor DarkGray
    Move-Item -LiteralPath $dataDir -Destination $tempDataBackup
}

# wipe and rebuild dist
if (Test-Path $distDir) {
    Write-Host "[clean] removing $distDir" -ForegroundColor Yellow
    Remove-Item -Recurse -Force $distDir
}

Write-Host "[build] GetHub v$version ($Runtime) -> GetHub_Dist" -ForegroundColor Cyan
try {
    & $dotnet publish $projectCsproj `
        -c Release `
        -r $Runtime `
        --self-contained `
        -p:DisableAOT=true `
        -p:PublishTrimmed=true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $distDir `
        --nologo `
        -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
}
catch {
    # Build failed: restore the data backup so user prefs aren't stranded in %TEMP%
    if ($hadData -and (Test-Path $tempDataBackup)) {
        Write-Host "[preserve] build failed - restoring data/ folder before exit" -ForegroundColor Yellow
        if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }
        Move-Item -LiteralPath $tempDataBackup -Destination $dataDir
    }
    throw
}

# strip pdbs
Get-ChildItem -Path $distDir -Filter *.pdb -Recurse | Remove-Item -Force

# --- restore or seed the data folder ---
if ($hadData) {
    Write-Host "[preserve] restoring data/ folder" -ForegroundColor Green
    Move-Item -LiteralPath $tempDataBackup -Destination $dataDir
}
else {
    # First build: seed from %APPDATA%\GetHub if present, else empty folder
    $appDataSrc = Join-Path $env:APPDATA "GetHub"
    if (Test-Path $appDataSrc) {
        Write-Host "[seed] copying $appDataSrc -> data/ (first build)" -ForegroundColor Green
        Copy-Item -Path $appDataSrc -Destination $dataDir -Recurse -Force
        # remove stale lock from copy
        $lock = Join-Path $dataDir "process.lock"
        if (Test-Path $lock) { Remove-Item -Force $lock }
    }
    else {
        New-Item -ItemType Directory -Path $dataDir | Out-Null
        Write-Host "[seed] created empty data/ (portable mode enabled)" -ForegroundColor DarkGray
    }
}

$size = "{0:N1} MB" -f ((Get-ChildItem $distDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)
Write-Host "[done] $distDir ($size)" -ForegroundColor Green
Write-Host "[portable] preferences live in $dataDir" -ForegroundColor DarkGray

# -Dist / -Zip: produce a CLEAN shareable zip with NO personal data.
# The dist staging contains only GetHub.exe + an empty data/ folder (portable
# mode). Your preference.json / avatars are NEVER included.
if ($Zip -or $Dist) {
    $zipPath = Join-Path $repoRoot "GetHub_${version}_${Runtime}.zip"
    $stageDir = Join-Path $env:TEMP "GetHub_dist_$([Guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path "$stageDir\GetHub" | Out-Null
        Copy-Item $exePath -Destination "$stageDir\GetHub\GetHub.exe"
        New-Item -ItemType Directory -Path "$stageDir\GetHub\data" | Out-Null
        Set-Content -Path "$stageDir\GetHub\data\.keep" `
            -Value "Portable mode: GetHub stores all settings in this folder."
        Set-Content -Path "$stageDir\GetHub\HOW-TO-OPEN.txt" -Encoding UTF8 -Value @"
GetHub for Windows
==================

If the app won't open ("Windows protected your PC", or nothing happens):
GetHub is not code-signed, so Windows SmartScreen blocks it. To run it:

OPTION 1 - Unblock the ZIP BEFORE extracting (recommended):
  1. Right-click the downloaded .zip -> Properties
  2. At the bottom, tick "Unblock" -> OK
  3. Now extract and run GetHub.exe

OPTION 2 - Allow it at the SmartScreen prompt:
  1. Double-click GetHub.exe
  2. On "Windows protected your PC", click "More info"
  3. Click "Run anyway"

Settings are stored in the "data" folder next to GetHub.exe (portable mode).
Requires Git for Windows: https://git-scm.com/download/win
"@

        if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
        Write-Host "[dist] building clean shareable zip (no personal data)" -ForegroundColor Cyan
        Compress-Archive -Path "$stageDir\GetHub" -DestinationPath $zipPath -CompressionLevel Optimal
    }
    finally {
        if (Test-Path $stageDir) { Remove-Item -Recurse -Force $stageDir }
    }
    $zipSize = "{0:N1} MB" -f ((Get-Item $zipPath).Length / 1MB)
    Write-Host "[dist] $zipPath ($zipSize)" -ForegroundColor Green
    Write-Host "[dist] contents: GetHub/GetHub.exe + empty GetHub/data/" -ForegroundColor DarkGray
}

$shouldLaunch = $Run -or ($wasRunning -and -not $NoRelaunch)
if ($shouldLaunch) {
    $reason = if ($Run) { "run" } else { "relaunch" }
    Write-Host "[$reason] launching GetHub.exe" -ForegroundColor Cyan
    Start-Process -FilePath $exePath
}

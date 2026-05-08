#Requires -Version 5
[CmdletBinding()]
param(
    [switch]$Zip,
    [switch]$Run,
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

# Refuse to build while GetHub is running (locks DLLs)
$running = Get-Process -Name GetHub -ErrorAction SilentlyContinue
if ($running) {
    $paths = $running | Select-Object -ExpandProperty Path -Unique
    throw "GetHub.exe is running ($($paths -join ', ')). Close it first."
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
& $dotnet publish $projectCsproj `
    -c Release `
    -r $Runtime `
    --self-contained `
    -p:DisableAOT=true `
    -p:DisableUpdateDetection=true `
    -p:PublishTrimmed=true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $distDir `
    --nologo `
    -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

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

if ($Zip) {
    $zipPath = Join-Path $repoRoot "GetHub_Dist_${version}_${Runtime}.zip"
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Write-Host "[zip] $zipPath" -ForegroundColor Cyan
    Compress-Archive -Path $distDir -DestinationPath $zipPath -CompressionLevel Optimal
    $zipSize = "{0:N1} MB" -f ((Get-Item $zipPath).Length / 1MB)
    Write-Host "[zip] done ($zipSize)" -ForegroundColor Green
}

if ($Run) {
    Write-Host "[run] launching GetHub.exe" -ForegroundColor Cyan
    Start-Process -FilePath $exePath
}

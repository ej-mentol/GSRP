# GSRP Release Publisher Script

Write-Host "--- Terminating active processes ---" -ForegroundColor Yellow
try { taskkill /F /IM GSRP.Daemon.exe /T /FI "STATUS eq RUNNING" | Out-Null } catch {}

Write-Host "--- Checking Environment ---" -ForegroundColor Yellow

# Check Node.js
try {
    $nodeVer = node -v
    Write-Host "Node.js detected: $nodeVer" -ForegroundColor Gray
} catch {
    Write-Error "Node.js not found! Please install Node.js v18 or newer."
    exit 1
}

# Check .NET SDK
try {
    $dotnetVer = dotnet --version
    Write-Host ".NET SDK detected: $dotnetVer" -ForegroundColor Gray
    if ($dotnetVer -notmatch "^(9|10)\.") {
        Write-Warning "Caution: .NET SDK is not version 9.x or 10.x. Build might fail."
    }
} catch {
    Write-Error ".NET SDK not found! Please install .NET 9 SDK."
    exit 1
}

Write-Host "--- Cleaning old builds ---" -ForegroundColor Yellow
cmd /c npm run clean

Write-Host "--- Starting GSRP Build Process ---" -ForegroundColor Cyan

# 1. Run the build
cmd /c yarn build
if ($LASTEXITCODE -ne 0) { 
    Write-Error "Yarn build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE 
}

# 2. Get version from package.json
if (!(Test-Path package.json)) { Write-Error "package.json not found!"; exit 1 }
$packageJson = Get-Content -Raw -Path package.json | ConvertFrom-Json
$version = $packageJson.version
$name = "GSRP_v$($version)-win-x64"

# 3. Prepare release directory
$releaseDir = "dist/release"
if (!(Test-Path $releaseDir)) { New-Item -ItemType Directory -Path $releaseDir | Out-Null }

# 4. Create Archive
$sourceDir = Join-Path (Get-Location) "dist/win-unpacked"
if (!(Test-Path $sourceDir)) { 
    Write-Error "Build directory $sourceDir not found! Did electron-builder succeed?"
    exit 1 
}

$destPath = Join-Path (Get-Location) "$releaseDir/$name.zip"
if (Test-Path $destPath) { Remove-Item $destPath -Force }

Write-Host "--- Cleaning debug symbols (.pdb) ---" -ForegroundColor Yellow
Get-ChildItem -Path $sourceDir -Filter *.pdb -Recurse | Remove-Item -Force

Write-Host "--- Cleaning unnecessary locales ---" -ForegroundColor Yellow
$localesDir = Join-Path $sourceDir "locales"
if (Test-Path $localesDir) {
    Get-ChildItem -Path $localesDir -Exclude "ru.pak", "en-US.pak" | Remove-Item -Force
}

Write-Host "--- Cleaning other unnecessary files ---" -ForegroundColor Yellow
$filesToRemove = @(
    "LICENSE.electron.txt",
    "LICENSES.chromium.html",
    "resources/bin/web.config"
)

foreach ($file in $filesToRemove) {
    $path = Join-Path $sourceDir $file
    if (Test-Path $path) { Remove-Item $path -Force }
}

Write-Host "--- Archiving build to $destPath ---" -ForegroundColor Green

# Use .NET System.IO.Compression for reliability (handles long paths and symlinks better)
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    [System.IO.Compression.ZipFile]::CreateFromDirectory($sourceDir, $destPath)
} catch {
    Write-Error "Archiving failed: $($_.Exception.Message)"
    exit 1
}

Write-Host "--- Done! Release ready in $releaseDir ---" -ForegroundColor Cyan

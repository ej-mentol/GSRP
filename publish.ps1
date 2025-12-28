# GSRP Release Publisher Script

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

Write-Host "--- Starting GSRP Build Process ---" -ForegroundColor Cyan

# 1. Run the build
yarn build
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed!"; exit $LASTEXITCODE }

# 2. Get version from package.json
$packageJson = Get-Content -Raw -Path package.json | ConvertFrom-Json
$version = $packageJson.version
$name = "GSRP_v$($version)-win-x64"

# 3. Prepare release directory
$releaseDir = "dist/release"
if (!(Test-Path $releaseDir)) { New-Item -ItemType Directory -Path $releaseDir }

# 4. Create Archive
$sourceDir = "dist/win-unpacked"
$destPath = Join-Path $releaseDir "$name.zip"

if (Test-Path $destPath) { Remove-Item $destPath }

Write-Host "--- Archiving build to $destPath ---" -ForegroundColor Green

# Use native PowerShell Compression (works everywhere)
Compress-Archive -Path "$sourceDir\*" -DestinationPath $destPath

Write-Host "--- Done! Release ready in $releaseDir ---" -ForegroundColor Cyan

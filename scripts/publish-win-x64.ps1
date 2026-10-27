# Velo Production Self-Contained x64 Release Packaging Script
# Compliant with Velo PRD v2.0 Section 1A & Section 13

param(
    [string]$Version = "1.0.0-preview.1"
)

$ErrorActionPreference = "Stop"
$rootDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishDir = Join-Path $rootDir "publish\win-x64"
$artifactsDir = Join-Path $rootDir "artifacts"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Packaging Velo win-x64 v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (!(Test-Path $artifactsDir)) { New-Item $artifactsDir -ItemType Directory -Force | Out-Null }

Write-Host "`n1. Publishing Windows 10 x64 Release Binary..." -ForegroundColor Yellow
dotnet publish (Join-Path $rootDir "src\Velo.Desktop\Velo.Desktop.csproj") `
    -c Release `
    --no-restore `
    -o $publishDir

$sourceIco = Join-Path $rootDir "src\Velo.Desktop\Assets\Velo.ico"
if (Test-Path $sourceIco) {
    Copy-Item $sourceIco -Destination (Join-Path $publishDir "Velo.ico") -Force
}

Write-Host "`n2. Creating Portable ZIP Package..." -ForegroundColor Yellow
$zipPath = Join-Path $artifactsDir "Velo-win10-x64-v$Version-portable.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "`n3. Computing SHA-256 Release Checksums..." -ForegroundColor Yellow
$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash
$checksumFile = Join-Path $artifactsDir "SHA256SUMS.txt"
"$hash  $(Split-Path $zipPath -Leaf)" | Out-File -FilePath $checksumFile -Encoding utf8

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host " Build & Packaging Successful!" -ForegroundColor Green
Write-Host " Artifact: $zipPath" -ForegroundColor Green
Write-Host " SHA-256:  $hash" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

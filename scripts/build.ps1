# Velo Build Script
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "Building Velo Solution [$Configuration]..." -ForegroundColor Cyan
dotnet build (Join-Path $PSScriptRoot "..\Velo.sln") -c $Configuration

Write-Host "Build Complete!" -ForegroundColor Green

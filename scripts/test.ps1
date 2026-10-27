# Velo Test Runner Script
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "Running Velo Test Suites [$Configuration]..." -ForegroundColor Cyan
dotnet test (Join-Path $PSScriptRoot "..\Velo.sln") -c $Configuration --logger "console;verbosity=normal"

Write-Host "All Tests Completed Successfully!" -ForegroundColor Green

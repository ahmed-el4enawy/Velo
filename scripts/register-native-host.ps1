<#
.SYNOPSIS
    Registers the Velo Native Messaging Host in Windows Registry for Chrome, Edge and Firefox (PRD Section 9).
#>

param(
    [string]$HostExePath = "$PSScriptRoot\..\src\Velo.NativeMessaging\bin\x64\Release\net10.0\Velo.NativeMessaging.exe"
)

$ErrorActionPreference = "Stop"

$fullHostPath = [System.IO.Path]::GetFullPath($HostExePath)
$targetDir = [System.IO.Path]::GetDirectoryName($fullHostPath)
$manifestPath = Join-Path $targetDir "com.velo.downloader.json"

Write-Host "Configuring Velo Native Messaging Host Manifest at: $manifestPath" -ForegroundColor Cyan

$manifest = @{
    name = "com.velo.downloader"
    description = "Velo High-Speed Download Acceleration Bridge"
    path = $fullHostPath
    type = "stdio"
    allowed_origins = @(
        "chrome-extension://*/"
    )
}

$manifestJson = $manifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($manifestPath, $manifestJson)

# Register for Chrome, Edge, and Firefox in HKCU (Per-user, zero admin required)
$registryTargets = @(
    "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.velo.downloader",
    "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.velo.downloader",
    "HKCU:\Software\Mozilla\NativeMessagingHosts\com.velo.downloader"
)

foreach ($regPath in $registryTargets) {
    if (-not (Test-Path $regPath)) {
        New-Item -Path $regPath -Force | Out-Null
    }
    Set-ItemProperty -Path $regPath -Name "(Default)" -Value $manifestPath
    Write-Host "Registered: $regPath -> $manifestPath" -ForegroundColor Green
}

Write-Host "Velo Native Messaging Host successfully registered!" -ForegroundColor Green

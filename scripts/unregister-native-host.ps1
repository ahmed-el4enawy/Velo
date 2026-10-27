<#
.SYNOPSIS
    Unregisters the Velo Native Messaging Host from Windows Registry for Chrome, Edge and Firefox.
#>

$ErrorActionPreference = "SilentlyContinue"

$registryTargets = @(
    "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.velo.downloader",
    "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.velo.downloader",
    "HKCU:\Software\Mozilla\NativeMessagingHosts\com.velo.downloader"
)

foreach ($regPath in $registryTargets) {
    if (Test-Path $regPath) {
        Remove-Item -Path $regPath -Force -Recurse
        Write-Host "Unregistered: $regPath" -ForegroundColor Yellow
    }
}

Write-Host "Velo Native Messaging Host successfully unregistered." -ForegroundColor Green

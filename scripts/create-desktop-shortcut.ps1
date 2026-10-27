<#
.SYNOPSIS
    Creates a Windows Desktop shortcut for Velo with custom application icon.
#>

$desktopPath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
$shortcutPath = [System.IO.Path]::Combine($desktopPath, "Velo.lnk")
$targetExe = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\publish\win-x64\Velo.exe")
$workingDir = [System.IO.Path]::GetDirectoryName($targetExe)
$iconPath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\publish\win-x64\Velo.ico")

$wshShell = New-Object -ComObject WScript.Shell
$shortcut = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetExe
$shortcut.WorkingDirectory = $workingDir
$shortcut.IconLocation = "$iconPath,0"
$shortcut.Description = "Velo - Next-Gen High-Speed Download Accelerator"
$shortcut.Save()

Write-Host "Velo desktop shortcut created successfully at: $shortcutPath" -ForegroundColor Green
Write-Host "Target executable: $targetExe" -ForegroundColor Cyan
Write-Host "Icon: $iconPath" -ForegroundColor Cyan

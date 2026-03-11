param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\CopilotTimeTracker"
)

$ErrorActionPreference = "Stop"

$exePath = Join-Path $InstallDir "TimeTracker.App.exe"
$existingProcess = Get-Process | Where-Object { $_.Path -eq $exePath }
foreach ($process in $existingProcess) {
    Stop-Process -Id $process.Id -Force
}

$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $runKeyPath -Name "CopilotTimeTracker" -ErrorAction SilentlyContinue

$uninstallKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CopilotTimeTracker"
Remove-Item -Path $uninstallKeyPath -Recurse -Force -ErrorAction SilentlyContinue

$shortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Copilot Time Tracker.lnk"
Remove-Item -Path $shortcutPath -Force -ErrorAction SilentlyContinue

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
}

Write-Host "Uninstalled Copilot Time Tracker"

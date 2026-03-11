param(
    [string]$SourcePath = "",
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\CopilotTimeTracker"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Join-Path $repoRoot "artifacts\publish\TimeTracker.App"
}

if (-not (Test-Path $SourcePath)) {
    throw "Publish output not found at $SourcePath. Run scripts\Publish-Release.ps1 first."
}

$exePath = Join-Path $InstallDir "TimeTracker.App.exe"
$existingProcess = Get-Process | Where-Object { $_.Path -eq $exePath }
foreach ($process in $existingProcess) {
    Stop-Process -Id $process.Id -Force
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $SourcePath "*") -Destination $InstallDir -Recurse -Force

$shell = New-Object -ComObject WScript.Shell
$shortcutDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $shortcutDir "Copilot Time Tracker.lnk"
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Save()

$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
New-Item -Path $runKeyPath -Force | Out-Null
Set-ItemProperty -Path $runKeyPath -Name "CopilotTimeTracker" -Value "`"$exePath`""

$uninstallKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CopilotTimeTracker"
New-Item -Path $uninstallKeyPath -Force | Out-Null
Set-ItemProperty -Path $uninstallKeyPath -Name "DisplayName" -Value "Copilot Time Tracker"
Set-ItemProperty -Path $uninstallKeyPath -Name "DisplayVersion" -Value "0.1.0"
Set-ItemProperty -Path $uninstallKeyPath -Name "Publisher" -Value "Copilot"
Set-ItemProperty -Path $uninstallKeyPath -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $uninstallKeyPath -Name "UninstallString" -Value "powershell -ExecutionPolicy Bypass -File `"$repoRoot\scripts\Uninstall-TimeTracker.ps1`""

Write-Host "Installed Copilot Time Tracker to $InstallDir"

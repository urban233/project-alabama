[CmdletBinding()]
param([switch]$Visible, [switch]$Rosewood)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskPlayer = Join-Path $taskRoot $(if ($Rosewood) { 'builds/nfs-world-rosewood/Alabama.exe' } else { 'builds/nfs-world-runtime/Alabama.exe' })
$taskReport = Join-Path $taskRoot 'artifacts/NfsWorld/player-runtime-lifetime.json'
$taskLog = Join-Path $taskRoot 'artifacts/NfsWorld/player-runtime-lifetime.log'
if (-not (Test-Path -LiteralPath $taskPlayer)) { throw 'Build the selected district runtime player first.' }
if (Test-Path -LiteralPath $taskReport) { Remove-Item -LiteralPath $taskReport }
if (-not ('DistrictLifetimePower' -as [type])) {
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class DistrictLifetimePower { [StructLayout(LayoutKind.Sequential)] public struct Status { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; } [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out Status status); }'
}
function Read-TaskPower {
    $taskStatus = New-Object DistrictLifetimePower+Status
    $taskKnown = [DistrictLifetimePower]::GetSystemPowerStatus([ref]$taskStatus)
    $taskScheme = & powercfg /getactivescheme
    [pscustomobject]@{ utc = [DateTime]::UtcNow.ToString('o'); statusKnown = $taskKnown;
        acLineStatus = [int]$taskStatus.ACLineStatus; batteryPercent = [int]$taskStatus.BatteryLifePercent;
        powerScheme = ($taskScheme -join ' ') }
}
$taskPowerBefore = Read-TaskPower
$taskArguments = @('-district-lifetime-probe', '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080', '-logFile', $taskLog)
$taskArguments = $taskArguments | ForEach-Object { '"' + $_ + '"' }
$taskWindowStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
$taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArguments -WindowStyle $taskWindowStyle -PassThru
if (-not $taskProcess.WaitForExit(600000)) {
    $taskProcess.Kill() # This opt-in test player contains no editor work.
    throw 'The district lifetime player probe timed out after ten minutes.'
}
$taskProcess.Refresh()
if (-not (Test-Path -LiteralPath $taskReport)) { throw "Player exited without lifetime evidence; read $taskLog." }
$taskData = Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
$taskData | Add-Member -NotePropertyName powerBefore -NotePropertyValue $taskPowerBefore
$taskData | Add-Member -NotePropertyName powerAfter -NotePropertyValue (Read-TaskPower)
$taskData | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $taskReport -Encoding utf8
if ($taskProcess.ExitCode -ne 0 -or -not $taskData.passed -or $taskData.cameraRenders -le 240) {
    throw "Rendered lifetime probe failed; read $taskReport and $taskLog."
}
$taskData.operations | Select-Object name,wallMilliseconds,meanFrameMilliseconds,maximumFrameMilliseconds
Write-Output "Rendered lifetime report: $taskReport"

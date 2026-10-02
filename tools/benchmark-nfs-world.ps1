[CmdletBinding()]
param([switch]$Visible, [switch]$Style, [switch]$Art, [switch]$Driving, [ValidateRange(0,240)][int]$FrameRateCap = 0,
      [switch]$DiagnosticNoShadows, [switch]$DiagnosticHardShadows,
      [ValidateRange(0,1)][float]$DiagnosticRenderScale = 0,
      [ValidateSet('Default','D3D11','D3D12')][string]$GraphicsApi = 'Default')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if ($Art -and $Style) { throw 'Choose either the art or style variant.' }
if ($Driving -and -not $Art) { throw 'The high-speed corridors are qualified against the local art scene.' }
if ($GraphicsApi -ne 'Default' -and $DiagnosticRenderScale -eq 0) { throw 'Select an explicit diagnostic render scale for graphics-API comparisons.' }
$taskPlayer = Join-Path $taskRoot $(if ($Art) { 'builds/nfs-world-art/Alabama.exe' } elseif ($Style) { 'builds/nfs-world-style/Alabama.exe' } else { 'builds/nfs-world/Alabama.exe' })
$taskOutput = Join-Path $taskRoot 'artifacts/NfsWorld'
if (-not (Test-Path -LiteralPath $taskPlayer)) { throw 'Build the local NFS World player first.' }
$taskName = if ($Art) { 'player-art-benchmark' } elseif ($Style) { 'player-style-benchmark' } else { 'player-view-benchmark' }
if ($DiagnosticNoShadows -or $DiagnosticHardShadows -or $DiagnosticRenderScale -gt 0) { $taskName = 'player-art-diagnostic-benchmark' }
if ($Driving) { $taskName += '-driving' }
if ($FrameRateCap -gt 0) { $taskName += "-cap$FrameRateCap" }
$taskLog = Join-Path $taskOutput ($taskName + '.log')
$taskReport = Join-Path $taskOutput ($taskName + '.json')
if (Test-Path -LiteralPath $taskReport) { Remove-Item -LiteralPath $taskReport }
# No batch mode: the report requires camera renders and nonzero GPU timings.
$taskArguments = @('-nfs-benchmark', '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080', '-logFile', $taskLog)
if ($Style) { $taskArguments += '-nfs-style-benchmark' }
if ($Art) { $taskArguments += '-nfs-art-benchmark' }
if ($Driving) { $taskArguments += '-nfs-driving-benchmark' }
if ($FrameRateCap -gt 0) { $taskArguments += @('-nfs-benchmark-cap', $FrameRateCap.ToString()) }
if ($DiagnosticNoShadows) { $taskArguments += '-nfs-diagnostic-no-shadows' }
if ($DiagnosticHardShadows) { $taskArguments += '-nfs-diagnostic-hard-shadows' }
if ($DiagnosticRenderScale -gt 0) { $taskArguments += @('-nfs-diagnostic-render-scale', $DiagnosticRenderScale.ToString([System.Globalization.CultureInfo]::InvariantCulture)) }
if ($GraphicsApi -ne 'Default') { $taskArguments += ('-force-' + $GraphicsApi.ToLowerInvariant()) }
$taskArguments = $taskArguments | ForEach-Object { '"' + $_ + '"' }
if (-not ('NfsBenchmarkPower' -as [type])) {
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class NfsBenchmarkPower { [StructLayout(LayoutKind.Sequential)] public struct Status { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; } [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out Status status); }'
}
function Read-TaskPower {
    $taskStatus = New-Object NfsBenchmarkPower+Status
    $taskKnown = [NfsBenchmarkPower]::GetSystemPowerStatus([ref]$taskStatus)
    $taskScheme = & powercfg /getactivescheme
    [pscustomobject]@{ utc = [DateTime]::UtcNow.ToString('o'); statusKnown = $taskKnown;
        acLineStatus = [int]$taskStatus.ACLineStatus; batteryPercent = [int]$taskStatus.BatteryLifePercent;
        powerScheme = ($taskScheme -join ' ') }
}
$taskPowerBefore = Read-TaskPower
$taskWindowStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
$taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArguments -WindowStyle $taskWindowStyle -PassThru
if (-not $taskProcess.WaitForExit(600000)) {
    $taskProcess.Kill()
    throw 'The map viewpoint benchmark timed out after ten minutes.'
}
$taskProcess.Refresh()
if ($taskProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $taskReport)) {
    throw "Rendered benchmark failed ($($taskProcess.ExitCode)); read $taskLog."
}
$taskData = Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
$taskData | Add-Member -NotePropertyName powerBefore -NotePropertyValue $taskPowerBefore
$taskData | Add-Member -NotePropertyName powerAfter -NotePropertyValue (Read-TaskPower)
$taskData | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $taskReport -Encoding utf8
if ($taskData.cameraRenders -lt 2400 -or @($taskData.views | Where-Object { $_.gpuSamples -le 0 }).Count -gt 0) {
    throw 'The run did not verify actual camera rendering and GPU measurements.'
}
$taskData.views | Select-Object name,meanFps,p95Milliseconds,gpuP95Milliseconds,gpuSamples
Write-Output "Rendered viewpoint report: $taskReport"

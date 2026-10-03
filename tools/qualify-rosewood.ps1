[CmdletBinding()]
param([ValidateSet('Seam','Route')][string]$Mode = 'Seam', [ValidateRange(0,240)][int]$FrameRateCap = 30, [switch]$Visible)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskOutput = Join-Path $taskRoot 'artifacts/NfsWorld/Rosewood'
$taskPlayer = Join-Path $taskRoot 'builds/nfs-world-rosewood/Alabama.exe'
if (-not (Test-Path -LiteralPath $taskPlayer)) { throw 'Build the combined Rosewood player first.' }
$taskName = if ($Mode -eq 'Seam') { 'player-seam' } elseif ($FrameRateCap -gt 0) { "player-route-cap$FrameRateCap" } else { 'player-route-uncapped' }
$taskReport = Join-Path $taskOutput ($taskName + '.json')
if (Test-Path -LiteralPath $taskReport) { Remove-Item -LiteralPath $taskReport }
$taskArguments = @($(if ($Mode -eq 'Seam') { '-nfs-rosewood-seam' } else { '-nfs-rosewood-route' }),
    '-nfs-rosewood-cap',$FrameRateCap.ToString(),'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080',
    '-logFile',(Join-Path $taskOutput ($taskName + '.log')))
$taskArguments = $taskArguments | ForEach-Object { '"' + $_ + '"' }
if (-not ('RosewoodPower' -as [type])) {
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class RosewoodPower { [StructLayout(LayoutKind.Sequential)] public struct Status { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; } [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out Status status); }'
}
function Read-TaskPower {
    $taskStatus = New-Object RosewoodPower+Status
    $taskKnown = [RosewoodPower]::GetSystemPowerStatus([ref]$taskStatus)
    $taskScheme = & powercfg /getactivescheme
    [pscustomobject]@{ utc = [DateTime]::UtcNow.ToString('o'); statusKnown = $taskKnown;
        acLineStatus = [int]$taskStatus.ACLineStatus; batteryPercent = [int]$taskStatus.BatteryLifePercent;
        powerScheme = ($taskScheme -join ' ') }
}
$taskBefore = Read-TaskPower
$taskStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
$taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArguments -WindowStyle $taskStyle -PassThru
if (-not $taskProcess.WaitForExit(1200000)) { $taskProcess.Kill(); throw 'The owned Rosewood qualification player exceeded twenty minutes.' }
$taskProcess.Refresh()
if (-not (Test-Path -LiteralPath $taskReport)) { throw 'Player produced no Rosewood qualification report.' }
$taskData = Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
$taskData | Add-Member -NotePropertyName powerBefore -NotePropertyValue $taskBefore
$taskData | Add-Member -NotePropertyName powerAfter -NotePropertyValue (Read-TaskPower)
$taskViews = Join-Path $taskOutput ($taskName + '-views')
New-Item -ItemType Directory -Path $taskViews -Force | Out-Null
$taskStartUtc = [DateTime]::Parse($taskBefore.utc).ToUniversalTime()
$taskCaptures = @(Get-ChildItem -LiteralPath $taskOutput -Filter '*.png' -File | Where-Object {
    $_.LastWriteTimeUtc -ge $taskStartUtc -and $_.Name -match '^(downtown-to-rosewood|rosewood-to-downtown)-'
})
foreach ($taskCapture in $taskCaptures) { Copy-Item -LiteralPath $taskCapture.FullName -Destination $taskViews }
$taskData | Add-Member -NotePropertyName captureDirectory -NotePropertyValue ($taskName + '-views')
$taskData | Add-Member -NotePropertyName captures -NotePropertyValue @($taskCaptures | ForEach-Object { $_.Name })
$taskData | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $taskReport -Encoding utf8
if ($taskProcess.ExitCode -ne 0 -or -not $taskData.passed -or $taskData.cameraRenders -lt 120 -or $taskData.gpuSamples -le 0) {
    throw "Rosewood rendered qualification failed: $($taskData.failure). Read $taskReport."
}
$taskData | Select-Object passed,seamCrossings,distanceMetres,durationSeconds,meanFps,p95Milliseconds,p99Milliseconds,gpuP95Milliseconds,peakAllocatedMemoryBytes
Write-Output "Rendered Rosewood report: $taskReport"

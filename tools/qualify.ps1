[CmdletBinding()]
param(
    [ValidateSet('Benchmark', 'Reliability')][string]$Mode = 'Benchmark',
    [ValidateRange(60, 7200)][int]$DurationSeconds = 1800,
    [ValidateSet(0, 30, 60, 144)][int]$Cap = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskPlayer = Join-Path $taskRoot 'builds/district-loop-release/Alabama.exe'
$taskDirectory = Join-Path $taskRoot 'artifacts/Qualification'
if ($Mode -eq 'Reliability' -and $Cap -ne 0) { throw '-Cap applies only to Benchmark mode.' }
$taskName = $Mode.ToLowerInvariant() + $(if ($Cap -gt 0) { "-cap$Cap" } else { '' })
$taskReport = Join-Path $taskDirectory ($taskName + '.json')
$taskLog = Join-Path $taskDirectory ($taskName + '.log')
if (-not (Test-Path -LiteralPath $taskPlayer -PathType Leaf)) {
    throw "Build the release player first with ./tools/unity.ps1 -Action LoopReleaseBuild."
}
New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
if (Test-Path -LiteralPath $taskReport) { Remove-Item -LiteralPath $taskReport }
$taskArguments = @(
    '-force-d3d11', '-screen-width', '1920', '-screen-height', '1080',
    '-screen-fullscreen', '0', '-logFile', ('"' + $taskLog + '"'),
    '-alabama-output', ('"' + $taskReport + '"'),
    $(if ($Mode -eq 'Benchmark') { '-alabama-benchmark' } else { '-alabama-reliability' }),
    '-alabama-duration-seconds', "$DurationSeconds", '-alabama-cap', "$Cap"
)
$taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArguments -WindowStyle Normal -PassThru
$taskTimeoutMs = if ($Mode -eq 'Benchmark') { 240000 } else { ($DurationSeconds + 90) * 1000 }
if (-not $taskProcess.WaitForExit($taskTimeoutMs)) {
    Stop-Process -Id $taskProcess.Id -Force
    throw "Player exceeded $($taskTimeoutMs / 1000) seconds. Read $taskLog."
}
$taskProcess.Refresh()
if (-not (Test-Path -LiteralPath $taskReport)) {
    throw "Player produced no report (exit $($taskProcess.ExitCode)). Read $taskLog."
}
$taskResult = Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
if ($taskProcess.ExitCode -ne 0 -or $taskResult.status -ne 'passed') {
    throw "Qualification $Mode failed (exit $($taskProcess.ExitCode), status $($taskResult.status)). Read $taskReport and $taskLog."
}
Write-Output "$Mode passed: $($taskResult.measuredFrames) frames, p95 $($taskResult.frameP95Ms) ms, $($taskResult.completedLaps) laps. Report: $taskReport"

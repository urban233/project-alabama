[CmdletBinding()]
param([switch]$Visible, [switch]$Style, [switch]$Art, [switch]$DiagnosticNoShadows, [switch]$DiagnosticHardShadows,
      [ValidateRange(0,1)][float]$DiagnosticRenderScale = 0)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if ($Art -and $Style) { throw 'Choose either the art or style variant.' }
$taskPlayer = Join-Path $taskRoot $(if ($Art) { 'builds/nfs-world-art/Alabama.exe' } elseif ($Style) { 'builds/nfs-world-style/Alabama.exe' } else { 'builds/nfs-world/Alabama.exe' })
$taskOutput = Join-Path $taskRoot 'artifacts/NfsWorld'
if (-not (Test-Path -LiteralPath $taskPlayer)) { throw 'Build the local NFS World player first.' }
$taskName = if ($Art) { 'player-art-benchmark' } elseif ($Style) { 'player-style-benchmark' } else { 'player-view-benchmark' }
if ($DiagnosticNoShadows -or $DiagnosticHardShadows -or $DiagnosticRenderScale -gt 0) { $taskName = 'player-art-diagnostic-benchmark' }
$taskLog = Join-Path $taskOutput ($taskName + '.log')
$taskReport = Join-Path $taskOutput ($taskName + '.json')
if (Test-Path -LiteralPath $taskReport) { Remove-Item -LiteralPath $taskReport }
# No batch mode: the report requires camera renders and nonzero GPU timings.
$taskArguments = @('-nfs-benchmark', '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080', '-logFile', $taskLog)
if ($Style) { $taskArguments += '-nfs-style-benchmark' }
if ($Art) { $taskArguments += '-nfs-art-benchmark' }
if ($DiagnosticNoShadows) { $taskArguments += '-nfs-diagnostic-no-shadows' }
if ($DiagnosticHardShadows) { $taskArguments += '-nfs-diagnostic-hard-shadows' }
if ($DiagnosticRenderScale -gt 0) { $taskArguments += @('-nfs-diagnostic-render-scale', $DiagnosticRenderScale.ToString([System.Globalization.CultureInfo]::InvariantCulture)) }
$taskArguments = $taskArguments | ForEach-Object { '"' + $_ + '"' }
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
if ($taskData.cameraRenders -lt 2400 -or @($taskData.views | Where-Object { $_.gpuSamples -le 0 }).Count -gt 0) {
    throw 'The run did not verify actual camera rendering and GPU measurements.'
}
$taskData.views | Select-Object name,meanFps,p95Milliseconds,gpuP95Milliseconds,gpuSamples
Write-Output "Rendered viewpoint report: $taskReport"

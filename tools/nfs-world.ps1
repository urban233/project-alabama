[CmdletBinding()]
param(
    [ValidateSet('SampleSetup', 'Setup', 'SampleCapture', 'Capture', 'SampleVerify', 'Verify', 'Build', 'DrivingTest', 'StylePreview', 'StyleCapture', 'LightingCapture', 'StyleOptimize', 'StyleVerify', 'StyleBuild', 'StyleAntialiasing', 'RestoreProjectAntialiasing', 'ArtOptimize', 'ArtVerify', 'ArtCapture', 'ArtBuild')]
    [string]$Action = 'Setup',
    [string]$EditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskProject = Join-Path $taskRoot 'unity'
if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) { throw 'Pinned Unity editor not found.' }
if ((Get-Item -LiteralPath $EditorPath).VersionInfo.ProductVersion -notmatch '^6000\.3\.25f1[_\s]') {
    throw 'Use the pinned Unity 6000.3.25f1 editor.'
}
$taskMethods = @{
    SampleSetup = 'Alabama.Editor.NfsWorldSetup.Sample'
    Setup = 'Alabama.Editor.NfsWorldSetup.District'
    SampleCapture = 'Alabama.Editor.NfsWorldCapture.Sample'
    Capture = 'Alabama.Editor.NfsWorldCapture.District'
    SampleVerify = 'Alabama.Editor.NfsWorldValidation.Sample'
    Verify = 'Alabama.Editor.NfsWorldValidation.District'
    Build = 'Alabama.Editor.NfsWorldValidation.Build'
    StylePreview = 'Alabama.Editor.NfsWorldStylePreview.Create'
    StyleCapture = 'Alabama.Editor.NfsWorldCapture.Style'
    LightingCapture = 'Alabama.Editor.NfsWorldCapture.LightingStudy'
    StyleOptimize = 'Alabama.Editor.NfsWorldVisualOptimization.Run'
    StyleAntialiasing = 'Alabama.Editor.NfsWorldVisualOptimization.EfficientAntialiasing'
    RestoreProjectAntialiasing = 'Alabama.Editor.NfsWorldVisualOptimization.RestoreProjectAntialiasing'
    StyleVerify = 'Alabama.Editor.NfsWorldValidation.Style'
    StyleBuild = 'Alabama.Editor.NfsWorldValidation.BuildStyle'
    ArtOptimize = 'Alabama.Editor.NfsWorldVisualOptimization.RunArt'
    ArtVerify = 'Alabama.Editor.NfsWorldValidation.Art'
    ArtCapture = 'Alabama.Editor.NfsWorldCapture.Art'
    ArtBuild = 'Alabama.Editor.NfsWorldValidation.BuildArt'
}
$taskOutput = Join-Path $taskRoot ('artifacts/NfsWorld/' + $Action)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskLog = Join-Path $taskOutput 'editor.log'
$taskArguments = @('-batchmode', '-projectPath', $taskProject, '-logFile', $taskLog,
                   '-executeMethod', $taskMethods[$Action])
if ($Action -eq 'DrivingTest') {
    $taskResults = Join-Path $taskOutput 'results.xml'
    if (Test-Path -LiteralPath $taskResults) { Remove-Item -LiteralPath $taskResults }
    $taskArguments = @('-batchmode', '-nographics', '-projectPath', $taskProject, '-logFile', $taskLog,
                      '-runTests', '-testPlatform', 'PlayMode', '-testFilter', 'Alabama.Tests.NfsWorldDrivingTests',
                      '-testResults', $taskResults)
} elseif ($Action -notin @('SampleCapture', 'Capture', 'StyleCapture', 'LightingCapture', 'ArtCapture')) { $taskArguments += @('-nographics', '-quit') }
$taskQuoted = $taskArguments | ForEach-Object { '"' + $_ + '"' }
$taskProcess = Start-Process -FilePath $EditorPath -ArgumentList $taskQuoted -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
$taskProcess.Refresh()
if ($taskProcess.ExitCode -ne 0) { throw "NFS World $Action failed ($($taskProcess.ExitCode)). Read $taskLog." }
if ($Action -eq 'DrivingTest') {
    [xml]$taskRun = Get-Content -LiteralPath $taskResults -Raw
    if ($taskRun.'test-run'.result -ne 'Passed' -or [int]$taskRun.'test-run'.passed -lt 1 -or [int]$taskRun.'test-run'.failed -gt 0) {
        throw "Converted-map driving test failed or did not run. Read $taskResults."
    }
}
Write-Output "NFS World $Action finished. Log: $taskLog"

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Open', 'Setup', 'Verify', 'EditTests', 'PlayTests', 'Build', 'VehicleSetup', 'VehicleVerify', 'VehicleCapture', 'VehicleBuild', 'StreetSetup', 'StreetVerify', 'StreetCapture', 'StreetBuild', 'HandlingSetup', 'HandlingVerify', 'HandlingBuild', 'LoopSetup', 'LoopVerify', 'LoopCapture', 'LoopBuild', 'LoopReleaseBuild')]
    [string]$Action,
    [string]$EditorPath = $env:UNITY_EDITOR_PATH
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskProjectPath = Join-Path $taskRoot 'unity'
$taskVersionFile = Join-Path $taskProjectPath 'ProjectSettings/ProjectVersion.txt'
$taskVersionLine = Get-Content -LiteralPath $taskVersionFile | Where-Object { $_ -match '^m_EditorVersion: ' }
$taskEditorVersion = ($taskVersionLine -split ': ', 2)[1]

if ([string]::IsNullOrWhiteSpace($EditorPath)) {
    $taskLocalToolchain = Join-Path $taskRoot 'local-toolchain.json'
    if (Test-Path -LiteralPath $taskLocalToolchain) {
        $EditorPath = (Get-Content -LiteralPath $taskLocalToolchain -Raw | ConvertFrom-Json).unityEditor
    } else {
        $EditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$taskEditorVersion/Editor/Unity.exe"
    }
}
if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
    throw "Unity $taskEditorVersion was not found. Supply -EditorPath or set UNITY_EDITOR_PATH."
}
$taskProductVersion = (Get-Item -LiteralPath $EditorPath).VersionInfo.ProductVersion
if ($taskProductVersion -notmatch ('^' + [regex]::Escape($taskEditorVersion) + '([_\s]|$)')) {
    throw "This project requires Unity $taskEditorVersion; the selected executable reports $taskProductVersion."
}

if ($Action -eq 'Open') {
    Start-Process -FilePath $EditorPath -ArgumentList @('-projectPath', ('"' + $taskProjectPath + '"')) -WindowStyle Normal
    Write-Output "Opening $taskProjectPath in Unity $taskEditorVersion."
    return
}

$taskArtifactPath = Join-Path $taskRoot "artifacts/$Action"
New-Item -ItemType Directory -Path $taskArtifactPath -Force | Out-Null
$taskLogPath = Join-Path $taskArtifactPath 'editor.log'
$taskArguments = @('-batchmode', '-nographics', '-projectPath', $taskProjectPath, '-logFile', $taskLogPath)
if ($Action -in @('VehicleCapture', 'StreetCapture', 'LoopCapture')) {
    $taskArguments = $taskArguments | Where-Object { $_ -ne '-nographics' }
}
$taskResultsPath = Join-Path $taskArtifactPath 'results.xml'

switch ($Action) {
    'VehicleBuild' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.ProjectBuild.BuildVehicleReview') }
    'VehicleSetup' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.VehicleAssetSetup.Setup') }
    'VehicleVerify' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.VehicleAssetSetup.Verify') }
    'VehicleCapture' { $taskArguments += @('-executeMethod', 'Alabama.Editor.VehicleCapture.Run') }
    'StreetSetup' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.IndustrialStreetScene.Setup') }
    'StreetVerify' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.IndustrialStreetVerify.Run') }
    'StreetCapture' { $taskArguments += @('-executeMethod', 'Alabama.Editor.IndustrialStreetCapture.Run') }
    'StreetBuild' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.ProjectBuild.BuildIndustrialStreet') }
    'HandlingSetup' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.HandlingCourseSetup.Run') }
    'HandlingVerify' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.HandlingCourseSetup.Verify') }
    'HandlingBuild' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.ProjectBuild.BuildHandlingCourse') }
    'LoopSetup' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.DistrictLoopScene.Setup') }
    'LoopVerify' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.DistrictLoopScene.Verify') }
    'LoopCapture' { $taskArguments += @('-executeMethod', 'Alabama.Editor.DistrictLoopCapture.Run') }
    'LoopBuild' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.ProjectBuild.BuildDistrictLoop') }
    'LoopReleaseBuild' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.ProjectBuild.BuildDistrictLoopRelease') }
    'Setup' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.ProjectFoundation.Setup') }
    'Verify' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.ProjectFoundation.Verify') }
    'Build' { $taskArguments += @('-quit', '-executeMethod', 'Alabama.Editor.ProjectBuild.BuildWindows') }
    'EditTests' { $taskArguments += @('-runTests', '-testPlatform', 'EditMode', '-testResults', $taskResultsPath) }
    'PlayTests' { $taskArguments += @('-runTests', '-testPlatform', 'PlayMode', '-testResults', $taskResultsPath) }
}

# -quit is deliberately omitted for test runs; the test runner owns shutdown.
# Wait for the editor itself; -Wait can also wait on persistent licensing children.
if ($Action -in @('EditTests', 'PlayTests') -and (Test-Path -LiteralPath $taskResultsPath)) {
    Remove-Item -LiteralPath $taskResultsPath
}
$taskQuotedArguments = $taskArguments | ForEach-Object { '"' + $_ + '"' }
$taskProcess = Start-Process -FilePath $EditorPath -ArgumentList $taskQuotedArguments -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
$taskProcess.Refresh()
if ($taskProcess.ExitCode -ne 0) {
    throw "Unity $Action failed with exit code $($taskProcess.ExitCode). Read $taskLogPath."
}

if ($Action -in @('EditTests', 'PlayTests')) {
    if (-not (Test-Path -LiteralPath $taskResultsPath)) {
        throw "Unity exited without producing test results. Read $taskLogPath."
    }
    [xml]$taskResults = Get-Content -LiteralPath $taskResultsPath -Raw
    $taskRun = $taskResults.'test-run'
    if ([int]$taskRun.total -eq 0 -or [int]$taskRun.failed -gt 0 -or $taskRun.result -ne 'Passed') {
        throw "Tests did not pass: $($taskRun.result), total=$($taskRun.total), failed=$($taskRun.failed). Read $taskResultsPath."
    }
    Write-Output "$Action passed: $($taskRun.total) tests. Results: $taskResultsPath"
} else {
    Write-Output "$Action completed. Log: $taskLogPath"
}

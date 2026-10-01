[CmdletBinding()]
param(
    [string]$EditorPath = $env:UNITY_EDITOR_PATH,
    [string]$SourceRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    Split-Path -Parent $PSScriptRoot
} else {
    (Resolve-Path -LiteralPath $SourceRoot).Path
}
$taskSource = Join-Path $taskRoot 'unity'
$taskRunRoot = Join-Path $taskRoot ('artifacts/c/' + (Get-Date -Format 'HHmmss'))
$taskProject = Join-Path $taskRunRoot 'unity'
$taskProbe = Join-Path $taskProject 'Library/PackageCache/com.unity.render-pipelines.core@3f80e92e91c2/Editor/Lighting/ProbeVolume/RenderingLayerMask/TraceRenderingLayerMask.urtshader'
if ($taskProbe.Length -ge 245) {
    throw "The clean Unity project path is too deep ($($taskProbe.Length) characters at a URP shader). Use a shorter checkout path."
}
$taskVersionLine = Get-Content -LiteralPath (Join-Path $taskSource 'ProjectSettings/ProjectVersion.txt') |
    Where-Object { $_ -match '^m_EditorVersion: ' }
$taskVersion = ($taskVersionLine -split ': ', 2)[1]
if ([string]::IsNullOrWhiteSpace($EditorPath)) {
    $taskToolchain = Join-Path $taskRoot 'local-toolchain.json'
    if (Test-Path -LiteralPath $taskToolchain) {
        $EditorPath = (Get-Content -LiteralPath $taskToolchain -Raw | ConvertFrom-Json).unityEditor
    } else {
        $EditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$taskVersion/Editor/Unity.exe"
    }
}
if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) { throw "Unity $taskVersion was not found." }
New-Item -ItemType Directory -Path $taskProject -Force | Out-Null
foreach ($taskFolder in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $taskSource $taskFolder) -Destination $taskProject -Recurse
}

function Invoke-CleanUnity {
    param([string]$Name, [string[]]$ExtraArguments, [string]$ResultsPath)
    $taskLog = Join-Path $taskRunRoot "$Name.log"
    $taskArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $taskProject + '"'),
        '-logFile', ('"' + $taskLog + '"')) + $ExtraArguments
    $taskProcess = Start-Process -FilePath $EditorPath -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
    $taskProcess.Refresh()
    if ($taskProcess.ExitCode -ne 0) { throw "$Name failed (exit $($taskProcess.ExitCode)); see $taskLog" }
    if ($ResultsPath) {
        if (-not (Test-Path -LiteralPath $ResultsPath)) { throw "$Name did not produce $ResultsPath" }
        [xml]$taskResults = Get-Content -LiteralPath $ResultsPath -Raw
        $taskRun = $taskResults.'test-run'
        if ([int]$taskRun.total -eq 0 -or [int]$taskRun.failed -gt 0 -or $taskRun.result -ne 'Passed') {
            throw "$Name did not pass; see $ResultsPath"
        }
        Write-Output "$Name passed: $($taskRun.total) tests"
    } else {
        Write-Output "$Name passed"
    }
}

Invoke-CleanUnity -Name 'Verify' -ExtraArguments @('-quit', '-executeMethod', 'Alabama.Editor.DistrictLoopScene.Verify')
foreach ($taskMode in @('EditMode', 'PlayMode')) {
    $taskResultsPath = Join-Path $taskRunRoot "$taskMode.xml"
    Invoke-CleanUnity -Name $taskMode -ExtraArguments @('-runTests', '-testPlatform', $taskMode,
        '-testResults', ('"' + $taskResultsPath + '"')) -ResultsPath $taskResultsPath
}
Invoke-CleanUnity -Name 'Build' -ExtraArguments @('-quit', '-executeMethod', 'Alabama.Editor.ProjectBuild.BuildDistrictLoopRelease')
Write-Output "Fresh copied project imported, tested, and built: $taskRunRoot"

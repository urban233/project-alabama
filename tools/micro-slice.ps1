[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Packages','Setup','Verify','Capture','Tests','Build','Probe','ProbeShortcut')]
    [string]$Action,
    [string]$EditorPath=$env:UNITY_EDITOR_PATH
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskProject=Join-Path $taskRoot 'unity'
$taskVersion=((Get-Content -LiteralPath (Join-Path $taskProject 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }) -split ': ',2)[1]
$taskOutput=Join-Path $taskRoot ('artifacts/MicroSlice'+$Action)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
if ($Action -in @('Probe','ProbeShortcut')) {
    $taskExe=Join-Path $taskRoot 'builds/micro-slice/Alabama.exe'
    if (!(Test-Path -LiteralPath $taskExe)) { throw 'Run Build before Probe.' }
    $taskProbe=Join-Path $taskOutput 'driving.json'
    if (Test-Path -LiteralPath $taskProbe) { Remove-Item -LiteralPath $taskProbe }
    $taskMode=if ($Action -eq 'ProbeShortcut') { '-microSliceShortcutProbe' } else { '-microSliceProbe' }
    $taskPlayer=Start-Process -FilePath $taskExe -ArgumentList @($taskMode,'-microSliceProbeOutput',('"'+$taskProbe+'"'),'-logFile',('"'+(Join-Path $taskOutput 'player.log')+'"'),'-screen-width','1280','-screen-height','720','-screen-fullscreen','0') -WindowStyle Hidden -PassThru
    if (!$taskPlayer.WaitForExit(240000)) { $taskPlayer.Kill(); throw 'Driving probe timed out.' }
    if ($taskPlayer.ExitCode -ne 0 -or !(Test-Path -LiteralPath $taskProbe)) { throw 'Driving probe failed; inspect player.log.' }
    $taskReport=Get-Content -LiteralPath $taskProbe -Raw | ConvertFrom-Json
    if (!$taskReport.passed) { throw 'Driving probe did not complete a valid lap.' }
    Write-Output ($taskReport | ConvertTo-Json)
    exit 0
}
if (!$EditorPath) {
    $taskLocal=Join-Path $taskRoot 'local-toolchain.json'
    if (Test-Path -LiteralPath $taskLocal) { $EditorPath=(Get-Content -LiteralPath $taskLocal -Raw | ConvertFrom-Json).unityEditor }
    else { $EditorPath=Join-Path $env:ProgramFiles "Unity/Hub/Editor/$taskVersion/Editor/Unity.exe" }
}
if (!(Test-Path -LiteralPath $EditorPath)) { throw "Supply -EditorPath for Unity $taskVersion." }
if ((Get-Item -LiteralPath $EditorPath).VersionInfo.ProductVersion -notmatch ('^'+[regex]::Escape($taskVersion)+'([_\s]|$)')) { throw 'The editor does not match the pinned version.' }
$taskArgs=@('-batchmode','-projectPath',('"'+$taskProject+'"'),'-logFile',('"'+(Join-Path $taskOutput 'editor.log')+'"'))
if ($Action -notin @('Capture','Build')) { $taskArgs+='-nographics' }
$taskMethod=switch($Action) {
    'Packages' { 'Alabama.Editor.MicroSlicePackages.Install' }
    'Setup' { 'Alabama.Editor.MicroSliceSetup.Setup' }
    'Verify' { 'Alabama.Editor.MicroSliceSetup.Verify' }
    'Capture' { 'Alabama.Editor.MicroSliceCapture.Run' }
    'Build' { 'Alabama.Editor.MicroSliceCapture.Build' }
}
if ($Action -eq 'Tests') {
    $taskResults=Join-Path $taskOutput 'results.xml'
    if (Test-Path -LiteralPath $taskResults) { Remove-Item -LiteralPath $taskResults }
    $taskArgs+=@('-runTests','-testPlatform','EditMode','-testFilter','Alabama.Tests.MicroSliceTests','-testResults',('"'+$taskResults+'"'))
} else {
    $taskArgs+=@('-executeMethod',$taskMethod)
    if ($Action -in @('Setup','Verify','Build')) { $taskArgs+='-quit' }
}
$taskProcess=Start-Process -FilePath $EditorPath -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit();$taskProcess.Refresh()
if ($taskProcess.ExitCode -ne 0) { throw "MicroSlice$Action failed; inspect $taskOutput/editor.log." }
if ($Action -eq 'Tests') {
    if (!(Test-Path -LiteralPath $taskResults)) { throw 'No test report was produced.' }
    [xml]$taskTest=Get-Content -LiteralPath $taskResults -Raw
    if ($taskTest.'test-run'.result -ne 'Passed' -or [int]$taskTest.'test-run'.total -eq 0) { throw 'Micro-slice tests failed.' }
    Write-Output "Passed $($taskTest.'test-run'.total) micro-slice tests."
} else { Write-Output "MicroSlice$Action completed: $taskOutput" }

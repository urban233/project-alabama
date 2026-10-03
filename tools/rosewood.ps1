[CmdletBinding()]
param(
    [ValidateSet('Generate','FinalizeContent','Verify','Build','AcceptSeam','Occlusion','ArtStudyCapture','ArtStudyLightingCapture','ArtStudyVerify')][string]$Action = 'Verify',
    [string]$EditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe',
    [switch]$AllowEditorUpgrade
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) { throw 'Supply the pinned Unity editor path.' }
$editorVersion = (Get-Item -LiteralPath $EditorPath).VersionInfo.ProductVersion
if ($editorVersion -notmatch '^6000\.3\.25f1[_\s]' -and
    -not ($AllowEditorUpgrade -and $editorVersion -match '^6000\.6\.4f1[_\s]')) {
    throw 'Use Unity 6000.3.25f1, or explicitly allow the installed 6000.6.4f1 for an art study.'
}
$taskOutput = Join-Path $taskRoot ('artifacts/NfsWorld/Rosewood/' + $Action)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskMethod = if ($Action -eq 'Occlusion') { 'Alabama.Editor.NfsWorldRosewoodSetup.BakeOcclusion' }
    elseif ($Action -eq 'ArtStudyCapture') { 'Alabama.Editor.NfsWorldCapture.RosewoodConnector' }
    elseif ($Action -eq 'ArtStudyLightingCapture') { 'Alabama.Editor.NfsWorldCapture.RosewoodConnectorLighting' }
    elseif ($Action -eq 'ArtStudyVerify') { 'Alabama.Editor.NfsWorldRosewoodSetup.VerifyConnectorStudy' }
    else { 'Alabama.Editor.NfsWorldRosewoodSetup.' + $Action }
$taskArguments = @('-batchmode','-projectPath',(Join-Path $taskRoot 'unity'),
    '-logFile',(Join-Path $taskOutput 'editor.log'),'-executeMethod',$taskMethod)
if ($Action -notin @('ArtStudyCapture','ArtStudyLightingCapture')) { $taskArguments += '-nographics' }
if ($Action -notin @('Occlusion','ArtStudyCapture','ArtStudyLightingCapture')) { $taskArguments += '-quit' }
$taskArguments = $taskArguments | ForEach-Object { '"' + $_ + '"' }
$taskProcess = Start-Process -FilePath $EditorPath -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit(); $taskProcess.Refresh()
if ($taskProcess.ExitCode -ne 0) { throw "Rosewood $Action failed; read $taskOutput/editor.log." }
Write-Output "Rosewood $Action passed. Log: $taskOutput/editor.log"

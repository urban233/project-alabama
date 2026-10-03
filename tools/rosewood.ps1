[CmdletBinding()]
param(
    [ValidateSet('Generate','FinalizeContent','Verify','Build','AcceptSeam','Occlusion')][string]$Action = 'Verify',
    [string]$EditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) { throw 'Supply the pinned Unity editor path.' }
if ((Get-Item -LiteralPath $EditorPath).VersionInfo.ProductVersion -notmatch '^6000\.3\.25f1[_\s]') { throw 'Use Unity 6000.3.25f1.' }
$taskOutput = Join-Path $taskRoot ('artifacts/NfsWorld/Rosewood/' + $Action)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskMethod = if ($Action -eq 'Occlusion') { 'BakeOcclusion' } else { $Action }
$taskArguments = @('-batchmode','-nographics','-projectPath',(Join-Path $taskRoot 'unity'),
    '-logFile',(Join-Path $taskOutput 'editor.log'),'-executeMethod',('Alabama.Editor.NfsWorldRosewoodSetup.' + $taskMethod))
if ($Action -ne 'Occlusion') { $taskArguments += '-quit' }
$taskArguments = $taskArguments | ForEach-Object { '"' + $_ + '"' }
$taskProcess = Start-Process -FilePath $EditorPath -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit(); $taskProcess.Refresh()
if ($taskProcess.ExitCode -ne 0) { throw "Rosewood $Action failed; read $taskOutput/editor.log." }
Write-Output "Rosewood $Action passed. Log: $taskOutput/editor.log"

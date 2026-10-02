[CmdletBinding()]
param([string]$BlenderPath = 'C:/custom_programs/Blender Foundation/Blender 4.4/blender.exe')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskStart = Get-Date
& $BlenderPath --background --factory-startup --disable-autoexec --python-exit-code 1 --python (Join-Path $taskRoot 'tools/blender/simplify_nfs_world.py') -- --root $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Blender rejected the visible-mesh reduction.' }
$taskReport = Get-Item -LiteralPath (Join-Path $taskRoot 'artifacts/NfsWorld/visual-reduction.json')
if ($taskReport.LastWriteTime -lt $taskStart) { throw 'No fresh reduction report was produced.' }
$taskData = Get-Content -LiteralPath $taskReport.FullName -Raw | ConvertFrom-Json
if (-not $taskData.finiteCoordinatesValidated -or $taskData.collisionChanged) { throw 'Reduction validation failed.' }
$taskTarget = Join-Path $taskRoot 'unity/Assets/Alabama/Art/Maps/NfsWorld/StyleMeshes'
New-Item -ItemType Directory -Path $taskTarget -Force | Out-Null
foreach ($taskCategory in @($taskData.meshes.category | Select-Object -Unique)) {
    $taskFile = Get-Item -LiteralPath (Join-Path $taskRoot "source-art/maps/nfs-world/style-export/$taskCategory.fbx")
    if ($taskFile.LastWriteTime -lt $taskStart) { throw "Stale export: $taskCategory" }
    Copy-Item -LiteralPath $taskFile.FullName -Destination (Join-Path $taskTarget $taskFile.Name) -Force
}
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action StyleOptimize
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action StyleVerify
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action DrivingTest
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action StyleCapture
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action StyleBuild

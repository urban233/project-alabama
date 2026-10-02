[CmdletBinding()]
param([switch]$SkipBlender, [switch]$SkipTextureBake, [string]$BlenderPath = 'C:/custom_programs/Blender Foundation/Blender 4.4/blender.exe')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskExitsPath = Join-Path $taskRoot 'unity/Assets/Alabama/Art/Maps/NfsWorld/ArtPass/exits.json'
if (-not (Test-Path -LiteralPath $taskExitsPath)) { throw 'Prepare and inspect the local district exit manifest first; see tools/nfs_world/README.md.' }
$taskExits = Get-Content -LiteralPath $taskExitsPath -Raw | ConvertFrom-Json
if (-not $taskExits.reviewed -or $taskExits.sourceCollisionChanged -or $taskExits.closures.Count -ne 6) { throw 'The six district exit spans need review.' }
if (-not $SkipBlender) {
    & $BlenderPath --background --factory-startup --disable-autoexec --python-exit-code 1 --python (Join-Path $taskRoot 'tools/blender/art_nfs_world.py') -- --root $taskRoot
    if ($LASTEXITCODE -ne 0) { throw 'Blender rejected the art meshes.' }
}
$taskReport = Get-Content -LiteralPath (Join-Path $taskRoot 'artifacts/NfsWorld/art-meshes.json') -Raw | ConvertFrom-Json
if (-not $taskReport.finiteCoordinatesValidated -or $taskReport.collisionChanged) { throw 'Art mesh validation failed.' }
$taskMeshes = Join-Path $taskRoot 'unity/Assets/Alabama/Art/Maps/NfsWorld/ArtMeshes'
New-Item -ItemType Directory -Path $taskMeshes -Force | Out-Null
foreach ($taskCategory in @($taskReport.meshes.category | Select-Object -Unique)) {
    Copy-Item -LiteralPath (Join-Path $taskRoot "source-art/maps/nfs-world/art-export/$taskCategory.fbx") -Destination $taskMeshes -Force
}
# Hand-authored images remain local inputs; the cached Blender bake treats the remaining district textures.
if (-not $SkipTextureBake) {
    & $BlenderPath --background --factory-startup --disable-autoexec --python-exit-code 1 --python (Join-Path $taskRoot 'tools/blender/bake_nfs_textures.py') -- --root $taskRoot
    if ($LASTEXITCODE -ne 0) { throw 'Blender rejected the texture bake.' }
}
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtOptimize
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtShadowPartition
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtOcclusion
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtVerify
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action DrivingTest
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action QualificationTest
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtCapture
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtExitCapture
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtBuild

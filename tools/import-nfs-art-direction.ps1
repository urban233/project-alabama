[CmdletBinding()]
param([switch]$SkipBake, [switch]$SkipQualification,
      [string]$BlenderPath = 'C:/custom_programs/Blender Foundation/Blender 4.4/blender.exe',
      [string]$PythonPath = 'python',
      [string]$EditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskSource = Join-Path $taskRoot 'source-art/maps/nfs-world/art-textures/art-direction/road-detail.png'
if (-not (Test-Path -LiteralPath $taskSource)) { throw 'Receive the private Developer B art-direction candidate, including its generated road-detail source.' }
if (-not $SkipBake) {
    & $BlenderPath --background --factory-startup --disable-autoexec --python-exit-code 1 --python (Join-Path $taskRoot 'tools/blender/bake_nfs_art_direction.py') -- --root $taskRoot
    if ($LASTEXITCODE -ne 0) { throw 'Blender rejected the art-direction bake.' }
}
& $PythonPath (Join-Path $PSScriptRoot 'nfs_world/verify_art_direction.py')
if ($LASTEXITCODE -ne 0) { throw 'Saved texture validation failed.' }
Copy-Item -LiteralPath $taskSource -Destination (Join-Path $taskRoot 'unity/Assets/Alabama/Art/Maps/NfsWorld/ArtDirection/Textures/road-detail.png') -Force
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtDirectionSetup -EditorPath $EditorPath
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtDirectionRuntimeSetup -EditorPath $EditorPath
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtDirectionOcclusion -EditorPath $EditorPath
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtDirectionRuntimeOcclusion -EditorPath $EditorPath
if (-not $SkipQualification) {
    & (Join-Path $PSScriptRoot 'unity.ps1') -Action EditTests -EditorPath $EditorPath
    & (Join-Path $PSScriptRoot 'unity.ps1') -Action PlayTests -EditorPath $EditorPath
}
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtDirectionBeforeCapture -EditorPath $EditorPath
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtDirectionCapture -EditorPath $EditorPath
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtDirectionBuild -EditorPath $EditorPath
& (Join-Path $PSScriptRoot 'nfs-world.ps1') -Action ArtDirectionRuntimeBuild -EditorPath $EditorPath

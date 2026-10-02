# Local KN5 map pipeline

This tooling reads static KN5 v5/v6 map data and preserves transforms, flags,
materials, embedded texture identities, and source metadata. It is an original
bounded reader; no downloaded converter is installed or executed. The layout was
checked against published format-reading source in
[RaduMC/kn5-converter](https://github.com/RaduMC/kn5-converter/blob/master/kn5%20converter/Program.cs).
It rejects unsupported formats and animated mesh nodes instead of guessing.

Use Python with NumPy and Pillow to inspect the nine extracted Downtown Rockport
files:

```powershell
python tools/nfs_world/inspect_map.py --root .
```

Sources, exported assets, generated scenes, metadata, and source-specific reports
stay local under Git-ignored paths. This map is excluded from the project's
original shareable asset pack. A separately pinned private `nfs-world` ZIP now
supports two-developer handoff; see [asset versioning](../../docs/asset-versioning.md)
and [the developer plan](../../docs/two-developer-plan.md). No asset redistribution
rights are established by conversion.

## Reproduce the local import

Run from the repository root with Unity closed. Use Python with NumPy and Pillow,
Blender 4.4, 7-Zip, and the project's pinned Unity 6000.3.25f1. Preserve the archive
at the repository root. Extract the selected files together because it is a solid
archive:

```powershell
& 'C:/custom_programs/7-Zip/7z.exe' x asw-nfs-world.7z '-osource-art/maps/nfs-world/original' '-aos' 'mauleous_nfs_world/nfs-world-DowntownRockport-*.kn5' 'mauleous_nfs_world/nfs-world-Pits.kn5' 'mauleous_nfs_world/models*.ini' 'mauleous_nfs_world/README.txt' 'mauleous_nfs_world/downtownrockport/*' 'mauleous_nfs_world/ui/downtownrockport/*' 'mauleous_nfs_world/extension/ext_config.ini'
python tools/nfs_world/inspect_map.py --root .
& 'C:/custom_programs/Blender Foundation/Blender 4.4/blender.exe' --background --factory-startup --disable-autoexec --python tools/blender/import_nfs_world.py -- --root . --sample
python tools/nfs_world/prepare_textures.py --root . --variant Sample
./tools/nfs-world.ps1 -Action SampleSetup
./tools/nfs-world.ps1 -Action SampleVerify
./tools/nfs-world.ps1 -Action SampleCapture
```

After checking the sample's scale, handedness, material alpha, and road collision,
export the entire district:

```powershell
& 'C:/custom_programs/Blender Foundation/Blender 4.4/blender.exe' --background --factory-startup --disable-autoexec --python tools/blender/import_nfs_world.py -- --root .
python tools/nfs_world/prepare_textures.py --root . --variant District
./tools/nfs-world.ps1 -Action Setup
./tools/nfs-world.ps1 -Action Verify
./tools/nfs-world.ps1 -Action DrivingTest
./tools/nfs-world.ps1 -Action Capture
./tools/nfs-world.ps1 -Action Build
./tools/nfs-world.ps1 -Action StylePreview
./tools/nfs-world.ps1 -Action StyleCapture
python -m unittest discover -s tools/tests
./tools/unity.ps1 -Action EditTests
./tools/unity.ps1 -Action PlayTests
```

Run each Unity operation sequentially. In the restricted Codex session, the batch
editor needs the normal user's licensed execution context; the sandbox's separate
license-client connection is unavailable. Generic script and reader tests run
without the Unity assets. The local map PlayMode test is skipped when the map is
absent, so ordinary public-project test runs do not need this source archive.

For an explicitly requested visible player measurement:

```powershell
./tools/benchmark-nfs-world.ps1 -Visible
```

The benchmark must verify actual rendering and GPU timing before its frame times
are used. Hidden Windows players can run their update loop without rendering and
will fail that verification. Do not combine a rendered benchmark with captures or
another GPU workload. Its stationary views do not certify an entire driving route.

Scene regeneration replaces the generated baseline: keep manual experiments in a
separate scene. The style command deliberately creates a separate preview. Do not
include `Assets/Alabama/Art/Maps/NfsWorld` in `docs/assets.json` or the asset pack.

After reviewing the lighting study, generate the separate lower-poly variant:

```powershell
./tools/import-nfs-world-style.ps1
./tools/benchmark-nfs-world.ps1 -Visible -Style
```

This helper runs Blender with a nonzero Python error exit code, verifies a fresh
finite-coordinate report, copies completed FBX files from outside Unity's asset
tree, then builds the visual-only spatial batches and source-sized texture arrays.
It validates collision and texture-layer metadata, drives both scene variants,
captures the result, and builds `builds/nfs-world-style/Alabama.exe`.

The Blender script begins with fixtures proving that bounds queries leave vertices
unchanged and coplanar reduction preserves known UVs/normals. It welds coincident
geometry and dissolves nearly coplanar architectural detail with UV/normal seams
retained. Invalid coordinates, changed extrema, or changed geometric boundary
curves cause the Unity assembly to retain the original visible mesh. Source
sign/prop geometry and skyline exceptions remain intact. The accepted polygon
reduction is modest; spatial batching is the main performance improvement.

The local pipeline copy enables SRP batching and uses FXAA, retaining the approved
lighting and shadow settings. Its runtime component restores the original pipeline
and global antialiasing value on exit. `StylePreview` recreates the lighting-only
study and also saves `NfsWorldLightingStudy.unity` for comparison; rerun
`StyleOptimize` to restore the reduced/batched version. `LightingCapture` captures
the preserved lighting-only scene without replacing the optimized scene.

See [implementation status](../../docs/nfs-world-import-status.md) for controls,
the verified coordinate transform, evidence paths, and remaining qualification.

## Separate model and texture art study

The art study starts from the preserved lighting-only scene, keeping the approved
sun, ambient fill, fog, exposure and shadows. It produces a third scene/player;
the baseline and optimized first visual pass remain available for comparison.

```powershell
./tools/import-nfs-world-art.ps1
# Reuse the completed Blender exports when only the reviewed textures change:
./tools/import-nfs-world-art.ps1 -SkipBlender
# Reassemble already baked textures and meshes without regenerating either:
./tools/import-nfs-world-art.ps1 -SkipBlender -SkipTextureBake
```

`art_nfs_world.py` reads the original editable models. Suitable closed manifold
solids are rebuilt from fourteen extremal support directions into coarse convex
forms. Volume ratios reject unsuitable concave structures; proxy vertex, edge
and face samples must lie within 30 cm of building source surfaces or 15 cm for
props. These sample checks do not certify a maximum error at every surface point.
UVs transfer from
source triangles. Limited edge dissolves keep UV/material seams and open boundaries;
3-degree architecture and 15-degree opaque street furniture/trunks simplify small
curves. Accepted architecture, street furniture and trunks use flat facets.
Road/bridge meshes, alpha cards and open architectural structures are protected.
Blender rejects extrema changes over 2 cm; Unity checks boundary curves in both
directions with a 2 cm tolerance for intentional visible faceting and rejects
larger outline changes/new openings. Rejected simplifications and other opaque
building/prop/wall/skyline/tree meshes still receive exact-position flat shading;
coplanar faces share vertices to avoid unnecessary vertex growth. Roads, bridges
and cutout cards are excluded. The older visual pass keeps its tighter 3 mm
boundary tolerance. Collision always uses original meshes.

Fourteen reviewed diffuse textures live in ignored `ArtPass/Textures`, with thirty-seven
source hash mappings in `ArtPass/textures.json`. The built-in imagegen tool produced
nine facade/window tiles, three wall/concrete tiles and two transparent autumn foliage cards.
Prompts and source identities are saved in
`source-art/maps/nfs-world/art-textures/prompts.json`, `prompts-angular.json` and
`prompts-facades.json`. `prompts-all.json` collects all fourteen accepted prompts,
their saved workspace destinations and the rejected variants' reasons.
Facade layout is inspected before binding; misleading texture names are insufficient
to classify windows, doors or marking atlases. Plain wall tiles intentionally
replace surface seams with larger repeating blocks. Equivalent four-window facade
families intentionally share the simplified grid and quieter wall palette; window
shapes and pane sizes can become broader or more angular. The generated layout is
checked visually and in Unity; a two-window facade candidate and two candidates
that removed filled wall regions were rejected before import.
Unity imports these at the largest associated source texture resolution with mipmaps;
foliage keeps genuine alpha. Imported texture dependency hashes invalidate array
caches when a texture changes. Source textures and car assets remain unchanged.

The helper also runs `bake_nfs_textures.py`: Blender image buffers perform
alpha-weighted edge-preserving albedo smoothing and joint brightness quantization
across the remaining visible district textures. Independent colour-channel
quantization was rejected because it introduced coloured bands in neutral glass.
The current recipe retains exact dimensions and cutout alpha, forces opaque
input alpha to one, preserves lettering/marking/effect source textures, and keeps
the hand-authored mappings. It saves separate PNGs in ignored `Textures/Baked`.
Recipe/source hashes cache unchanged outputs. `verify_texture_bake.py` compares
saved PNG dimensions, alpha and brightness to source images; it reports geometric
surface coverage separately from screen-space visibility. Reports are in
`artifacts/NfsWorld/texture-bake.json` and `texture-bake-validation.json`.

Outputs: `NfsWorldArtPass.unity`, `builds/nfs-world-art/Alabama.exe`,
`artifacts/NfsWorld/art-meshes.json`, `art-validation.json`, `art-batching.json`,
`collision-Art.json` and `Captures/ArtPass`. Texture treatment spans the full
district; substantial polygon/LOD reduction remains further authoring work.
A clean checkout still needs the local source archive and generated assets.

## District exits and performance qualification

The art helper requires the local reviewed `ArtPass/exits.json` before importing.
Extract only the six neighbouring `*-RoadsPhysical.kn5` files into ignored
`source-art/maps/nfs-world/neighbor-roads/mauleous_nfs_world/`. Pass explicit archive
member paths to 7-Zip; an include filter alone can retain its default all-files
selection. Run these scripts with the local Python/NumPy/Pillow runtime:

```powershell
python tools/nfs_world/audit_district_exits.py
python tools/nfs_world/plan_exit_closures.py
```

The first script probes outside Downtown's physical road boundaries, retaining
source elevation when matching neighbouring road triangles. The second groups
those connections and the northern unfinished terminal into six barrier spans,
five metres inside the retained district. Inspect `exit-closure-plan.png` and
the source topology before setting `reviewed` to true in the local manifest.
Identical regeneration retains review; changed spans invalidate it. Source data
and the reviewed manifest stay ignored. `ArtExits` imports the angular walls with
grounded box collision; `ArtExitCapture` saves six road-level previews.

```powershell
./tools/nfs-world.ps1 -Action QualificationTest
./tools/nfs-world.ps1 -Action ArtRenderOptimize
./tools/nfs-world.ps1 -Action ArtShadowPartition
./tools/nfs-world.ps1 -Action ArtOcclusion
./tools/nfs-world.ps1 -Action ArtBuild
./tools/benchmark-nfs-world.ps1 -Visible -Art
./tools/benchmark-nfs-world.ps1 -Visible -Art -Driving -FrameRateCap 30
```

Occlusion baking must follow scene regeneration: it stores scene-specific static
visibility data in the ignored scene directory. Opaque surfaces can occlude;
foliage/fence cutouts cannot. Captures and rendered benchmarks must verify the
result. Exact shadow surfaces are partitioned into 32-metre batches, retaining
every source triangle, normal and UV/cutout mask; colour batches stop casting
duplicate shadows. This increases stored meshes and build/import work, while
tighter bounds reduce unnecessary cascade submissions. Source collision is unchanged.
The art helper now runs shadow partitioning and occlusion after scene generation.
The scene-local renderer preserves approved lighting/shadows and uses FSR1 at
0.75 render scale (1440x810 world, 1920x1080 output/HUD). Ordinary art play is capped
at the user's revised 30 FPS target; other prototype scenes keep their own target.

Reports expose both resolutions, GPU/CPU thread timings, frame percentiles and
Windows power/charger state at both ends of the run. The helper records this state
without changing the power plan. `-Driving` adds nine two-second traversals over
three source-road corridors, repeated three times at initial 126 km/h along the
road grade. It rejects failed displacement or wheel support. This samples movement
and loading/culling behaviour, rather than certifying every street. `-FrameRateCap`
tests pacing; omit it to measure uncapped performance headroom. Suffixes `-driving`
and `-cap30` identify these reports. Optional `-DiagnosticNoShadows`,
`-DiagnosticHardShadows`, `-DiagnosticRenderScale` and `-GraphicsApi` affect only
the benchmark process and write separate diagnostic reports; they are profiling
tools, not accepted final settings. See the status document for observed results.

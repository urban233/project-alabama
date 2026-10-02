# Downtown Rockport local prototype

Implementation status: 2 October 2026. Branch: `codex/asw-nfs-world-import`.
Code/documentation checkpoints are committed and pushed on the feature branch as
requested. Map assets, source archives, textures, models, generated scenes, builds
and evidence remain local and excluded from Git; the asset pack is unchanged.
A final PR will be opened only after completion and request `urban233` as reviewer.

The nine Downtown Rockport source models have been converted through Blender 4.4
to binary FBX and imported into Unity 6000.3.25f1. The complete selected district
is present in a separate free-roam scene with the existing E46, controls, chase
camera, telemetry, reset, and automatic recovery after falling below the map.
The baseline, optimized visual pass, and separate model/texture art pass are
available for local testing.
The latter retains the approved autumn lighting and shadows and improves rendering
through spatial batches, shared materials, and a local pipeline copy. Substantial
geometry simplification, full-road traversal, district exit treatment, and 60 FPS
qualification remain outstanding; this is a prototype delivery.

## Open and play locally

- Windows player: `builds/nfs-world/Alabama.exe`.
- Optimized visual-pass player: `builds/nfs-world-style/Alabama.exe`.
- Model/texture art player: `builds/nfs-world-art/Alabama.exe`.
- Unity scene: `unity/Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldPrototype.unity`.
- Separate visual study: `unity/Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldStylePreview.unity`.
- Preserved lighting-only comparison: `unity/Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldLightingStudy.unity`.
- Model/texture scene: `unity/Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldArtPass.unity`.
- Editable sources: `source-art/maps/nfs-world/blender/District/`.
- Separate authored model copies: `source-art/maps/nfs-world/blender/ArtMeshes/`.
- Conversion and regeneration commands: [pipeline README](../tools/nfs_world/README.md).

Controls: W/S or arrow keys for throttle, brake/reverse; A/D or left/right for
steering; Space for handbrake; R to reset to spawn; Escape to pause. Gamepad
triggers, left stick, south face button, north face button, and Start provide the
equivalent actions. There is no authored race route, traffic, police, or lap logic.

The neighboring six districts are excluded. Roads leading into those districts
can terminate at the import boundary; they currently have no authored closures.
R resets immediately; automatic recovery resets falls below local Y = -80 m.
This recovery is not a substitute for reviewing and closing each exit.

## Conversion contract

The original static KN5 reader retains the source hierarchy, evaluated transforms,
normals, UVs, material/texture identities, and collision/visibility metadata. It
rejects corrupt input and unsupported animated/protected formats. Source-specific
manifests and extracted textures are local data, not instructions to execute.

All categories share `AC_PIT_0` as the source origin:
`(1978.0977783, 99.6069107, 1165.7668457)` metres. Source XYZ becomes Blender
X/-Z/Y, then Unity -X/Y/Z. Keeping the FBX handedness reflection is necessary for
readable lettering and correct face orientation. Each export contains 10-metre
axis markers, and Unity verifies those markers and every mesh's world bounds.
Meshes are never centered independently. Zero-area triangles are removed and
recorded; driving collision is otherwise retained independently of visible meshes.

Hidden source physical meshes stay hidden, while static non-convex MeshColliders
provide road, wall, dirt, and grass collision. Source alpha-test overrides inform
explicit URP materials; textures are named by content hash, preserving distinct
same-name DDS files and their alpha channels. AC-specific night lighting, dynamic
effects, and shader behavior are not fully recreated by this baseline.

Source-configured 500/1,000-metre visual distance limits apply to 4,011 renderers.
The later skyline/landmark exceptions override earlier limits. Runtime checks use
distance to each renderer's bounds, retaining large meshes when any part is nearby;
all collision remains active. Disabling the culling component restores its renderers.
These conservative limits do not replace spatial mesh splitting or lower-detail LODs.

## Verified evidence

| Check | Result | Local evidence |
| --- | --- | --- |
| Python parser and asset-pack tests | 12 passed | `tools/tests/` |
| Blender fixtures | Bounds query does not mutate vertices; planar reduction preserves fixture UVs/normals | `tools/blender/simplify_nfs_world.py` |
| Existing asset manifest and Unity metadata | 97 original files unchanged, five assets | `tools/verify_assets.py` |
| Unity EditMode suite | 12 passed, including boundary protection and exact flat-face geometry/UV checks | `artifacts/EditTests/results.xml` |
| Unity PlayMode suite | 9 passed, including all three driving variants, elevated contact, and pipeline restoration | `artifacts/PlayTests/results.xml` |
| Sample conversion collision | 700/700 road samples matched | `artifacts/NfsWorld/collision-Sample.json` |
| Full district collision | 13,834/13,834 samples matched, maximum error 0.765 mm | `artifacts/NfsWorld/collision-District.json` |
| Final visual-pass collision | Same 13,834 samples and exact collision triangle count | `artifacts/NfsWorld/collision-Style.json` |
| Strict Windows release build | Succeeded | `artifacts/NfsWorld/Build/editor.log` |
| Strict Windows visual-pass build | Succeeded | `artifacts/NfsWorld/StyleBuild/editor.log` |
| Actual URP images | Baseline chase and district overview, separate visual study | `artifacts/NfsWorld/Captures/` |

The full scene contains 4,088 visible meshes / 3,132,972 visible triangles and
2,201 static MeshColliders / 3,381,268 collision triangles. These are total asset
counts, not per-frame rendered counts. The road sampler covers 1,158 physical road
meshes, including one ceiling-only bridge underside; it samples up to 12 upward
faces per mesh. This tests alignment and cooking at sampled locations, not every
triangle or every possible route. The car test checks four grounded wheels,
acceleration, braking, reset, and fall recovery at the source spawn, four-wheel
contact on two measured elevated roads, collision retention when culling changes,
and restoration of the original render pipeline and antialiasing value. It does not
certify high-speed driving throughout the complete district.

## Performance and visual direction

The opt-in standalone benchmark samples five stationary road viewpoints at
1920 × 1080, with 120 warmup frames and 360 measured frames per viewpoint. It
requires camera-render events and nonzero GPU frame timings. A hidden Windows
run produced zero rendering and was rejected; its loop timing is not performance
evidence. A visible run was explicitly authorized. The benchmark is not an
automated driving-route qualification. Results and the actual hardware are
recorded in `artifacts/NfsWorld/player-view-benchmark.json`.

The final baseline run with source distance culling averaged 11.9–14.0 FPS.
The final visual-pass run averaged **36.4–44.4 FPS**, with **26.2–32.2 ms p95**
frame times. It verified 2,400 camera renders and 1,800 GPU samples on AMD
Radeon(TM) Graphics / Ryzen 7 5700U. The repository's 1080p / 60 FPS target is
**not met on this hardware**. The existing RTX 4060 benchmark elsewhere in the
repository describes different hardware and cannot qualify this import.

| Final view | Mean FPS | p95 frame ms | Draw calls | All-pass rendered triangles | SetPass calls |
| --- | ---: | ---: | ---: | ---: | ---: |
| 0 | 36.4 | 32.2 | 1,473 | 2,658,602 | 50 |
| 1 | 44.4 | 26.2 | 1,132 | 1,889,999 | 50 |
| 2 | 37.1 | 30.8 | 1,558 | 3,615,953 | 58 |
| 3 | 37.2 | 31.5 | 1,347 | 2,293,552 | 50 |
| 4 | 40.1 | 28.8 | 928 | 1,439,317 | 50 |

Rendering counters are snapshots from the last measured frame of each view,
including shadow/depth passes. They are not unique scene-triangle counts.
Allocated Unity memory measured 685,502,102 bytes in the final visual pass,
versus 1,088,996,070 bytes in the baseline. These are allocated-memory snapshots,
not peak system/GPU-memory measurements. The last two rendered runs were sequential
with no other Unity or Blender workload; single local runs are not a controlled
thermal/performance study. Earlier reports before culling and with four-sample
MSAA are retained separately. The initial pre-culling run overlapped an editor
job and is only indicative evidence.

The user reviewed the actual Unity study and selected **keep this direction and
current shadows**. Warm sunlight, muted autumn colours, haze, roughness, saturation,
and ACES tonemapping are retained in the first visual pass. The final variant
contains **3,112 visible batches / 3,126,068 visible triangles**, using **58 array
materials**. It removes only **6,904 triangles (0.22%)** from the baseline: substantial
whole-district polygon reduction has not been achieved. Most of the rendering gain
comes from spatial/material batching and better culling, rather than decimation.

Collapse decimation opened source façades and was rejected. The current Blender
pass welds coincident geometry and dissolves nearly coplanar detail while retaining
UV/normal seams. Bounds changes above 2 cm are rejected before export. Unity also
checks imported extrema and geometric boundary curves, allowing 3 mm for its 1 mm
coordinate-rounding noise, and falls back to the original visible mesh when those
checks fail. Roads, terrain, signs/props, foliage cards and skyline exceptions retain
their source geometry. All 2,201 colliders / 3,381,268 collision triangles remain exact.

The visual pipeline groups source textures by dimensions, compression, mipmaps,
and alpha-test state into texture arrays without resizing them. A small URP shader
uses the existing PBR lighting functions and the project's Forward+ variant, with forward, shadow, depth and normal
passes. Per-vertex array-layer and cutoff data preserves tiled UVs and cutouts.
Whole triangles are assigned to 256-metre spatial cells, with identical positions
retained at boundaries, and batched by array material, shadow state, and distance
class. This addresses meshes whose original bounds span much of the district.
Array cache manifests verify ordered texture GUIDs and dependency hashes before reuse. Generated visual
assets and editable Blender sources remain local and ignored.

The visual scene applies a local copy of the active URP pipeline with SRP batching
enabled and FXAA replacing four-sample MSAA. Render scale, sun, ambient fill,
cascades, shadow-map resolution, distance and softness remain unchanged. The
original pipeline and global antialiasing value are restored on scene exit and
after captures; project rendering settings have no remaining Git diff. Camera
captures apply the runtime distance limits; the overview deliberately shows the
complete district. The preserved lighting-only scene uses the original geometry
and project pipeline for comparison.

## Model and texture art pass

The user requested stronger changes to the actual models and textures, with
buildings and props noticeably simpler and more angular. `NfsWorldArtPass.unity`
starts from the preserved lighting study and keeps its sun, fog, ambient fill,
exposure and shadows. It has separate visible meshes, materials, texture arrays
and a Windows player, leaving the earlier scenes available for comparison.

Unity accepted **903 building meshes and 60 prop meshes** from the authored Blender
copies. This includes flat shading, limited curve simplification and **29 coarse
closed solids**: two building details and 27 prop instances across newspaper
boxes, rusty/crash barrels, garbage cans and a median pole. For rebuilt props,
the remaining open surfaces are retained rather than dissolved. Volume and
nearest-surface sampling constrain closed-solid rebuilding; extrema changes
above 2 cm and changed boundary curves above 2 cm are rejected. The 709 rejected
candidates retain their original geometry, including all candidate trunks.
An additional 1,543 opaque building, prop, wall, skyline and tree meshes receive
flat face normals without moving vertices, changing UVs or removing triangles.
Coplanar faces share vertices; angled faces split their normals. Roads, bridges
and alpha-cutout cards are excluded from this fallback treatment.
Large angular autumn leaves come from two new transparent foliage cards.

Fourteen generated diffuse textures replace **37 source materials**, covering
cleaner facade/window layouts, flat dark glass, larger quiet brick/concrete blocks
and autumn foliage. Nine files are facade/window textures, three are wall/concrete
tiles and two are foliage cards. The built-in image generation tool was used;
selected outputs are local in `ArtPass/Textures`. Source bindings and prompts are
saved under `ArtPass/textures.json` and
`source-art/maps/nfs-world/art-textures/prompts*.json`. Three unsuitable candidates
were rejected for layout or missing wall regions. Canvas aspect and required alpha
are checked on import; source images and the car's asset files are unchanged.

At the user's selection, Blender Python now bakes the remaining district albedo
textures with edge-preserving, alpha-weighted smoothing and restrained brightness
quantization. Quantizing brightness together avoids false green/magenta bands in
neutral glass. This is a colour/detail bake, not a lighting bake. It preserves
the UV canvas and source pixel dimensions, excludes lettering/marking/effect
textures by source name, and retains all fourteen hand-authored tiles. The cache
includes the recipe, source image hash, category and alpha treatment.

**925 baked textures plus 37 hand-authored source bindings** cover 98.64% of the
audited building/prop/tree/wall surface area; the remaining 1.36% uses 266 protected
lettering/decal/effect textures across the district. This geometric area includes
transparent card rectangles and is not screen-space visibility or ground coverage.
All 122 baked cutout alpha masks are byte-exact; all baked dimensions match.
The maximum saved-image mean RGB shift is 0.01734 on a 0–1 scale. Original PNGs
are unchanged. Saved-PNG verification is in `texture-bake-validation.json`.
Unity binds replacements to **971 materials** and imports them in a batch.

The resulting art scene has **2,721 batches / 3,117,257 visible triangles**, with
53 texture-array materials. The reduction from baseline is **15,715 triangles
(0.50%)**. This is primarily an appearance pass; substantial district-wide polygon
reduction has not been achieved. The new art player has not been benchmarked, so
the earlier optimized player's FPS figures do not qualify it.

All **2,201 colliders / 3,381,268 collision triangles** remain exact, and
**13,834/13,834 road samples** match, maximum error 0.765 mm. The full Unity suites
passed 12 EditMode and 9 PlayMode tests, including all three converted-map driving
tests for the final district-wide texture/facet revision. The strict art Windows build
succeeded. Evidence is in `artifacts/NfsWorld/art-validation.json`,
`art-batching.json`, `collision-Art.json`, `DrivingTest/results.xml`,
`ArtBuild/editor.log` and `Captures/ArtPass`.
The original asset manifest check still verifies 97 files across five assets.

The texture treatment now spans the full district. Protected signs/effects and
source UV layouts remain, and the reference is an art direction rather than an
exact scene match. Broad silhouette/LOD reductions still require further authoring;
flat shading alone does not reduce triangle counts. The reference's traffic, police and pursuit HUD
remain outside the map conversion.

Next work: author stronger LOD/silhouette
reductions without losing façades/signs, close reviewed district exits, and qualify
multi-level/high-speed routes. Further performance work is required for 60 FPS on
the tested integrated GPU. No entire-district driving certification is claimed.

All map sources, Blender files, FBX/PNG/material/scene assets and their metadata,
builds, and evidence remain under Git-ignored paths. The archive credits
Mauleous_Gaming and NFS World/Most Wanted/Carbon content. This is local prototype
work; the existing shareable asset pack has not acquired these map files.

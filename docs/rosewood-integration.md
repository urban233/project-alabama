# Developer A: Rosewood conversion and connection

Rosewood is separate additive content under
`Assets/Alabama/Art/Maps/NfsWorld/Rosewood/RosewoodContent.unity`. Developer B's
persistent `Runtime/DistrictRuntime.unity` retains the car, input, chase camera,
sunlight, atmosphere and approved map render settings. Both districts initially
load together. Original Downtown standalone scenes remain the reproducible baseline.

The public recipe is `docs/districts/rosewood.json`. It selects the nine supplied
Rosewood source models, source pit marker, separate output/editable/texture
directories, and the accepted Downtown `AC_PIT_0` origin. Never recenter on
Rosewood's own pit. The Blender/FBX axis markers, metre scale, triangle counts and
world bounds are validated before content is saved. The source pit marker has no
physical road beneath it. Recovery uses the nearest supplied traffic-lane point
at the pit's level (lane 707, point 12, about 12 metres away); Unity verifies all
four tyre positions and chassis clearance on Rosewood-owned collision.

## Reproduce the private content

Restore the private packs required by the checked-in locks first. Keep existing
sources and Unity `.meta` files. The root `asw-nfs-world.7z` supplies the nine
`mauleous_nfs_world/nfs-world-Rosewood-*.kn5` members and
`mauleous_nfs_world/rosewood/data/traffic.json`; extract only these into
`source-art/maps/nfs-world/original/rosewood/`, retaining the archive's hierarchy.
Do not overwrite locally edited sources. Use the existing pinned Blender 4.4
and Python with NumPy and Pillow. Run from the project root with editors closed:

```powershell
python tools/nfs_world/inspect_map.py --root . --config docs/districts/rosewood.json
& 'C:/path/to/blender.exe' --background --factory-startup --disable-autoexec --python tools/blender/import_nfs_world.py -- --root . --config docs/districts/rosewood.json
python tools/nfs_world/prepare_textures.py --root . --variant District --config docs/districts/rosewood.json
& 'C:/path/to/blender.exe' --background --factory-startup --disable-autoexec --python tools/blender/art_nfs_world.py -- --root . --config docs/districts/rosewood.json
& 'C:/path/to/blender.exe' --background --factory-startup --disable-autoexec --python tools/blender/bake_nfs_textures.py -- --root . --config docs/districts/rosewood.json
python tools/nfs_world/audit_district_exits.py --root . --config docs/districts/rosewood.json
python tools/nfs_world/plan_exit_closures.py --root . --config docs/districts/rosewood.json
python tools/nfs_world/prepare_rosewood_connection.py --root .
```

Inspect `artifacts/NfsWorld/Rosewood/exit-closure-plan.png` against the source
overview before setting the generated `Rosewood/ArtPass/exits.json` review flag.
The six-neighbour physical-road audit identifies nine Rosewood frontiers. Unity
also rejects closure spans without support on the retained physical road.

The visual recipe protects lettering/markings, texture dimensions and cutout
alpha. Simplified Blender candidates must retain bounds, openings and boundary
curves; Unity falls back to source meshes when imported candidates fail the
silhouette checks. Colour geometry is spatially batched, and exact visual
surfaces supply 32-metre shadow chunks. Distance culling remains scene-owned.

The derived runtime scenes use mesh copies grouped by source GUID into 256
buckets per district. This reduces individual asset-file lookups during scene
loading. Packaging verifies identical vertex/index bytes, submesh ranges, bounds
and readability. It changes only runtime mesh references; all original mesh files,
Unity GUIDs, transforms, materials, collision shapes and standalone scenes remain.
The grouped assets are private under Rosewood's `RuntimeMeshes/` and
`Runtime/DowntownMeshes/`; subsequent saves retain their metadata.

## Selected seam and ownership

Downtown `downtown-exit-3` pairs with Rosewood `rosewood-exit-7`. The private
source lane curves 807 (points 4–66) and 808 (points 43 onward) provide opposing
real-road driving routes. Both endpoints remain about 20 metres inside the
retained Downtown Palmont barrier; the complete source lanes extend beyond the
imported district and remain unchanged.
The other five Downtown and eight Rosewood exits stay closed.

Downtown owns the tiny overlapping source road area. The ownership tool derives
four Rosewood scene colliders, retaining Rosewood's road grade and excluding
0.002415 m² of overlap across 21 source faces. Original KN5/Blender/FBX models
are preserved. Visuals and the remaining collision stay independent of this cut.
The derived meshes and detailed inventories remain private.

Generate and qualify using Unity 6000.3.25f1:

```powershell
./tools/rosewood.ps1 -Action Generate -EditorPath 'C:/path/to/Unity.exe'
./tools/rosewood.ps1 -Action Build -EditorPath 'C:/path/to/Unity.exe'
./tools/qualify-rosewood.ps1 -Mode Seam -Visible
./tools/rosewood.ps1 -Action AcceptSeam -EditorPath 'C:/path/to/Unity.exe'
./tools/rosewood.ps1 -Action Occlusion -EditorPath 'C:/path/to/Unity.exe'
./tools/rosewood.ps1 -Action Build -EditorPath 'C:/path/to/Unity.exe'
./tools/qualify-rosewood.ps1 -Mode Route -Visible -FrameRateCap 30
./tools/qualify-rosewood.ps1 -Mode Route -Visible -FrameRateCap 0
./tools/qualify-district-lifetime.ps1 -Rosewood -Visible
```

The first seam trial opens only the two selected closures in the disposable
player scene instances. `AcceptSeam` requires a current successful rendered
report whose signature matches both content scenes before saving the paired
connection records and opening their saved barriers. It does not accept fixture
recovery as seam proof. Generating content again restores the closed trial state.
Verified connection gates also follow district lifetime: they open only while
matching reciprocal content is registered and close when that neighbour unloads.
Building an accepted connection preserves its saved qualification identity and
host scene. Unity importer dependency hashes can differ between machines; a
normal received-pack build must not rewrite accepted artwork. Regenerated trial
builds still refresh the identity before their rendered qualification.

The player probe drives with the production controller and four WheelColliders,
checks both scene-owned road contacts and duplicate district contact at each
tyre, and preserves the same car/input/camera and host rendering across each
crossing. Each opposing leg has an independent initial spawn; there are no body
position/velocity writes during a leg. The longer legs cover the source curves
and grades in both directions. These are repeatable qualification drives, not
exhaustive proof of every Rosewood street.

Combined occlusion includes the host, Downtown and Rosewood. The previous
Downtown-only bake is disabled while generating/trialling the new content.
Reports, game captures, power state and frame statistics are under
`artifacts/NfsWorld/Rosewood/`; the combined Windows player is under
`builds/nfs-world-rosewood/`. Actual camera rendering and nonzero GPU samples are
required. Compare capped pacing and uncapped headroom with sunlight/shadows and
1080p output / 0.75 FSR1 render scale retained.

## Private release

Run the full Python and Unity Edit/Play Mode suites and the combined map/player
checks before publication. Allow twenty minutes for each cold standalone-map test and forty minutes for
the two-reload/combined-map cases; the cold Downtown editor integration on this
workstation measured about ten minutes. These bounds include native scene loading
and do not change the driving/contact/closure assertions. Record editor load times
separately from measured player startup and driving performance. Review B's owned contributor additions first; preserve
the original release and contribution ZIPs. A publishes the next minor NFS
version for the new district using the shared asset procedure. The completed
lock, code revision and verification record must accompany the root ZIP/checksum
through a private transfer. B then receives and independently verifies them.
Artwork, full inventories, captures and ZIPs stay out of Git and GitHub.

B's candidate added runtime files outside the installed 0.1.0 receipt. Receiving
the integrated release can therefore report unmanaged conflicts for A's replaced
`Runtime/DistrictRuntime.unity`, `Runtime/DowntownContent.unity` and
`Runtime/DistrictRuntime/OcclusionCullingData.asset`. Keep B's original candidate
ZIP/checksum. With editors closed, compare those local files against their exact
candidate-manifest hashes before moving only matching superseded files to an
ignored backup. Leave their `.meta` files in place, then run `receive` again.
Preserve and review any file that differs from the candidate; never force-import
or move unrelated local artwork. The private handoff records the before/after
hashes and matching source commit.

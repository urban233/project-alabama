# Whole playable map: reference adaptation

This follow-up uses the [art style guide](2026-10-02-nfsmw/ART-STYLE-GUIDE.md)
and [adaptation guide](2026-10-02-nfsmw/agent-reference-pack/ADAPTATION-GUIDE.md)
throughout the current playable Downtown Rockport map. The three balanced
images determine fidelity; the six supplementary images guide individual
surface and structural families. The original screenshot supplies atmosphere.
The other studies remain comparison bounds and composition context.

Developer B owns this map and shared rendering. The imported route, metre scale,
landmarks and drivable openings remain authoritative. The reference lighthouse,
safehouse and stadium are visual examples rather than measured replacement maps.
`main` and `origin/main` were refreshed on 4 October 2026 and both resolve to
`fe2e19f`; this work is on `codex/developer-b-art-direction`.

## What changed after the first geometry pass

The previous pass reached most material bindings but its actual captures still
had flat pavement and dark, brown shadows. A regular district survey captured
**17 source-road locations in four directions: 68 views**. It exposed the same
response in ordinary streets, construction areas, elevated roads and downtown
blocks. This follow-up changes shared surface/rendering behavior across those
areas, and extends the derived prop geometry.

| Reference concern | Treatment throughout the map |
| --- | --- |
| Matte, moderately rough pavement | Source-aligned albedos and world-space grayscale aggregate; stronger restrained variation and shallow normal relief on explicitly reviewed pavement layers. Paint, coloured regions and vertical atlas regions are masked. Relief fades between 40 and 110 m. Physical roads stay exact. |
| Solid architecture and moderate seams | The guarded facade cleanup, planar structural normals, UV window grids, masonry transitions and source openings remain. Geometry is allocated to existing building masses, railings, curbs and passage structure. |
| Ordinary street furniture | Existing 128 box/bin form projections plus 267 crash-barrel and 79 round-bin radial profiles. The latter use a twelve-sided radial envelope with at most 1.5 cm movement; original topology and UV corners remain. This describes the shaping envelope, not a claim that every source ring has twelve edges. |
| Natural autumn crowns | Original crown gaps, card alpha and branches survive the alpha-aware texture treatment. No crystalline replacement crowns or uniformly flattened foliage normals. |
| Readable passages and cool shadows | A separately captured lighting study raises diffuse fill and reduces the orange direct-light bias. The persistent runtime owns the explicit diffuse probe; content loading keeps the same illumination. |
| Near/middle/far separation | Blue-gray fog colour and retained fog density separate the skyline. Validated distance meshes retain industrial and landmark silhouettes. |
| Restrained car reflections | Derived body/glass/alloy responses remain, with the smoother original car geometry. |

The recorded stages are `Captures/ArtDirectionPrevious/` (previous pass),
`ArtDirectionSurface/` (pavement under unchanged light), and
`ArtDirectionLighting/` (the separate light study). Final regenerated captures
and the second map survey are recorded by qualification below. These are actual
Unity captures; generated reference images are never used as facade atlases.

## Recipes and reproduction

- `downtown-material-recipe.json`: exact source texture IDs and pavement response.
- `downtown-lighting-recipe.json`: sunlight colour/intensity, diffuse fill and fog colour.
- `tools/blender/art_direction_geometry.py`: `balanced-downtown-geometry-v5`,
  source-derived geometry with bounds, boundary and UV checks. `--category Props`
  can refresh that category after a complete prior generation.
- `tools/import-nfs-art-direction.ps1`: full private-asset regeneration and qualification.
- `tools/nfs-world.ps1 -Action ArtDirectionMapCapture`: reproducible four-direction
  survey across the physical district; positions are recorded in `poses.json`.
- `tools/benchmark-nfs-world.ps1 -Visible -RequireMains -Game -Driving`: measure the actual
  default Windows game. Add `-FrameRateCap 30` for the target pacing check.

The open-cylinder Blender fixture verifies boundaries, face connectivity, UV
corners and displacement. Runtime diffuse-fill tests verify restoration and
that a replacement probe is not overwritten. Custom ambient mode and scene-load
callbacks retain the host's diffuse probe when additive content loads; a PlayMode
regression covers switching away from and back to the host. This addresses the
native player's initial failure, where a configured probe was overwritten during
environment updates (as described by [Unity's ambient-probe API](https://docs.unity.cn/ru/current/ScriptReference/RenderSettings-ambientProbe.html)).
The native benchmark checks that
the configured fill is actually active, alongside rendered frames, GPU timings,
road support and runtime LOD use.

## Current qualification

Functional checks passed on 4 October 2026: **45 Python, 20 EditMode
and 24 PlayMode tests**, strict default Windows build, 199 accepted geometry
groups and 474 shaped prop forms. Near visible geometry including the car is
3,129,624 triangles. All 13,834 road samples passed (maximum error 0.765 mm).
The 146 active LOD groups retain collision and road meshes. Both occlusion
datasets were rebaked. All 942 derived textures retained dimensions/alpha.
Original artwork/license/metadata verification passed; all 86,417 accepted
inventory files remain present and unchanged.

The refreshed 68-view survey matches the same 17 source-road positions and
angles. See `artifacts/NfsWorld/DeveloperBArt/whole-map-style-comparison.jpg`.
The actual built game confirms its diffuse fill is configured and active,
distance meshes are used, no LOD targets are statically batched, every driving
sample retained road support and all views provided GPU timings.

**Mains performance qualification is pending.** Windows reported AC status 0
(battery) in the fresh uncapped runs. These results are provisional; previous
v4 capped results do not qualify this build. The required four mains runs will
use `-RequireMains` after AC power is restored. Do not treat the new private
snapshot as performance-qualified yet. Checked functional evidence is
`DeveloperBArt/functional-qualification-v5.json`.

The treatment covers the complete current playable Downtown map. Future
neighbouring districts and exact concept-map reconstructions remain outside
this imported map; dense geometry is retained where preservation guards reject
simplification.

## Private contribution

The v5 assets and qualification status above accompany this private contributor snapshot:

`project-alabama-work-nfs-world-developer-b-art-direction-0.0.20261004115710729459.zip`

ZIP SHA-256: `319d4c1b536fd84603230179aeb32d89504ca01decebd8f933045c7e6ea52b85`. Size: **4,050,301,719 bytes**.
The full snapshot contains **105,050 files**, content ID
`986f47a716c87b0e3ca47d54e157c5d21171b6783a1af109d5ce458d5855eeba`. Against accepted 0.1.0, it contains **18,633
additions, zero modifications and zero deletions**.

The additions are confined to Developer B's `ArtDirection` Unity assets,
derived texture/geometry sources, editable Blender files and prior `Runtime`
contribution. Preserve metadata and integrate these owned additions rather than
replacing the receiving workspace wholesale. Share the ZIP and its `.zip.sha256`
sidecar privately with this code revision.

The separate private review archive is
`artifacts/NfsWorld/DeveloperBArt/developer-b-whole-map-review-evidence.zip` with
its SHA-256 sidecar. It includes staged and final captures, all 68 survey pairs,
test/build logs, geometry/UV/collision/texture/LOD records and native benchmarks.
The earlier v4 evidence ZIP remains historical.

Accepted asset pins remain `base` 1.0.0 / `nfs-world` 0.1.0. Developer B exports a
contributor candidate; Developer A retains accepted-pack publication. Public
Git contains code, recipes and records. Runtime artwork and generated source
files are carried by the private candidate.

# Developer B Downtown art direction

Prepared on 3–4 October 2026 from updated `main` / `origin/main` at `fe2e19f`,
on `codex/developer-b-art-direction`. The current playable Downtown game uses
Developer B's derived geometry, materials and distance LODs by default.
Developer A retains neighbouring districts and accepted pack publication.

## Reference application

All guides, prompt records and 19 images in
`docs/art-direction/2026-10-02-nfsmw` informed this pass. The balanced safehouse,
lighthouse and stadium studies set the medium-detail target; `style-reference.png`
supplies atmosphere. The six supplemental studies guide roads, facades, foliage,
underpasses, intersections and waterfront depth. The polished and coarse sets
bound the rejected extremes; first-pass images provide composition context.
The adaptation guide calls for the imported Downtown layout, dimensions and
landmarks to remain authoritative. The conceptual locations are not additional
playable districts or measured layouts.

| Reference concern | Current treatment |
| --- | --- |
| Substantial architecture with moderate openings | Guarded coplanar cleanup on 192 building mesh groups; source window grids, trim, openings and broad masses retained. Hard environmental corners remain faceted. |
| Ordinary, simpler street furniture | 128 closed newspaper-box and waste-bin forms projected onto angular coarse surfaces, with at most 8 cm displacement and exact triangle connectivity and UV corners. Five prop mesh groups accepted, including these four reshaped groups. |
| Matte roads, curbs and legible markings | 39 derived road albedos plus restrained world-space grain on 21 explicit pavement layers in two array materials. Markings and source road shape retained. |
| Moderate concrete, brick, glass and metal detail | Broad wear and material transitions retained while fine noise is reduced across nine texture families. Authored atlases, signs, effects and data maps remain protected. |
| Irregular autumn foliage, terrain and water | Alpha-aware foliage treatment preserves crown gaps and natural source silhouettes; earth/water families retain broad variation. No tree trunks passed the shape-rebuilding guards. |
| Restrained vehicle reflections | Four derived vehicle materials reduce body/glass gloss while retaining the smoother source body and wheel geometry. |
| Near/far depth and industrial silhouettes | 146 spatial groups use validated distance meshes at 80/200 m. Original shadows, landmark structure, skyline, culling and reviewed atmosphere remain. |

## Geometry, textures and runtime

The public geometry recipe is `balanced-downtown-geometry-v4`. It starts from
original editable district meshes and exports separate FBX files and editable
Blender copies. A 1 mm weld reconnects submillimetre source seams. Category-specific
cleanup protects open curves, bounds, material/UV regions, structural roads and
bridges, alpha cards and readable signs. Flat normals alone do not count as a
triangle reduction.

Unity accepted **197 changed mesh groups** (192 buildings and five props) and
rejected 11 candidates. It also applied faceted normals to **2,309 exact source
mesh groups**, with category and smooth-silhouette exclusions. Source visual
meshes assemble into **2,916 spatial batches / 53 texture-array materials**.
Near map geometry is **3,126,664 triangles**, versus 3,132,972 in the imported map
(6,308 fewer, 0.20%). It is **9,407 triangles higher** than the accepted ArtPass
(3,117,257), because this pass restores detail that more aggressive simplification
failed to preserve. Including the car, the standalone scene has 3,129,624 visible
triangles. These are conservative style edits, not a large geometry optimization.

UV validation covers texture interpolation, not just seams. An earlier candidate
removed an interior gradient and erased facade window grids; that candidate was
rejected. The final recipe protects differing affine UV gradients and verifies
source interpolation within 0.0002 UV units. Blender fixtures exercise atlas
rejection, continuous gradients, safe affine cleanup, and actual closed-solid
vertex projection with unchanged UVs/connectivity. Final normal and near-level
captures preserve the facade grids and trim.

LOD generation applies the same UV checks, material-layer retention, bidirectional
surface-distance checks and open-boundary/bounds validation. Of 2,307 eligible
spatial groups, **146** accept a reduction. Their aggregate triangles are
2,928,534 near and 2,925,003 at both middle and far: only **3,531 additional
triangles** saved. The two distance levels currently produce the same accepted
counts. The 4,326 rejected candidates include candidates with no reduction;
rejection is expected under these guards. Colour LOD ownership is independent
of colliders and exact retained shadow proxies, and is scoped to additive content.
Active distance meshes are excluded from Unity static batching so runtime buffer swaps use their own mesh buffers. Unity copies static-batched mesh data at build time ([Unity 6.3 API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/StaticBatchingUtility.Combine.html)). The rendered benchmark verifies zero statically batched LOD targets and actual reduced-mesh selection. Disabling the owner restores near meshes. The final update regenerated 20 changed
inputs and reused 2,287 byte-identical inputs; the cache record checks source
hashes and output headers, counts and sizes, and Unity verifies every imported input against the
current scene mesh. A full uncached public regeneration remains available.

The schema-2 material recipe lists exact source IDs: **942** saved albedos
(concrete 109, masonry 55, road 39, props 127, facade 83, foliage 29, ground 18,
water 5, other surfaces 477), with **20** protected bindings preserved.
Dimensions and alpha remain exact; maximum saved mean brightness drift is
0.00777165 on a 0–1 scale. The material builder overrides 971 material bindings.
The four vehicle materials are derived assets; source materials/geometry stay
unchanged. Road grain source, exact generation prompt and sampling recipe stay
in the private source contribution.

The default Windows build and Unity Play menu now enter the styled additive
runtime. The enabled build scenes are its persistent host and Downtown content;
Foundation, HandlingCourse and DistrictLoop remain registered review scenes.
Foundation setup preserves this entry configuration. Review tests load their
scenes explicitly through the editor instead of relying on enabled build entries.
The existing six unresolved exits remain closed.

## Verification and visual review

| Check | Result / private evidence |
| --- | --- |
| Python suite | 45 passed (`DeveloperBArt/python-tests.log`); Blender-specific geometry/UV/projection fixtures also passed during generation. |
| Unity EditMode | 18 passed, zero failed/skipped (`artifacts/EditTests/results.xml`). Includes open-boundary, hole and submillimetre seam guards. |
| Unity PlayMode | 23 passed, zero failed/skipped (`artifacts/PlayTests/results.xml`). Includes driving/collision, six barriers, additive lifetime, LOD distances/restoration and culling-cache invalidation. |
| Collision | 13,834/13,834 samples on 1,158 source road meshes; maximum discrepancy 0.765 mm. Source mesh and exit collider signatures match exactly; 2,201 mesh colliders / 3,381,268 collision triangles. |
| Shared rendering | Sun colour/intensity/rotation/shadows and atmosphere profile match the accepted scene exactly (`scene-validation.json`). |
| Occlusion | Standalone and additive configurations baked. Cutout cards are excluded as solid occluders. |
| Windows builds | Strict standalone, additive runtime and default game builds succeeded with zero build errors. |
| Default entry | Actual Foundation Setup followed by GameSetup passed. Build-settings SHA-256 stayed identical; only the two styled runtime scenes are enabled (`default-game-validation.json`). |
| Original artwork / accepted inventory | 97 recorded files across five assets, license evidence and Unity metadata verified. Accepted NFS inventory: 86,417 files, zero missing/modified; base dependency verified. |

Clean final before/after captures cover chase camera, two source-road views,
oblique facade, shadowed lower roadway, barrel, hydrant, newspaper boxes,
waste bins and overview. The actual source-road/passage and prop poses are shared
between variants. Comparisons retain identical lighting and disable baked
occlusion in both variants; gameplay keeps its baked occlusion. The inspected
images preserve road paint, signage, facade grids, crown gaps and passages, and
show the angular bin/box forms. They are an adaptation of the repository's
medium-detail direction, not an exact recreation of the concept compositions.

Private images are under `artifacts/NfsWorld/Captures/ArtDirectionBefore/` and
`ArtDirection/`. `DeveloperBArt/game-style-comparison.jpg` is the compact four-view
comparison; `before-after.jpg` contains the full comparison. Earlier material-only
measurements and rejected geometry captures are historical diagnostics, not
qualification of these final assets.

## Mains performance

All four fresh visible-player runs verified mains before and after measurement
and the same Power saver plan, with Unity and Blender closed. Hardware: Ryzen
7 5700U / Radeon integrated graphics, Direct3D 11, **1920×1080 output / 1440×810
internal rendering**, FSR1 at 0.75 scale. The accepted runtime player is the
retained qualified baseline; revised players were rebuilt from the final assets.
Lighting and shadows remain enabled.

Each run samples five stationary views (120 warm-up / 360 measured frames each)
and nine two-second high-speed traversals: three source-road corridors repeated
three times with initial speed 126 km/h. Every measured driving frame retained
road support. View/corridor positions and rendering settings match; the spawn
view allows 1 cm for suspension settling, while other poses match within 1 mm.

| Variant / cap / sample | Mean FPS range | Frame p95 range (ms) | Maximum frame (ms) | Frames over 40 ms |
| --- | ---: | ---: | ---: | ---: |
| Accepted / uncapped / stationary | 62.28–91.81 | 12.19–17.20 | 32.74 | 0 / 1800 |
| Revised / uncapped / stationary | 58.14–86.52 | 12.82–20.14 | 44.44 | 2 / 1800 |
| Accepted / uncapped / driving | 65.56–84.79 | 12.79–16.63 | 36.54 | 0 / 1397 |
| Revised / uncapped / driving | 63.72–83.08 | 13.11–18.12 | 28.51 | 0 / 1362 |
| Accepted / 30 cap / stationary | 29.78–30.00 | 33.34–33.48 | 86.18 | 5 / 1800 |
| Revised / 30 cap / stationary | 29.91–30.00 | 33.34–33.39 | 50.88 | 4 / 1800 |
| Accepted / 30 cap / driving | 29.99–30.00 | 33.34–33.46 | 38.04 | 0 / 540 |
| Revised / 30 cap / driving | 29.99–30.00 | 33.34–33.50 | 34.21 | 0 / 540 |

Camera-render counts (accepted / revised) are
4877 / 4842 uncapped and
4020 / 4020 capped.
Every sample recorded nonzero GPU timings. Both revised runs verified 146 LOD
targets, zero in static batches and 143 using reduced meshes at the final viewpoint.

Unity allocated memory at uncapped report completion is
**1121.17 MB accepted / 1254.15 MB revised**
(**+132.98 MB**); this is total allocated memory, not isolated GPU
texture memory. The revised scene retains source shadow materials alongside
derived colour arrays and distance meshes.

These short sequential samples establish local target headroom and observed
pacing, not a performance gain or a guarantee for every street. Occasional long
frames must be judged from the table. Capped GPU timings can include frame-pacing
waits; use uncapped runs to assess rendering cost. They do not qualify the planned
long connected route. Raw reports/logs are
`artifacts/NfsWorld/player-{runtime,art-direction}-benchmark-driving{,-cap30}.*`;
`DeveloperBArt/performance-comparison.json` records checked conditions and timings.

## Reproduction and private handoff

Restore the pinned private packs and this contributor candidate with its code
revision before opening the styled scene. Accepted pins remain `base` **1.0.0**
and `nfs-world` **0.1.0**; Developer B's publisher list remains empty.

With Unity and Blender closed, `tools/import-nfs-art-direction.ps1` builds guarded
geometry and derived textures, assembles visual batches, generates/imports LODs,
builds additive content, bakes both occlusion configurations, runs Unity suites,
captures comparisons, builds all three strict Windows players and configures the
default game. Regeneration also requires original district Blender files,
`artifacts/NfsWorld/inventory.json`, `art-meshes.json`, the reviewed exit manifest
and lighting-study inputs from the accepted pipeline. The private candidate
contains derived assets that can be used without regenerating them.

Use **Alabama → Play → Open Styled Game** or open
`Assets/Alabama/Art/Maps/NfsWorld/ArtDirection/Runtime/DistrictRuntime.unity`.
`./tools/unity.ps1 -Action Build` writes `builds/windows/Alabama.exe`.
The qualification variants are `builds/nfs-world-art-direction/Alabama.exe` and
`builds/nfs-world-art-direction-runtime/Alabama.exe`.

New contributor snapshot prepared after final qualification with
`python tools/asset_handoff.py contribute --pack nfs-world --label developer-b-art-direction`:

`project-alabama-work-nfs-world-developer-b-art-direction-0.0.20261003230155458880.zip`

ZIP SHA-256: `34a6fece51db09d0484c653dbbfaf3ef6cd7971cc55485509ff611e26be7e3dc`. ZIP size: **4,051,802,440 bytes**.
Share the ZIP and its `.zip.sha256` sidecar privately with the code revision.
The full snapshot contains **105,050 files / 10,123,594,292
uncompressed bytes**, content ID `1b7b5aabad4dfecaecf352867748c7a29d6c7f050c4a54a9df2f6d53b7b37cdc`. Against accepted 0.1.0:
**18,633 additions, zero modifications and zero deletions**.

| Owned additions for integration | Files |
| --- | ---: |
| `unity/Assets/Alabama/Art/Maps/NfsWorld/ArtDirection.meta` and `ArtDirection/**` | 18,601 |
| `source-art/maps/nfs-world/art-textures/art-direction/**` | 3 |
| `source-art/maps/nfs-world/art-direction-geometry/**` | 6 |
| `source-art/maps/nfs-world/blender/ArtDirection/**` | 5 |
| Prior B contribution: `NfsWorld/Runtime.meta` and `Runtime/**` | 18 |

Preserve received metadata and integrate owned additions rather than replacing
A's workspace wholesale. Inventory: `DeveloperBArt/private-contribution-inventory.json`.
The private `DeveloperBArt/developer-b-art-direction-review-evidence.zip` bundles 68 files:
final captures, tests, builds, collision/texture/LOD/startup validation, benchmark
reports and this record; verify its separate SHA-256 sidecar when transferring.

Developer A must privately review/integrate owned additions before publishing an
accepted asset release. The candidate and review evidence remain ignored by Git;
only code, recipes, settings and this record belong on the review branch. No
neighbouring district connection or new accepted pack publication is claimed.

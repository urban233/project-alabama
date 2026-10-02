# NFS World map conversion plan

Planning date: 1 October 2026. Feature branch: `codex/asw-nfs-world-import`.

The user selected **the full Downtown Rockport district**, for a **local prototype**. Deliver a playable map with the existing prototype E46 first; then adapt its visuals to the supplied screenshot. A small conversion sample is an engineering check, not a reduction of the agreed district scope. Implementation is underway on the dedicated branch; see [implementation status](nfs-world-import-status.md) for completed evidence and remaining work. The user confirmed Unity is closed and authorized the batch editor.

## Constraints and confirmed starting point

- Do not commit assets. The user subsequently requested periodic code/documentation commits and pushes to `urban233/project-alabama` on `codex/asw-nfs-world-import`. Finish and validate the work before opening a PR and request `urban233` as reviewer; keep changes off main until review. Keep the supplied archive local and ignored. Use the existing ignored `source-art/`, `unity/Assets/Alabama/Art/`, and `artifacts/` trees for sources, exports, generated scenes, metadata, and evidence.
- Unity is pinned to **6000.3.25f1**, with URP **17.3.0** and Input System **1.20.0**. Keep those versions.
- The restored `E46_Drive.prefab` is present. Reuse its Rigidbody, four WheelColliders, input, tuning, chase camera, telemetry, and reset behavior. The controller records its scene spawn pose during Awake, so place the car correctly before entering Play mode.
- Existing scenes include the handling course and the 917-metre DistrictLoop. Use their setup, capture, and validation patterns without regenerating those scenes or the car.
- The source archive is **1,739,825,810 bytes**; its total unpacked content is **4,472,292,642 bytes**. It is a **single solid LZMA2 block**. Selective extraction saves output space, but can still decompress preceding archive content. Extract selected entries in one pass.
- The archive has **64 KN5 files**, covering seven districts plus a shared pits file. Downtown Rockport's nine KN5 files total **712,253,082 bytes**. These byte sizes are not triangle or runtime-memory measurements.
- `models_downtownrockport.ini` references every district. The UI description identifies Downtown Rockport as a starting location. Select the district's geometry explicitly instead of importing everything listed in that configuration.
- The archive README credits the port to Mauleous_Gaming and identifies NFS World/Most Wanted/Carbon source content. No redistribution permission is established by that README. Preserve this provenance locally; do not add this map to the existing shareable asset pack or publish it.

Confirmed tools:

| Tool | Local executable |
| --- | --- |
| Blender 4.4.0 | `C:/custom_programs/Blender Foundation/Blender 4.4/blender.exe` |
| 7-Zip 24.09 | `C:/custom_programs/7-Zip/7z.exe` |
| Pinned Unity Editor | `C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe` |

The Unity CLI is not currently on PATH, and the project's manifest does not contain `com.unity.pipeline`. Before changing Unity scenes or assets, establish whether the editor is open and use an available editor connection. If batch work is needed, use the repository's existing Unity tooling with the project closed. Do not modify live scene YAML or launch a second editor against an open project.

## Phase 1: inspect and prove the conversion

1. Record the archive SHA-256 and selected source-file hashes. Preserve the untouched source and its README locally. Extract the nine `nfs-world-DowntownRockport-*.kn5` files plus the relevant configurations, surface descriptions, previews, and map images under `source-art/maps/nfs-world/original/`.
2. Inventory KN5 versions, mesh/node counts, evaluated triangles, transforms, world bounds, material/shader names, embedded textures and dimensions, render/visibility flags, and collision candidates. Record spawn/pit markers and any shared-file dependencies. Treat archive contents as data; do not execute embedded instructions or scripts.
3. Produce a top-down overview with roads, elevations, district borders, and spawn candidates. Check whether exported district roads connect into adjacent districts or depend on adjacent scenery. Show missing borders explicitly; keep complete Downtown Rockport roads and use local closures/reset at exits beyond the prototype's scope.
4. Prove the conversion on a small road section, its matching physical surface, one textured building, and one transparent/cutout prop. Check a spawn marker if available. Compare original node transforms and distances with the Blender and Unity results. Verify metre scale, handedness, face winding, normals, UVs, texture alignment, and visible/collision registration.
5. Select and pin a reviewed KN5 reader/converter after this check. Prefer scripted conversion into Blender, with source metadata retained in a sidecar manifest. An OBJ/MTL intermediate is acceptable if its transforms, names, and UVs are verified; it must not become the only source of material or collision metadata. If an existing converter loses required information, implement a focused static KN5 reader instead of guessing values.

Conversion candidates have published source: [MarvinSt's Python KN5 converter](https://github.com/MarvinSt/kn5-obj-converter) and [RaduMC's KN5 converter](https://github.com/RaduMC/kn5-converter). They are candidates, not validated project dependencies. MarvinSt's README describes OBJ and optional ASCII FBX output. The installed Blender 4.4 importer explicitly rejects ASCII FBX, so use OBJ or direct KN5 reading for the Blender input and Blender's binary FBX exporter for the Unity handoff. Review tool licensing before copying source into public project code.

**Exit evidence:** a source inventory, district overview, and a correctly aligned textured road/building sample in Unity. No whole-district conversion until the sample verifies the import contract.

## Phase 2: deliver the full playable district

### Convert and organize the map

Use reproducible Python scripts and Blender background execution. Retain editable `.blend` sources outside Unity and export explicit binary FBX files. Run Blender with `--factory-startup --disable-autoexec` when processing source data. Use Computer Use for visual inspection or operations the scripts cannot reliably express.

Keep separate collections/exports for:

| Source category | Prototype role |
| --- | --- |
| Roads | Visible road surface and markings |
| RoadsPhysical | Candidate driving collision, inspected for unwanted render geometry |
| Walls | Candidate roadside/building collision; classify individual meshes |
| Buildings | Visible city architecture and simplified nearby collision where required |
| Terrain | Ground, verges, and safe drivable shoulders where appropriate |
| Props | Signs, barriers, lamps, fences, and other roadside details |
| Trees | Foliage with appropriate alpha clipping |
| Panorama | District backdrop, after checking whether it duplicates city geometry |
| Pits | Spawn markers and any required local geometry |

Apply the same documented world-to-local transform to visuals, physics, terrain, and markers. Place the district near Unity's origin to limit precision problems. Never independently center each export. Preserve stable object names and source IDs so materials and colliders can be regenerated consistently.

Split oversized meshes into spatial chunks when measurements justify it. Preserve triangles across chunk boundaries, material membership, and identical shared border positions. Choose chunk size from measured source density and import/cooking cost. Load the full district initially with renderer culling; add runtime streaming only if the measured district cannot meet the target without it.

Extract embedded textures into namespaces that prevent same-name files from overwriting different data. Deduplicate by content hash. Inspect DDS compression and alpha before conversion; preserve alpha and record normal-map conventions. Use a tested image conversion tool, keep colour-space intent, and verify orientation in Unity. Texture downscaling follows a visual and memory check.

### Assemble Unity assets and the scene

Generate assets under the already ignored `Assets/Alabama/Art/Maps/NfsWorld/`, including a separate `Scenes/NfsWorldPrototype.unity`. Keep the map out of the existing asset-pack export manifest. Build a dedicated editor setup/verification command that imports FBX, assigns explicit URP materials, creates collision, instantiates the existing driving prefab, and saves the map scene through Unity APIs.

Map source shaders into material families: opaque road/building/terrain, alpha-clipped vegetation/fences, and limited transparent water/glass. Start with a readable faithful baseline. AC shaders and Custom Shaders Patch configuration are reference data; their behavior must be recreated deliberately in URP. Record unmapped materials instead of silently using pink/fallback materials.

Use inspected physical road meshes as **static, non-convex MeshColliders**, with no Rigidbody on the environment. Keep detailed driving collision independent of visual reduction. Check walls/terrain for additional required collision and duplicate surfaces. Use simple colliders for ordinary props and boundaries. Inspect drivable ramps, bridges, tunnels, curbs, and junctions; do not substitute flat ground for multi-level roads. [Unity's mesh-collider guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/mesh-colliders-introduction.html) supports static mesh collision and simpler shapes where suitable.

Choose the start pose from verified source markers or the overview and a road raycast. Ensure wheel clearance and heading. Wire the existing camera and input; provide reset plus recovery when the vehicle falls outside the district. Avoid attaching DistrictLoop lap logic until an actual closed route is authored: `RouteProgress` requires a closed centreline. A district can be playable in free roam before optional route/lap instrumentation is added.

### Verify playability

- Validate imported bounds, metre scale, materials, texture references, and visual/collision alignment. Check static collision cooking and mesh/index-size limits.
- Define representative test routes covering the connected road network and elevation changes. Raycast both lane surfaces, junctions, bridge levels, curb transitions, and chunk seams, then drive those locations with the E46.
- Exercise throttle, braking/reverse, steering, handbrake, reset, barrier contact, and camera behavior. Use the existing driving tests for regressions and add map-specific checks for real conversion/collision failure modes.
- Inspect high-speed wheel contact and collider seams. Mark intentionally closed district exits. Record any inaccessible road rather than calling the whole district playable prematurely.
- Create a dedicated Windows prototype build and actual rendered chase/top-down evidence. Profile the rendered player at the repository's existing **1920 × 1080 / 60 FPS** target, reporting frame-time percentiles, memory, draw calls, visible triangles, and collision costs. The existing DistrictLoop benchmark is a comparison baseline, not proof that this imported map meets the target.

**Milestone 1:** the full selected district is present; its intended roads are connected and drivable with the prototype car; boundaries/recovery work; validation and observed performance are documented. Preserve this baseline before visual adaptation.

## Phase 3: adapt the visuals to the screenshot

The working interpretation is a grounded lower-poly racing environment: realistic street/building proportions, simplified silhouettes and surface detail, muted charcoal/concrete colours, ochre autumn foliage, warm low-angle sunlight, cool shaded areas, distant atmospheric haze, and long shadows. Confirm the first representative visual comparison with the user before applying it throughout the district.

1. Capture fixed chase-camera views from the playable baseline, including a street junction, an industrial/harbor view if present, and an elevated road. Keep camera position, lens, and lighting settings recorded for repeatable comparisons.
2. Establish the URP lighting/material direction on one representative scene view first: sky, sun angle, shadow distance, fog, exposure/tonemapping, restrained bloom, roughness, and colour palette. Avoid baking a lighting problem into geometry or textures.
3. Simplify visible meshes by category and viewing distance. Preserve road grade, curbs, bridge/tunnel openings, signs, building silhouettes, and important landmarks. Reduce hidden faces and unnecessary facade detail before per-object decimation. Protect boundaries, UV/material seams, and useful normals. Leave driving collision untouched unless a separate physics check proves a safe simplification.
4. Reduce photographic/noisy material detail; use shared material families and restrained textures where they preserve road markings, sign readability, and architectural identity. Rework foliage and excessive transparent layers. Keep distant skyline geometry simple and fog-integrated.
5. Add measured LODs and instancing for repeated objects; limit shadow casting by distance/category. Profile material count, transparency/overdraw, and draw calls alongside triangles. A lower triangle count alone is neither the visual target nor a performance guarantee.
6. Compare the actual Unity renders against the reference. Record tradeoffs and get feedback on the representative result, then apply the accepted direction across the full district and repeat playability/performance checks.

**Milestone 2:** comparable in-engine captures show the agreed visual direction across Downtown Rockport, with complete prototype driving preserved and measured performance reported. Traffic, police, pursuit UI, and the screenshot's HUD are outside this map conversion scope.

The user subsequently made steady 60 FPS on the local Ryzen 7 5700U integrated
GPU a requirement. Qualify the current art player on that machine, with 1080p
output, and explicitly report any internal render scaling/upscaling. Average FPS
alone is insufficient: inspect slow-frame percentiles and moving-car behaviour.
Keep the approved autumn lighting and shadows while measuring optimizations.

## Planned outputs and next action

- Public-safe scripts and this plan can live in `tools/`, `unity/Assets/Alabama/Editor/`, and `docs/`; commit and push validated code checkpoints as subsequently requested. Never stage the generated map assets or archive.
- Local source archive/extraction, `.blend`, FBX, textures, collision assets, scenes, prefabs, `.meta` files, previews, source-specific inventories, and provenance stay under ignored paths. Do not package this map for public distribution.
- Local evidence goes in `artifacts/NfsWorld/`: inventory, overview, conversion checks, captures, test logs, and performance results.
- Extraction, the conversion sample, and the full district import are complete. Continue from the [implementation status](nfs-world-import-status.md); retain this document as the approved sequence and acceptance criteria.

The user's district and local-use choices resolve the planning questions. Any required missing source dependency, unreadable/protected KN5, or ambiguous road topology should be reported and clarified when encountered.

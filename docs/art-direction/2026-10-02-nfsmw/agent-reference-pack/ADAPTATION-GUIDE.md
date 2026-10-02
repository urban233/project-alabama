# Adapting the imported NFS World map to the middle-ground style

This pack extends the three middle-ground location references with six focused studies. Keep realistic proportions, angular readable architecture, modest texture detail, irregular autumn foliage, muted industrial colors and restrained reflections. The new images describe appearance at different viewing distances; they do not prescribe a new map layout.

## Reference authority

- Primary scene/fidelity targets: [safehouse](../balanced/rosewood-safehouse.png), [lighthouse](../balanced/camden-lighthouse.png), [stadium](../balanced/hickley-field.png).
- Supporting atmosphere and palette: [original screenshot](../style-reference.png).
- These six supplementary studies explain materials, geometry and lighting in context. If a close foreground patch looks overly detailed, follow the primary images at gameplay distance. Tiny cracks, bolts, loose leaves and grass blades need not become modeled objects.
- The older polished and coarse sets are comparison extremes, not production targets. Avoid glossy photorealism and featureless polygon blockouts.
- Generated studies are visual concepts, not captures of the imported map, UV atlases, tileable textures, normal maps or measured models. Use real imported geometry for layout, dimensions and landmark shapes.

## Read these existing project records first

The user confirmed that the Assetto Corsa NFS World map is already imported into Unity, with Downtown Rockport selected. The local source archive is `asw-nfs-world.7z`.

- [Import status](../../../../docs/nfs-world-import-status.md): existing scenes, approved lighting, conversion contract and historical validation.
- [Pipeline README](../../../../tools/nfs_world/README.md): conversion, visual/art passes, texture bindings, capture and verification commands.
- [Blender visual authoring](../../../../tools/blender/art_nfs_world.py) and [conservative planar simplification](../../../../tools/blender/simplify_nfs_world.py).
- [Texture bake](../../../../tools/blender/bake_nfs_textures.py): existing alpha-weighted edge-preserving smoothing and joint brightness quantization.
- [Art assembly helper](../../../../tools/import-nfs-world-art.ps1): export, texture processing, assembly and checks.
- Imported art pass: `unity/Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldArtPass.unity`.
- Original editable district: `source-art/maps/nfs-world/blender/District/`. Separate authored copies: `source-art/maps/nfs-world/blender/ArtMeshes/`.
- Existing replacement bindings: `unity/Assets/Alabama/Art/Maps/NfsWorld/ArtPass/textures.json`.

These paths and implementation details were inspected for this handoff. No Unity/Blender regeneration, asset conversion, new capture or runtime test was performed for this reference pack. Historical verification in the status document is not a new test result. The documented `artifacts/NfsWorld/Captures/` directory was not present during this reference-pack task.

## What each new study teaches

| Image | Read it for | Apply it to imported assets |
| --- | --- | --- |
| [01 Road, curb and barrier](01-road-curb-barrier.png) | Road/paint/concrete separation, restrained roughness and plausible curb edges | Keep road grade and edge geometry; quiet albedo noise while retaining markings and broad repairs |
| [02 Facade, glass and metal](02-facade-glass-metal.png) | Window/wall balance, moderate brick seams, simple corrugation and dark glass | Retain facade openings and original UV regions; simplify detail within each region |
| [03 Foliage, rocks and ground](03-foliage-rock-ground.png) | Broken crown silhouettes, exposed branches, angular rock planes and near/far density | Restyle existing cards/meshes with varied ochre/russet groups; keep cutout behavior and natural crowns |
| [04 Underpass, props and shadow](04-underpass-props-shadow.png) | Broad structural masses, ordinary street furniture and readable shaded materials | Preserve drivable openings; simplify only suitable visual props; avoid crushing shadow detail |
| [05 Downtown intersection](05-downtown-intersection.png) | How material/geometry choices combine at ordinary viewing distance | Assess the full imported block from the gameplay camera; retain actual map topology and landmark silhouettes |
| [06 Waterfront infrastructure](06-waterfront-infrastructure.png) | Water restraint, industrial silhouettes and atmospheric separation | Reduce distant detail with appropriate LOD/culling while retaining cranes, seawalls and skyline identity |

## Preserve the imported map's useful structure

Keep world transforms, district origin, metre scale, mesh identities, UV layouts and material slots. Work on derived visible meshes/materials through the existing pipeline. Preserve the source district and earlier comparison scenes. Keep original physical road/collision geometry, bridge clearance, tunnel/garage openings, road markings and readable signs.

The existing project rejected collapse decimation that opened facades. Start with category-specific edits and conservative coplanar cleanup. Preserve boundaries and UV/material seams; changing the whole district to one decimation ratio or forcing flat shading everywhere will not produce this style. The current Blender/Unity pipeline already has bounds and boundary guards; retain them.

Closed simple props may be rebuilt with fewer sides. Open facades, cards, bridge decks and irregular connected meshes need different treatment. Keep vehicle bodies and important cylindrical silhouettes smoother than rocks and structural corners. Flat shading changes normal treatment; it does not by itself reduce triangle counts.

[Blender's Decimate documentation](https://docs.blender.org/manual/en/5.0/modeling/modifiers/generate/decimate.html) describes planar reduction and delimiters for normals, materials, seams and UVs. The project's existing guarded implementation is the starting point for this map.

## Texture adaptation: simplify information, preserve layout

1. Identify source materials by inspecting their actual images and meshes. A texture name alone is insufficient: an atlas can combine windows, wall panels, signs and transparent regions.
2. Preserve atlas dimensions, UV region positions, tile alignment, alpha masks, source IDs and replacement bindings. Simplify detail inside regions. Do not use a generated facade image as a replacement atlas without a separate layout-aware texture task.
3. Use the existing bake for moderate removal of fine noise. Keep window edges, mortar structure, large material transitions, broad repairs and directional features legible. Avoid indiscriminate blur, nearest-neighbor pixelation and deep posterization.
4. Keep the project's joint brightness quantization approach. Independent RGB channel quantization previously introduced green/magenta bands in neutral glass.
5. Keep hand-authored replacement textures and protected signs/markings/effects out of the generic bake, as the current implementation does. Treat foliage alpha with its existing alpha-aware process.
6. Start with one representative material from each family. Compare source, current art pass and revised treatment on the same mesh under the same illumination before applying the recipe broadly.
7. Keep source-resolution textures initially. Adjust resolution only after observing the actual texel density and gameplay distance; shrinking every texture to a fixed size is not a style recipe.

The existing bake uses different treatment for road/terrain, foliage and other materials. Its current recipe is a baseline to tune visually, not a guarantee that every asset already matches these references.

## Material and lighting decisions

| Family | Keep | Adjust toward this style |
| --- | --- | --- |
| Road/paving | Markings, curb contact, broad wear and dry rough response | Reduce distracting grain and dense crack noise; retain enough rough texture to avoid flat polygon patches |
| Brick/concrete | Wall regions, large seams, building mass and local stains | Lower small-scale contrast; make grime sparse; let the broad wall color remain visible |
| Windows | UV window grid, dark pane grouping and facade identity | Quiet photographic reflections and false interior detail; keep limited glass highlights |
| Metal/props | Recognizable silhouette, paint/rust distinction and functional parts | Sparse texture-only wear; moderate facets; remove tiny geometry only when visual/collision constraints allow |
| Foliage | Crown gaps, branching silhouette and alpha cutout | Moderate angular clumps and grouped autumn colors; avoid crystalline crowns and uniformly dense orange blobs |
| Rock/terrain | Major contours, route margins and large planes | Broad faceting with restrained surface variation; avoid uniformly sharp triangulation over every surface |
| Water/skyline | Seawall scale, landmark silhouettes and depth layers | Subtle broad ripples/reflections and distant haze; avoid glittering water or excessive skyline window detail |

Retain the imported scene's already reviewed sun, ambient fill, exposure, fog and shadows while evaluating mesh/material edits. Changing lighting and albedo together makes comparisons ambiguous. Match material appearance first; a later lighting adjustment should be separately demonstrated.

The local style material builders currently use environment smoothness around 0.06. Treat that as an inspected project setting, not an instruction to force every surface to the same value. Glass and the existing car have different visual needs.

Treat color textures and data maps according to their actual semantics. Unity distinguishes sRGB sampling from linear data textures, and normal maps require the appropriate import type. Do not reinterpret Assetto Corsa shader channels by filename alone. See [sRGB texture sampling](https://docs.unity3d.com/ja/2022.2/ScriptReference/TextureImporter-sRGBTexture.html) and [Unity normal-map import](https://docs.unity.com/en-us/engine/6000.7/manual/materials-and-shaders/built-in/shader-built-in-configure-properties/standard-shader-texture-maps/material-parameter-normal-map/import) for those general rules; use this repository's pinned Unity version and actual shaders.

## Review with repeatable scene comparisons

Choose an existing downtown block containing roadway, curb/barrier, building facade, autumn foliage and a shaded structure. Capture it from:

- The actual chase camera at the same position, angle, output size and internal rendering scale.
- A close oblique facade/road-edge view that reveals UV and material problems.
- A shadowed passage view that checks readability and material response.

Capture the current scene before editing. Change one family at a time; record the material/mesh IDs, source files, recipe and actual rendered result. Do not change map layout to reproduce the invented composition of a concept image.

Judge these five points: silhouette clarity; moderate surface detail; natural autumn crown shapes; restrained material highlights; coherent warm light/cool shade and atmospheric depth. Examine both a thumbnail and the full image. A screenshot full of gritty microdetail has drifted toward the polished extreme; featureless walls and crystalline trees have drifted toward the coarse extreme.

Use existing ArtVerify/driving and texture validation after relevant asset edits. Recheck performance if rendering cost changes, retaining the documented 30 FPS local target and reporting internal scaling. This pack establishes no new performance result. Agent work on assets should follow the existing Unity connection/closed-editor requirements in the project plan.

## Copyable agent handoff

> The NFS World map is already imported; use the existing Downtown Rockport art pipeline. Read ART-STYLE-GUIDE.md and this ADAPTATION-GUIDE.md, then inspect the three balanced primary images and these six supplementary images. Match their medium-detail grounded style through selective visual mesh simplification, restrained alpha-aware texture treatment and modest material reflections. Preserve imported layout, transforms, UV regions, collider surfaces, signs, openings and the already reviewed lighting/shadows. Begin with one representative block and compare real in-engine captures under identical conditions. Record asset IDs and recipes so the treatment can be applied consistently. Do not reimport the entire source map, replace UV atlases with scene illustrations, or claim exact style/performance from generated concepts.

## Pack record

Generated with built-in image_gen, using only the three balanced primary images as style inputs. Exact prompts, reference paths and saved outputs are in `prompts.json`. At the user's request, this reference pack is versioned under `docs/art-direction/2026-10-02-nfsmw/`. Image paths in the prompt records are relative to that pack root. Runtime map assets, the original archive and private asset ZIPs stay in their separate asset workflow.

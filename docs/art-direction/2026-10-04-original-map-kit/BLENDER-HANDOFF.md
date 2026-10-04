# Blender handoff: original modular racing map kit

Build a new reusable environment kit from this pack. Use the images for visual design and the written manifest for construction. Match our middle-ground style: realistic scale, substantial angular architecture, modest rough texture detail, muted industrial colors, natural broken autumn foliage and restrained reflections.

## Source and design direction

These are new generated asset concepts inspired by the autumn/industrial mood and layered urban roads of Most Wanted and Carbon. They are not extracted game assets or copies of an Assetto Corsa map. Author new meshes, UVs and textures; no NFS World KN5 files, FBX meshes, texture atlases, original map layouts or mod-derived collision surfaces are needed.

The three images in `style-anchors/` show the chosen scene-level fidelity. They are style inputs, not assets to trace. The 12 sheets expose silhouettes, rear faces and useful module decompositions. A generated rear view can disagree with its front view; resolve the design coherently in Blender using the dimensions, interface rules and main silhouette below. Generated contacts, thicknesses, thin wire detail and interior props are illustrative rather than verified topology.

## What to model

`asset-manifest.json` contains 57 proposed asset definitions across roads/structures, buildings, boundaries/lighting, street/industrial props and nature. This is a production scope list, not a claim that 57 finished meshes exist.

| Sheet | Core modeling work |
| --- | --- |
| 01 road segments | Straight, curves, separate curbs/sidewalks, driveway curb, shoulder |
| 02 junctions | T and four-way hubs, compatible corner and crossing surfaces |
| 03 structures | Bridge deck, pier, tunnel portal/shell, retaining wall |
| 04 warehouse | Assembled warehouse plus window, wall, cargo bay and roof modules |
| 05 workshop | Garage bays, door inserts, service bay, roof and corner modules |
| 06 row buildings | Storefronts, upper window bays, plain walls and parapets |
| 07 midrise/parking | Office facade/corners and an open parking block with real apertures |
| 08 boundaries | Jersey barriers, guardrails, fences, gate and compatible end caps |
| 09 lighting/signs | Poles, signal mast, yard light, blank signs, chevrons and bollard |
| 10 street furniture | Shelter, bench, trash bin, utility box, hydrant and dumpster |
| 11 industrial props | Container, drum, pallet, crate, loading platform and HVAC |
| 12 nature | Two trees, shrub, rocks, boulder and adaptable terrain berm |

Assembled buildings in the sheets are examples of combining modules. Deliver their underlying reusable bays as well as assembled examples. Extra incidental objects in a sheet are optional unless included in the manifest. Optional roof details, hazard paint, leaf scatter, lights and barriers must be separate layers/objects so a street can be assembled without them.

## Shared units, axes and naming

- Metres. Blender local X is right, Y is forward/along a road, Z is up. `size_m` is X/Y/Z. The export pipeline must consistently map to the target engine's axes.
- Apply object rotation and scale after modeling; keep mesh placement and connector metadata coherent. Avoid negative-scale instances in final exports; generate mirrored geometry when needed.
- Use the exact manifest IDs for asset roots. Child mesh naming can follow `ID_VIS`, `ID_COL`, `ID_LOD1`, `ID_LOD2`; separate collision from presentation.
- Use empties named `SOCKET_IN`, `SOCKET_OUT` and additional junction sockets. Record position, direction and width in a JSON sidecar; do not rely on image-based measurements or FBX empties alone.
- Props pivot at the ground-contact centre. Facade bay pivot is lower-left at its front plane, with width along X, wall depth into +Y and height +Z. Fence/barrier spans run along X and use one endpoint as their assembly origin.
- Use a 4 m architecture/module grid and 0.25 m detail increments where appropriate. Road curves use their actual connector transforms, not arbitrary rounding to the grid.

## Road interface contract

The default drivable top surface is 8 m wide: two 3.5 m lanes plus 0.5 m shoulders on either side. Two separate 2 m sidewalks give a typical street assembly width of 12 m. The default curb rise is 0.15 m. Keep asphalt road top at local Z = 0; set sidewalk top at Z = 0.15 m. Drawn thick specimen bases are a sheet presentation device, not a requirement for a deep road slab.

A 16 m straight has entry at (0,0,0), exit at (0,16,0), forward direction (0,1,0) and carriageway edges at X = -4 and +4. Keep the exact end cross-section on each road connector; sidewalks/curbs use matching end profiles.

For a left curve of centreline radius R and turn angle theta, place its entry at the origin with forward +Y. The exit position is:

```text
X = R * (cos(theta) - 1)
Y = R * sin(theta)
Z = 0
exit forward = (-sin(theta), cos(theta), 0)
```

The manifest starts with R = 32 m and 45/90-degree turns, plus a broader R = 64 m 45-degree turn. Right curves mirror the X coordinate and turning direction. At R = 32 m and 90 degrees, the exit is (-32,32,0). Bend geometry must be analytic/spline-based with enough segments for smooth vehicle motion; road handling and collision take priority over visible faceting.

T/four-way hubs occupy a 24 m square with an 8 m carriageway at each connected port. Use hub-centred coordinates: west/east/north/south ports are (-12,0,0), (12,0,0), (0,12,0), (0,-12,0). The T hub uses west/east/north. Ports point outward; attach adjoining road connectors with their directions opposed. Curbs end at open ports and wrap closed corners; they must not block a junction entrance.

Keep road markings separate from the core geometry or in a dedicated surface layer, with consistent lane placement at joins. Do not model the illustrative road repairs as bumpy collision geometry.

## Structure and building contract

- Bridge deck has a 10 m overall top width, with an 8 m carriageway and margins. Keep parapets separate. Deck top shares road Z = 0 locally; piers have adjustable assembly height.
- Tunnel portals/shells have at least 8.4 m clear width and 5.5 m clear height; chamfers, lamps and beams must not intrude into that envelope. Real inside/underside faces are required.
- Road grade/elevated connectors should be designed as a separate compatible extension after the flat kit joins correctly. Do not simply tilt unmodified junction hubs.
- Facade bays are 4 m wide. Urban storeys are 4 m high; roof/parapet caps are separate above the stated wall/storey height. Assembled wall-height dimensions in the manifest exclude optional 0.6 m parapet caps.
- Warehouse/workshop roofs can use custom-height upper strips while retaining 4 m horizontal bays. Keep eaves, roof ends, corners and service backs modeled so buildings work from any route angle.
- Garage doors are separate inserts. Each drivable garage opening is at least 3.2 m wide and tall. A closed door must not be permanently baked into the open-bay wall module.
- Shop windows can be opaque dark glass with a shallow recess and simple interior suggestion. Only build traversable interiors where gameplay needs them.
- The parking block needs an authored ramp and safe guardrails before it becomes a drivable space; the sheet alone does not specify a valid ramp route.

## Geometry and materials

Build clean plane-based walls with real structural openings. Use geometry for silhouette, curb profiles, large supports, roofs, door frames and meaningful recesses. Put fine brick seams, broad stains, minor metal ribs and paint wear into original textures/trim sheets. Avoid modeling every brick, chain-link wire, leaf or bolt.

Use hard normals at structural corners and rock facets; use selective smoothing on appropriate cylinders and canopy branches. Keep the environment serious and human-scale. Do not flatten every surface to a single color, reduce tree crowns to crystals or fill surfaces with high-frequency photoreal noise.

Share a compact material family: Asphalt, Concrete, Brick, MetalDark, PaintedMetal, GlassDark, Wood, DirtGrass, FoliageOchre and FoliageRusset. Names in the manifest have a `MAT_` prefix. Use moderate original tiling textures and/or trim sheets; geometry/UV design must support them. Different material families need consistent apparent texel density. Start around 128 pixels/metre for ordinary walls/ground and assess at the actual gameplay camera; this is a proposed starting value, not a measured property of the references.

No ambient directional shadows, sunset glow or lamp reflections should be baked into the base-color textures. Separate albedo, material data and emission. Base colors remain muted gray/beige/russet, with autumn foliage as a controlled accent. Use rough asphalt/concrete, quiet dark glass and sparse metal highlights. Treat wear as broad variations, not a layer of scratches over every face.

Tree crowns should be irregular clumps/cards with visible branch gaps. Keep alpha edges clean; simplify farther LODs without changing canopy density drastically. Nature sheets may suggest loose leaves and grass; these are optional surface/card decoration, not a requirement to create thousands of tiny objects.

Optional day/night lighting variants should reuse the same assets and colors. A Carbon-inspired night setup can use cool ambient fill and localized restrained warm lamps/windows; avoid making every surface wet or adding neon across the whole kit.

## Starting detail budgets

These are broad authoring estimates to guide a first review, not hard scene-performance limits:

| Asset type | Starting LOD0 triangle range |
| --- | --- |
| Straight road plus simple shoulders | 100-600 |
| Curved road or junction core | 400-2,000 |
| Facade bay | 150-800 |
| Assembled warehouse/workshop | 2,000-8,000 |
| Ordinary small prop | 80-1,000 |
| Large shelter/container | 500-2,500 |
| Tree with simplified foliage | 600-2,500 |

Judge silhouette and actual rendering cost rather than forcing every asset to one polygon ratio. Provide simpler LODs for buildings/trees and use repeatable modules instead of duplicating unique meshes. Lowering triangle counts alone does not establish the game's frame-rate target.

## Deliverables and modeling order

Deliver editable `.blend` sources, export meshes, new textures, material recipes, collision proxies, connector metadata and a catalog of actual renders. Suggested project location for authored source is `source-art/environment/original-map-kit/`; exports follow the project's existing separate runtime-art workflow.

1. Build RD_STRAIGHT_16, one broad bend, a curb/sidewalk pair, one facade bay, one barrier and one tree.
2. Assemble a 100-200 m test street from those pieces. Check scale using a 1 m calibration object and the project's vehicle.
3. Capture front/side/back orthographic views of modeled assets, then a normal gameplay-distance render of the assembled street. Compare with the three style anchors.
4. Finish the remaining road hubs and structure kit; check connector continuity, wheel support and passage clearance.
5. Expand the building/prop families with the same material/UV conventions. Validate rear faces, modular joins, openings and collider placement.
6. Package the accepted kit with a mesh/texture manifest and original-source attribution record. Asset-generation code and generated artwork should follow the repository's current distribution rules.

Do not import or modify the existing AC map as part of this job. Keep any current map scenes available as separate gameplay work; the new kit should be independently usable in an empty scene.

## Copyable prompt for another agent

> Build an original modular racing-map kit in Blender from this reference library. Read BLENDER-HANDOFF.md and asset-manifest.json, inspect the 12 asset sheets and the three style anchors, and use the manifest dimensions/road connectors as authority when generated views disagree. Author new meshes, UVs and textures without using the Assetto Corsa/NFS World map files. Deliver repeatable road segments/junctions, building bays/assemblies, props and natural assets with editable sources, material families, LODs, collision proxies and connector metadata. Start with the six-piece test street listed in the guide, compare actual Blender/in-engine renders with the anchors, then expand the accepted treatment across the 57 definitions. Preserve realistic scale and the grounded medium-detail style. Report images as references, not as evidence of finished geometry or performance.

## Generation record

The sheets were created with built-in image_gen using only the balanced safehouse/lighthouse images as style references. All exact prompts and output paths are in `prompts.json`. This package contains references and construction specifications, not completed 3D models or usable texture maps.

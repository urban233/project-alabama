# Environment art direction for coding agents

Build medium-detail low-poly 3D environments with realistic scale, worn industrial/suburban materials, golden autumn foliage, warm afternoon sunlight and cool atmospheric shadows. The latest requested direction sits between the polished studies and the coarse comparison images: substantial simple architecture, lightly textured surfaces, natural foliage silhouettes and restrained reflections. Keep a serious, grounded atmosphere and realistic proportions.

## Reference priority and scope

1. `balanced/camden-lighthouse.png`, `balanced/rosewood-safehouse.png` and `balanced/hickley-field.png` are the current candidate references for the user's requested middle level of detail. The user has requested this direction; final image approval is not implied.
2. `style-reference.png` supplies the original atmosphere, realistic scale, chase-view readability and palette. The new direction intentionally simplifies and varies it.
3. The root-level `*-art-study.png` images represent the rejected, overly polished detail extreme. `coarse-comparison/` represents the rejected, overly simplified extreme. They are comparison bounds, not implementation targets.
4. `first-pass/` preserves the earlier HUD/chase-view variants. Do not use their repeated camera layout as the template for every location. All generated scenes are concepts, not Unity captures, measured map layouts or exact recreations of the 2005 locations.

Use the balanced images for shape, composition and material relationships. Preserve moderate seams, brick patterns, railing detail and broad wear. Keep detailed cracks, individual grass blades, bolts and numerous tiny props out of the modeling target. Equally, avoid featureless walls, crystalline foliage, huge polygon color patches across the pavement and flat geometric cloud patches. Generated fine details are not requirements to model them individually.

## The middle-level detail rule

| Element | Current target | Too detailed | Too coarse |
| --- | --- | --- | --- |
| Buildings | Simple solid masses with moderate openings, seams and texture-only masonry | Individually modeled bricks, bolts, elaborate trim | Featureless boxes with only flat color bands |
| Roads | Matte rough surface, a few broad repairs and legible markings | Dense tiny cracks, pebbles and highly photographic noise | Large contrasting polygon patches or angular road bends |
| Trees | Irregular crowns of smaller angular clumps, branch gaps and natural silhouettes | Detailed individual modeled leaves everywhere | A few giant crystalline crown blocks |
| Vehicle | Recognizable body proportions with broad faceted chassis panels, simpler supporting detail and modest reflections | Smooth dense chassis, promotional gloss, intricate rims and mechanical microdetail | Box-shaped silhouette and plain disc wheels |
| Lighting | Natural cloudy sky, warm diffuse key, cool shadows and modest haze | Cinematic glow, heavy grading and dazzling reflections | Flat polygon clouds and uniformly flat unlit surfaces |

Allocate geometry to silhouette, structural openings and route readability. Allocate modest texture detail to material recognition. Following the 4 October chassis review, the car must show deliberate low-poly planes rather than retaining a dense body behind smooth normals. Preserve the BMW M3 GTR E46 roofline, kidney grille, wide arches, hood vents and rear wing while simplifying its surfaces.

## Additional references for the imported NFS World map

The [agent adaptation guide](agent-reference-pack/ADAPTATION-GUIDE.md) connects six supplementary road, facade, foliage, underpass, downtown and waterfront studies to the existing Downtown Rockport Blender/Unity pipeline. They explain how to adapt already imported meshes and textures while preserving layout, UV regions, alpha and driving collision. The three balanced location images remain the primary fidelity targets; these additional images supply category-specific context. They are generated concepts, not captures of the imported district or texture atlases. Exact additional prompts are in [the pack record](agent-reference-pack/prompts.json).

## Three distinct location identities

| Study | Visual identity | Reusable kit | What agents should reproduce |
| --- | --- | --- | --- |
| `balanced/camden-lighthouse.png` | Open coastal space, a tall landmark, winding route and distant harbor | Tapered tower, passage plinth, seawall, road curves, rails, lamps, faceted rocks | A clear tower silhouette against sky; an S-curve leading through a drivable passage; water separating near and distant geometry |
| `balanced/rosewood-safehouse.png` | Enclosed neighborhood garage, asymmetric roofline and quiet forecourt | Brick piers, metal wall/roof panels, roller doors, open bay, gate, poles, curbs, tires | A garage that reads from street level; repeated bays broken by one open bay; fencing and trees framing the approach without hiding it |
| `balanced/hickley-field.png` | Large repeated grandstand structure, open field and a bright framed exit | Seating steps, column bays, beams, fence sections, floodlight towers, passage | A sweeping rhythm of columns and seating; contrasting grass/dirt/concrete; a drivable exit clearly readable from inside |

## Geometry and surface rules

- Use box-based building masses and simple roof planes. Model a feature when it changes silhouette, casts an important shadow, frames the route or supports collision. Put shallow brick joints, stains and minor surface wear into textures.
- Keep man-made planar faces clean. Use hard edges at structural corners and broad authored facets on the vehicle chassis; reserve controlled smoothing for tires and appropriate rounded supporting objects. Avoid random triangulation on every flat wall.
- Use visibly faceted rock meshes with broad planes. Begin tower cylinders at roughly 12-16 sides and judge the silhouette at gameplay distance; this is a proposed modeling starting point, not a measured property of the source.
- Trees need irregular clustered crowns with gaps, ochre/russet color groups and readable trunks. Avoid perfect spheres, uniform lollipop trees and detailed individual leaf geometry everywhere.
- Reuse modules but vary assembly, roof height, opening pattern and material tone. Break repetition at landmarks rather than adding random clutter to every bay.
- Maintain plausible human/vehicle dimensions. As starting blockout values, try road lanes near 3.5 m and ordinary curbs near 0.15 m. Validate drivable passages against the project's actual vehicle bounds and turning path; the generated images are not dimensional evidence.
- Road markings follow the road surface. Rails, fences and support posts need consistent spacing and clean junctions. Reject disconnected rails, floating props and impossible overhead clearances even if a reference image contains them.

## Materials and authored starting palette

These colors are suggested starting swatches, not pixel samples or a mandatory LUT. Judge them under the scene lighting.

| Role | Suggested base colors | Treatment |
| --- | --- | --- |
| Asphalt | `#383B3A`, `#4C4A43` | Matte, broad patches and restrained cracks; no wet mirror finish |
| Concrete/stone | `#827D6C`, `#A19982` | Large warm-gray masses, sparse stains and edge wear |
| Brick/rust | `#6B4936`, `#82563C` | Muted rust/brown accents; low-contrast masonry detail |
| Painted steel | `#343B3B`, `#4B5550` | Dark structural silhouettes and limited worn paint |
| Autumn foliage | `#A26924`, `#BD8536`, `#70461E` | Separate crown color groups; moderate saturation |
| Grass/dirt | `#686A43`, `#826246` | Broad olive and earth surfaces, sparse ground detail |
| Water/distant air | `#506877`, `#89949A` | Cool blue-gray contrast, softened distant forms |

Keep most architecture rough and nonmetallic. Use restrained highlights on car paint, glass, steel and water. Use a small shared material family with controlled variants. Do not bake directional lighting or the screenshot's orange grading into all albedo textures.

## Lighting and presentation

- Use a warm afternoon key light with a low-to-moderate sun angle and cool gray-blue ambient fill. Keep detail visible in shadowed garages and passages while retaining depth.
- Make nearby geometry dark and distinct; progressively reduce contrast and saturation toward the skyline with atmospheric haze.
- Preserve a neutral-to-muted environment so gold foliage, road markings and vehicle lights remain readable. Avoid heavy global sepia, crushed black shadows and large bloom halos.
- Render style review images without HUD, depth of field or motion blur. Use a wide environmental view and a close asset view; then check the same scene from the actual gameplay camera.
- Each location should communicate its own shape language: vertical/coastal lighthouse, low enclosed safehouse, repeated sweeping stadium. Keep materials and illumination coherent across them.

## Suggested implementation order and visual acceptance

1. Block out route, landmark silhouette and camera view with simple geometry. Check actual vehicle passage and route readability.
2. Add the reusable architecture kit, curbs, barriers and major foliage groups. Evaluate an untextured render before adding detail.
3. Apply shared materials, broad wear and sparse props. Establish lighting and atmospheric depth using the approved source.
4. Compare actual scene captures with the reference at matching display size. A scene should remain recognizable as lighthouse, safehouse or stadium even as a small thumbnail; surface noise must not dominate architecture.
5. Check source-style fidelity: realistic proportions, angular environmental geometry, muted gray/brown masses, golden foliage, warm light/cool shadow, readable dark passages and separated near/middle/far depth. Check module connections, road continuity, collisions and clearances separately.

This package supplies visual targets and a handoff brief only. No scene, meshes, materials or gameplay code were changed, and no runtime performance or exact map reconstruction is established by these images. At the user's request, the reference images, guides and prompt records are versioned under `docs/art-direction/`. Runtime map assets, source archives and private asset packs follow the repository's separate asset workflow.

## Ready-to-use coding-agent prompt

> Read this ART-STYLE-GUIDE.md and inspect the three images under balanced/ plus style-reference.png. Target the balanced medium-detail low-poly direction: believable scale, simple substantial architecture, modest rough surface textures, irregular angular foliage with natural silhouettes, warm diffuse light, cool shadows and restrained vehicle reflections. Root-level *-art-study.png and coarse-comparison/ show the two rejected extremes; do not target either extreme. For the assigned location, first establish its distinctive silhouette, plausible route and reusable architecture modules. Use the repository's existing asset workflow and preserve existing GUIDs and baseline scenes. Review actual scene captures at gameplay camera distance before claiming a visual match. Do not infer exact original-game map geometry from these concepts.

## Generation record

Generated with the built-in image_gen tool. `prompts.json` stores the exact first-pass and polished-study prompts. `balanced/prompts.json` stores the exact balanced and coarse-comparison prompts and image paths. Original location descriptions were cross-checked against the [2005 game walkthrough](https://gamefaqs.gamespot.com/pc/927142-need-for-speed-most-wanted-2005/faqs/40050); it describes the lighthouse passage and Hickley Field drive-through route. Architecture in this package remains an interpretation; location screenshots would be needed for a closer layout match.

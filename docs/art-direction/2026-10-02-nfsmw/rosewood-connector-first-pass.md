# Rosewood connector: first art pass

This draft applies the reference pack to one short section of Rosewood's connector tunnel, around the source lane point at Unity world position `(503, 22, -1084)`. The starting view is the existing private `downtown-to-rosewood-25.png` route capture. The closest style study is [underpass structure, props and shadow](agent-reference-pack/04-underpass-props-shadow.png); [road, curb and barrier](agent-reference-pack/01-road-curb-barrier.png) supplies adjacent material context. These concept images are appearance targets, not replacement geometry or an exact tunnel layout.

## Authored scope

- Blender reduces connected pieces of the opaque tunnel fitting `Buildings_00213` independently at 15 degrees. Each piece must preserve its boundary curves and bounds within 2 mm before joining the result; Unity repeats its 2 cm FBX boundary check. UV/material seams remain protected. The shell and pipe meshes retain their existing recipes after their trial reductions failed this gate. Physical road and collision meshes are untouched.
- Three opaque connector concrete atlases receive a small warm lift in neutral, dark regions after the established edge-preserving bake. Those regions retain 25% of the source detail so the wall has subdued wear at driving distance. Bright lamps, strongly coloured paint, image dimensions and opaque material behavior remain protected. The existing opaque bake writes full alpha; the wall source's varying alpha is not a cutout mask in this material. The other texture families retain the existing recipe.
- Three unshadowed cool fill lights cover the section from roughly x=485 to x=535. Intensity 39.6 was selected from actual Unity captures at 0, 13.2, 39.6 and 79.2; the highest value produced larger bright pools. The persistent scene retains its sun, ambient sky, fog, exposure and shadow ownership.
- A fixed two-view Unity capture (`route.png`, `wall.png`) uses the same camera positions before and after regeneration. Captures and regenerated private assets remain under ignored local paths.

## Visual review and acceptance

1. Capture the current combined scene with `./tools/rosewood.ps1 -Action ArtStudyCapture`. The two 1920×1080 PNGs are saved to `artifacts/NfsWorld/Captures/RosewoodConnector/`. Preserve them before changing a recipe. `ArtStudyLightingCapture` separately renders controlled fill-light variants with fixed materials and camera positions.
2. Run the Rosewood Blender art mesh and texture bake with `docs/districts/rosewood.json`, run `ArtStudyVerify` to check the reduced fitting after FBX import, then regenerate Rosewood content and recapture from the same two positions. `-AllowEditorUpgrade` permits the locally installed Unity 6000.6.4f1 for this study; the project remains pinned to 6000.3.25f1 for accepted builds.
3. Compare the two actual rendered images at full size and thumbnail size against the underpass study. The wall, road edge and structural supports should remain readable in shade, with muted concrete and limited highlights. Reject bright halos, flat glowing walls, lost lamps or markings, and new openings or disconnected surfaces.
4. Record accepted triangle counts from `connector-geometry.json`, `art-meshes.json` and `batching.json`, plus any Unity candidate rejections. Recheck the drivable route, collision and performance before promoting this preview into the next private release.

The captured result is the fidelity gate for this draft. Recipe values are starting points until the before/after render and geometry reports are inspected.

Blender 4.4.3 produced 256 triangles from the fitting's 280 source triangles across six accepted pieces. `ArtStudyVerify` in Unity 6000.6.4f1 confirmed that reduction and preserved boundary curves after FBX import. The three baked atlases retain 512×256, 256×256 and 256×256 dimensions, with opaque output alpha and mean RGB shifts of approximately +0.041, +0.055 and +0.046.

The historical chase-camera view is the initial baseline. The first newly captured candidate and the controlled lighting sweep use the fixed study cameras; no fixed-camera capture of the untouched initial scene was obtained before regeneration. Keep that distinction when reviewing before/after images.

# Rosewood connector: first art pass

This draft applies the reference pack to one short section of Rosewood's connector tunnel, around the source lane point at Unity world position `(503, 22, -1084)`. The starting view is the existing private `downtown-to-rosewood-25.png` route capture. The closest style study is [underpass structure, props and shadow](agent-reference-pack/04-underpass-props-shadow.png); [road, curb and barrier](agent-reference-pack/01-road-curb-barrier.png) supplies adjacent material context. These concept images are appearance targets, not replacement geometry or an exact tunnel layout.

## Authored scope

- Blender applies guarded planar reduction to eight connector shell and fitting meshes: `Buildings_00041`, `00177`, `00207`–`00211` and `00213`. Angles range from 10 degrees for walls/roof to 35 degrees for repeated pipes. Other Rosewood meshes retain their existing recipe. UV/material seams and open boundaries remain protected; Unity's 2 cm boundary check can still reject a candidate. Physical road and collision meshes are untouched.
- Three opaque connector concrete atlases receive a small warm lift in neutral, dark regions after the established edge-preserving bake. Bright lamps, strongly coloured paint, image dimensions and alpha remain unchanged. The other texture families retain the existing recipe.
- Three unshadowed cool fill lights cover only the short section from roughly x=485 to x=535. The persistent scene retains its sun, ambient sky, fog, exposure and shadow ownership.
- A fixed two-view Unity capture (`route.png`, `wall.png`) uses the same camera positions before and after regeneration. Captures and regenerated private assets remain under ignored local paths.

## Visual review and acceptance

1. Capture the current combined scene with `./tools/rosewood.ps1 -Action ArtStudyCapture`. Preserve the two output PNGs from `artifacts/NfsWorld/Captures/RosewoodConnector/` as the local baseline.
2. Run the Rosewood Blender art mesh and texture bake with `docs/districts/rosewood.json`, then regenerate Rosewood content and recapture from the same two positions. `-AllowEditorUpgrade` permits the locally installed Unity 6000.6.4f1 for this study; the project remains pinned to 6000.3.25f1 for accepted builds.
3. Compare the two actual rendered images at full size and thumbnail size against the underpass study. The wall, road edge and structural supports should remain readable in shade, with muted concrete and limited highlights. Reject bright halos, flat glowing walls, lost lamps or markings, and new openings or disconnected surfaces.
4. Record accepted triangle counts from `art-meshes.json` and `batching.json`, plus any Unity candidate rejections. Recheck the drivable route, collision and performance before promoting this preview into the next private release.

The captured result is the fidelity gate for this draft. Recipe values are starting points until the before/after render and geometry reports are inspected.

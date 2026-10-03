# First car replacement and visual fidelity

This work is stacked on PR #5 (`codex/rosewood-underpass-art-study`), starting
at `5687abf9b050da866dd89e37eeb7caa1b3d6fc49`. The supplied car will replace the
current BMW E46 as the player vehicle.

## Status and source handoff

Planning only: `bmw_m3_need_for_speed_most_wanted.glb` and
`bmw-m3-need-for-speed-most-wanted.zip` are present locally, but have not been
inspected or imported. Keep both out of public Git history.
The user confirmed that "pixel perfect" means adapting this supplied 3D model
to the exact art direction defined by the [art style guide](art-direction/2026-10-02-nfsmw/ART-STYLE-GUIDE.md)
and its three primary `balanced/` references.

Target a grounded, medium-detail car with realistic proportions, a smooth main
body, simplified supporting detail, restrained texture noise and modest
reflections. Preserve recognizable wheels and silhouette. Avoid promotional
gloss, mechanical microdetail, boxy body shapes and plain disc wheels. The car
may be somewhat smoother and more detailed than its environment. Judge it in
the existing warm autumn light and cool shadows, preserving reviewed scene
lighting during material comparisons.

Prefer FBX with all texture maps. An optional GLB copy and reference renders
help compare the intended materials and appearance. Include the source/license
record, real dimensions if known, and front, side, rear and three-quarter views.
Keep original files under ignored `source-art/`; runtime artwork continues to
use private asset packs, never public Git or PR attachments.

## Implementation scope

- Inspect the supplied geometry, hierarchy, UVs, normals and materials before
  choosing any reductions. Preserve the car's silhouette and recognizable detail.
- Prepare metre scale, documented axes, and independent wheel meshes/pivots;
  adapt the existing four-wheel driving setup to the new dimensions.
- Assemble and review URP materials for paint, glass, trim, lights and wheels.
- Replace the player BMW in the active runtime and relevant review/setup recipes;
  inspect existing E46-specific assumptions and tests before updating them.
- Preserve steering, wheel rotation, suspension, collision and chase-camera
  behavior. Keep original source assets available throughout the replacement.

## Acceptance checklist

- [ ] Supplied model and source/license record inspected.
- [ ] Scale, wheelbase, wheel pivots, normals and material assignments verified.
- [ ] Front, side, rear, three-quarter and chase views compared with the primary
      balanced art-direction references and source-car silhouette references
      using actual Unity renders in the existing scene lighting.
- [ ] No visible clipping, floating wheels, broken glass, missing textures or
      obvious shading seams at intended gameplay distances.
- [ ] Driving, camera framing and collision checked with the replacement car.
- [ ] Relevant import and driving checks pass; gameplay performance measured
      against the existing baseline before acceptance.
- [ ] Updated private asset handoff and matching public recipes/locks prepared
      under the existing asset-versioning procedure.

The initial draft PR records this scope only. It does not claim a completed
replacement, accepted visuals, or runtime validation.

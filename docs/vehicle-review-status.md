# Vehicle review — 1 October 2026

Historical record of the earlier BlenderCentral study. The current player car
is the supplied memoov replacement; see [current adaptation and validation](first-car-replacement.md).

## Delivered scope

BlenderCentral's BMW M3 E46 has been adapted into a grounded racing-car study. The original archive is preserved locally. The cleaned editable Blender source retains separate panels; the Unity FBX merges them into one body and four wheel meshes. Generated race details include wider arches, a splitter, wing, diffuser, and exhaust tips. Original packed reference images/logo textures are removed.

- 60,311 triangles versus 816,368 in the inspected source viewport evaluation: 92.6% reduction.
- Unity bounds: approximately 4.65 m long, 2.03 m wide, 1.42 m high.
- Four independent wheel pivots, verified front/back, left/right, and axle height in Unity.
- Fourteen defined material families, with two generated 512 × 512 maps.
- CC-BY-SA 3.0 derivative asset, attributed to BlenderCentral. Code remains MIT; Poly Haven sky/asphalt are CC0.
- Static `VehicleReview.unity` scene with a textured road, sunset sky, warm directional light, and industrial building blockout.

This is a visual asset milestone. The city is not finished, and driving, traffic, police, UI, distant LODs, exact GTR bodywork/livery, and performance qualification remain pending. The screenshot's atmosphere is a target, not an achieved match. Source wheelbase and racing additions are visual estimates, not certified BMW dimensions.

## Reproducible workflow

1. `tools/blender/prepare_e46_source.py` validates/extracts the pinned archive.
2. `tools/blender/build_e46.py` rebuilds the editable source, material contract, FBX, maps, and geometry report; `--render` also saves Blender studio images.
3. `tools/unity.ps1 Setup` applies the project configuration.
4. `tools/unity.ps1 VehicleSetup` creates/remaps URP materials, builds the prefab, validates the export, and regenerates the review scene.
5. `tools/unity.ps1 VehicleCapture` saves actual 1920 × 1080 URP views.
6. `tools/unity.ps1 VehicleBuild` builds the static Windows review under `builds/vehicle-review/`.
7. `tools/record_vehicle_assets.py` updates hashes after final asset changes.

`VehicleSetup` owns its generated prefab/materials/review scene. Custom scene edits should be made in a separate scene. The standalone review uses the existing bootstrap settings for resolution and frame-rate target; the car is stationary.

## Rendering compatibility decision

On this machine (RTX 4060, driver 32.0.16.1074), the pinned Unity 6000.3.25f1/URP 17.3.0 render requests produced incorrect material colours/highlights with SRP batching enabled. Mesh submesh counts and assigned material assets were correct. Repeated captures reproduced the defect on both Direct3D 12 and Direct3D 11. Disabling SRP batching restored blue paint and red tail lamps; re-enabling it reproduced the defect. This is an observed project/environment issue, not a confirmed Unity bug diagnosis.

`ProjectFoundation.ConfigureProject` therefore explicitly disables SRP batching in the PC pipeline and enables 4× MSAA. No driver change or engine upgrade was made. This compatibility setting also applies to the standalone build; its CPU performance cost has not been qualified. Revisit it with a minimal reproduction and profiler measurements before scaling up the city. Diagnostic images are retained locally under `artifacts/VehicleCapture/` (`rear-no-batching.png`, `rear-second-batched.png`, `rear-batching-restored.png`). The final `rear.png`/`front.png` use the saved project configuration.

## Validation evidence

Import validation checks dimensions, axis orientation, wheel pivots, mesh count, triangle budget, material/submesh count, external URP material bindings, and required paint/lamp textures. Blender source and material previews were inspected through Computer Use. The standalone window's Computer Use access request timed out; automated URP captures provide the visual evidence instead.

Latest logs and results are under `artifacts/VehicleSetup/`, `VehicleCapture/`, `VehicleBuild/`, `EditTests/`, and `PlayTests/`. A successful screenshot/build is not a measured 1080p/60 FPS result.

| Check | Result |
| --- | --- |
| Final import and vehicle contract | Passed: 60,311 triangles, 5 meshes, all pivots/materials validated |
| Actual URP front/rear captures | Reviewed at 1920 × 1080 with the saved compatibility configuration |
| Edit Mode regression suite | 7 passed, 0 failed |
| Play Mode foundation integration | 1 passed, 0 failed |
| Final Windows development build, strict mode | Passed, 0 build errors |
| Final player startup | Ran for 12 seconds without script errors in headless mode; stopped by harness |
| Asset hashes and Unity metadata | No mismatches or missing `.meta` files |

The reduced mesh still has visible panel shading irregularities under strong reflections. Refining those surfaces, the GTR wheel/body details, and the reference livery belongs in the next art pass. The current result is ready for review rather than final visual acceptance.

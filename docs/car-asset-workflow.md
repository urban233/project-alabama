# E46 game asset workflow

The user selected the free [BMW M3 E46 by BlenderCentral](https://blendswap.com/blend/10588) on 1 October 2026. The desired result is a lower polygon, grounded racing car: retain the recognizable E46 proportions and a serious silver/blue GTR direction. It must not become a cute or exaggerated low-poly car.

## Source and license

Download through BlendSwap's normal authenticated download flow. Preserve the original archive unchanged under `source-art/third-party/blendswap/e46/original/`, alongside its SHA-256 and the supplied license/attribution. Do not execute scripts embedded in the Blender file; use Blender's `--disable-autoexec` option when inspecting it. Keep the editable adaptation outside Unity's Assets folder, with an explicit FBX export inside the project.

The downloaded archive confirms **CC-BY-SA 3.0**, with attribution to **BlenderCentral**. The archive and extracted source were hash-checked and preserved locally. The adapted car retains that license; code remains MIT. Original packed reference images and logos are removed from the exported asset. See `source-art/vehicles/e46/LICENSE.md` and `docs/assets.json`.

The current close model has **60,311 triangles** compared with **816,368** in the inspected source viewport evaluation (92.6% reduction). Unity imports five meshes: body plus four independent wheels. Measured Unity bounds are approximately 4.65 m long, 2.03 m wide, and 1.42 m high. Fourteen material families and two generated 512-pixel maps are retained for this review. Distant LODs remain pending.

## Inspect before reducing

Run `tools/blender/inspect_vehicle.py` against the untouched source. It reports mesh counts before and after modifiers, object transforms and bounds, material usage, images, collections, and embedded text/script flags. Identify the body, glass, trim, lights, wheels, and studio-only objects from that report and a visual inspection. Explicitly map the chosen car objects; never infer that every mesh in a studio scene belongs in the game.

Use the source control cage where possible. Retain subdivision only where it materially improves the silhouette. Reduce underside and hidden interior geometry first. Preserve wheel arches, roof/window profile, headlamp and tail-light outlines, hood/trunk edges, mirrors, and wing silhouette. Decimation is a controlled per-part tool, with boundaries and material seams protected where possible; a single aggressive decimation ratio for the whole vehicle is not the workflow.

## Starting budgets

These are tuning budgets, not measurements or acceptance results:

| Item | Initial budget |
| --- | --- |
| Close chase-camera car | 30,000–60,000 triangles, adjustable after inspecting the source |
| Distant LOD | 10,000–20,000 triangles once the close model is accepted |
| Material slots | Aim for 8–12 shared material families |
| Texture size | At most 2048 pixels for hero paint/detail maps initially |

Keep smooth normals on curved body panels and intentional hard edges on seams, trim, and mechanical parts. Low polygon count does not require flat shading. Retain natural car dimensions; do not enlarge lights, wheels, or cabin for a stylized toy appearance.

## Game-ready handoff

Use metre scale and a ground-level root. Export a stable hierarchy with independent `Wheel_FL`, `Wheel_FR`, `Wheel_RL`, and `Wheel_RR` axle pivots and a documented forward axis. Use dedicated simplified collision proxies. Assemble URP materials explicitly; the old Blender Cycles node graph is not a Unity material contract.

Save three-quarter front, side, and rear views before and after reduction using comparable cameras. Review silhouette, curvature, normals, panel boundaries, wheel placement, and lights at the intended chase-camera distance. Import into Unity, verify scale and pivots there, and review the actual URP render before expanding the city or calling the car accepted. Keep the original and adapted triangle counts in the validation record.

The source model is the starting point; GTR wide-body and wing changes follow inspection. Code quality, license provenance, and visual fidelity are separate checks, and all remain necessary.

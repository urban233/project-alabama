# Supplied BMW replacement

PR #6 is stacked on PR #5 (`codex/rosewood-underpass-art-study`), starting at
`5687abf9b050da866dd89e37eeb7caa1b3d6fc49`. The supplied memoov E46 replaces
the earlier BlenderCentral player BMW through the existing FBX, visual-prefab
and driving-prefab identities. Authored map scenes and lighting are preserved.

## Art adaptation

The [art style guide](art-direction/2026-10-02-nfsmw/ART-STYLE-GUIDE.md) and
three primary `balanced/` references define grounded, medium-detail low-poly
art: realistic proportions, broad faceted car surfaces, simplified supporting
detail, restrained texture noise and modest reflections in warm autumn light
with cool shadows.

The first 40,404-triangle adaptation was rejected as too smooth. The revised
chassis uses much stronger decimation, restrained planar merging and explicit
flat normals after final triangulation. It preserves the BMW M3 GTR E46 roofline,
kidney grille, hood vents, wide arches, sill profile and rear wing while making
the body visibly angular. This review correction is also recorded in the art guide.

The revised export has **14,166 triangles**, including **9,396 in the complete
body mesh and 2,837 in the painted chassis**. That is 81.5% fewer painted-body
triangles than the first adaptation's 15,325, and 65% fewer triangles overall
than its 40,404 Unity mesh. Rims, tyres, grilles, lamps, trim, interior and brakes
receive separate reductions. Near-coplanar painted faces are merged at one degree;
UV/material boundaries are retained, and submillimetre degenerate faces removed.
All 2,837 painted triangles round-trip through FBX with matching flat face normals.

Thirteen material families replace the supplied material bindings. Paint and
alloy use restrained metallic values and 0.28 smoothness; rubber and trim are
rough, and glass uses a stronger tint to reduce interior clutter. The paint atlas was cleaned
with the imagegen skill/tool, retaining the blue-and-silver layout while reducing
scratch noise. The reviewed atlas and exact prompt are private source assets at
`source-art/vehicles/supplied-e46/style/E46_Paint.png` and `prompt.json`.

Unity bounds are approximately **4.65 × 1.90 × 1.33 metres** (length, width,
height). One body mesh and four wheel meshes preserve independent steering,
rotation and suspension. The measured tyre radius is **0.327364 m** and the
axle separation is approximately **2.784 m**. WheelCollider positions, controller
visual references and `E46Tuning` agree with the replacement. Existing chassis
collision and handling settings are retained apart from measured tyre radius.

## Source and reproduction

The root ZIP provides the textured FBX; the accompanying GLB supplies a source
and license declaration. The GLB names **memoov**, the
[source model](https://sketchfab.com/3d-models/bmw-m3-need-for-speed-most-wanted-b6a04764e3ea42cc9021327fdccd45ab)
and CC-BY-4.0. This is the supplied declaration, not an independent rights audit.
Attribution and modifications are recorded in the private `LICENSE.md` and
public `docs/assets.json`. Earlier BlenderCentral source and attribution remain
preserved separately.

Original files are unchanged under ignored `source-art/vehicles/supplied-e46/original/`;
the root ZIP SHA-256 is
`cab9e032dbb6dde49752287facccb164d4d637bdc60f485f828f1f344ce2dc74`.
The source FBX SHA-256 is
`bbead0a8c581696324a8969bd0cb692ede51f483aaa043503152b12ecbfaf372`.
The recipe can rebuild from the extracted input supplied by the private pack
without requiring either original root upload.

With Unity closed, run Blender 4.4:

```powershell
blender --background --factory-startup --disable-autoexec --python tools/blender/build_supplied_car.py --
# Review artifacts/CarReplacement/export before installation.
blender --background --factory-startup --disable-autoexec --python tools/blender/build_supplied_car.py -- --install
./tools/unity.ps1 -Action SuppliedCarSetup
./tools/unity.ps1 -Action SuppliedCarVerify
./tools/unity.ps1 -Action SuppliedCarCapture
```

Installation preserves existing `.meta` files and backs up the previous runtime
directory under `artifacts/CarReplacement/baseline/runtime/`. Unity setup updates
materials and prefabs without regenerating custom scenes. The older `build_e46.py`
recipe recreates the superseded car.

## Validation

- Main-project import verification: five meshes, dimensions, axes, external URP
  materials, paint/lamp maps, wheel pivots and measured physics radius pass.
  The current contract caps the complete car at 16,000 triangles, complete body
  at 10,000 and painted chassis at 3,500; it requires flat normals on at least
  98% of painted faces. Unity reports 2,802/2,837 passing the panel-normal check.
- Main-project Edit Mode suite: **17 passed, 0 failed**.
- Main-project handling Play Mode tests: **3 passed, 0 failed**, including live
  wheel orientation/axle rotation, acceleration, braking, reverse, steering,
  reset, high-speed contact, curb and barrier behavior.
- Asset-tool Python suite: **48 passed** using Blender's bundled Python/numpy.
- Blender front, rear and side renders reviewed after the stronger mesh reduction.
- Nine actual 1920 × 1080 Unity views reviewed in the combined district: front,
  side, rear, chase and environment at the connector, plus four Downtown views.
  The runtime contains exactly one player car. Review transforms are unsaved;
  source scenes and their authored lighting remain unchanged. Accepted images
  accompany the private source under `source-art/vehicles/supplied-e46/review/`.
  Two additional Unity studio views and three Blender views show the chassis
  facets more clearly under direct light and accompany the same private review.

Local test evidence is in `artifacts/EditTests/results.xml` and
`artifacts/CarReplacement/faceted-handling-results.xml`. Geometry reduction is
measured against the earlier car; a new rendered-player FPS qualification has
not been performed by this art change.

## Private handoff

The base profile extends the original curated manifest with the replacement's
editable Blender source, extracted FBX/maps, reviewed atlas/prompt, attribution
and runtime assets. It excludes unchanged original upload archives and Blender
backups. Runtime artwork remains ignored; Git carries recipes, provenance,
tests and release locks only. The user authorized this checkout to publish base.

Published **base 1.0.2** contains 250 files (108,731,506 uncompressed bytes).
Verification reports zero missing or modified files and preserved GUIDs.
Privately send `project-alabama-base-1.0.2.zip` (95,786,658 bytes) and
`project-alabama-base-1.0.2.zip.sha256` with this PR's code revision.
ZIP SHA-256:
`b7c5b1eaa27bb362b72204603e4183427500e727667b85bf129ec1c4d0a2e193`.
The receiver also retains `project-alabama-nfs-world-0.2.0.zip` and runs
`python tools/asset_handoff.py receive` from the project root with editors closed.
Local publication does not transfer files to the other developer.

The compatible map release continues to use its existing ZIP. Its dependency
pin advances with the base lock; map payload identity and map version do not
change. A pre-existing local edit to the Rosewood `Buildings.blend` is preserved
and belongs to PR #5's map work; this car change does not publish or overwrite it.

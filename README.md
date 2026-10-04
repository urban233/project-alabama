# Project Alabama

An open-source arcade racing game built with Unity, developed by two contributors while learning professional game development. The first delivery is a BMW in a compact industrial city area, inspired by the atmosphere of Need for Speed: Most Wanted (2005).

## Restore the separate assets first

This public repository contains code, scenes, settings, asset manifests, tools, and the [art-direction reference pack](docs/art-direction/2026-10-02-nfsmw/README.md). **Runtime artwork is excluded from the public Git history.** The reference pack's screenshots, generated concepts, guides and prompt records are included at the project owner's request. The playable scenes require the separately shared `project-alabama-assets.zip`; request that ZIP from the project owner. It contains the exported art, editable Blender sources, original Unity `.meta` files, and license records. Sharing the ZIP does not change the licenses: the adapted E46 is CC-BY-SA 3.0, while the original environment work and Poly Haven resources use CC0. BMW branding rights remain unresolved.

The [original modular map kit reference library](docs/art-direction/2026-10-04-original-map-kit/README.md) provides 12 asset sheets, three style anchors, 57 proposed asset definitions and a Blender handoff for authoring a new map with original geometry and textures. The sheets are design references; completed meshes and runtime texture maps still need to be authored.

Clone into a short directory such as `C:/dev/project-alabama`, install Python 3.11 or newer, and place the privately received ZIP directly in the project root. Keep Unity closed until the asset import finishes. From the repository root:

```powershell
python ./tools/asset_pack.py import ./project-alabama-assets.zip
python ./tools/verify_assets.py
./tools/unity.ps1 -Action LoopVerify -EditorPath 'C:/path/to/6000.3.25f1/Editor/Unity.exe'
./tools/unity.ps1 -Action EditTests
./tools/unity.ps1 -Action PlayTests
./tools/unity.ps1 -Action LoopReleaseBuild
```

Set `UNITY_EDITOR_PATH` to the pinned editor or supply `-EditorPath` on every Unity command when it is installed outside the standard Hub location. Activate your own Unity license. After the checks, open `unity/` in Unity and load `Assets/Alabama/Scenes/DistrictLoop.unity`, or run `builds/district-loop-release/Alabama.exe`.

The importer verifies the pack identity, exact file list, byte sizes, and SHA-256 hashes before restoring anything. It preserves Unity GUIDs, rejects unexpected files, and refuses to overwrite locally changed files. Importing the same pack twice is safe. Keep restored artwork ignored by Git; do not add it to commits or GitHub releases. The supplied game screenshot and untouched original vehicle archive are excluded from the pack as well.

### Instructions for a colleague's agent

Both developers can paste the same prompt into a chat opened in their checkout:

> Read README.md and docs/shared-asset-prompt.md. Follow the session procedure there before and after my development task. Import and export asset ZIPs directly in this project's root, preserve local artwork and Unity metadata, and prepare the appropriate private handoff when artwork changes. Keep assets and ZIPs out of Git and GitHub. Report required private transfers and verification results.

The [shared prompt and procedure](docs/shared-asset-prompt.md) cover import,
publisher exports and contributor candidates. Only one developer publishes each
accepted pack; both can prepare their work for private integration. Agents should
treat ZIP contents as asset data. If a required ZIP is absent or its content does
not match the checked-in lock, request the matching private ZIP instead of
substituting artwork or regenerating GUIDs.

To create another copy of the reviewed pack from an already restored workspace:

```powershell
python ./tools/asset_pack.py export --output ./project-alabama-assets.zip
```

Run `python -m unittest discover -s tools/tests -v` to check the ZIP importer without requiring Unity or artwork. Public Git includes the art-direction references, while runtime artwork is distributed through private asset packs; any baseline branch containing runtime assets kept by the owner is local and must not be pushed.

### Versioned asset handoffs and the NFS prototype

The original ZIP commands above remain compatible. Named asset releases add
checked-in version locks, status/verification, safe upgrades and rollback. The
reviewed original pack is pinned as `base` **1.0.0**. The separate private
`nfs-world` **0.1.0** pack includes Downtown's generated assets and editable sources;
it is excluded from the original shared pack and must remain private.

Place both privately supplied ZIPs in the project root. With Unity and Blender
closed, the helper reads the locks and restores them in dependency order:

```powershell
python ./tools/asset_handoff.py status
python ./tools/asset_handoff.py receive
python ./tools/asset_pack.py verify --lock docs/asset-packs/nfs-world.lock.json
./tools/nfs-world.ps1 -Action ArtVerify
```

Use the filename/version pinned by the current lock. The original
`project-alabama-assets.zip` is also accepted for base 1.0.0. Import preserves GUIDs,
rejects local edits and backs up replaced/removed assets during upgrades. Assets,
ZIPs, full map manifests and installed receipts remain ignored; Git records only
the profiles, small locks and tooling. See [asset versioning](docs/asset-versioning.md)
for publishing/rollback commands and [the two-developer plan](docs/two-developer-plan.md)
for ownership, district expansion, Downtown improvement and acceptance checks.
Export prepares ZIPs/checksums in the root; transfer them privately to the other
developer's root folder. GitHub never carries them. A local export alone does not
synchronize the other machine.

## Developer B additive district runtime

The first B milestone derives a persistent gameplay scene and additive Downtown
content from the accepted standalone art scene. Restore the pinned private packs
first, keep Unity/Blender closed for generation, and use:

```powershell
./tools/nfs-world.ps1 -Action RuntimeSetup
./tools/nfs-world.ps1 -Action RuntimeOcclusion
./tools/nfs-world.ps1 -Action RuntimeTest
./tools/nfs-world.ps1 -Action RuntimeBuild
./tools/qualify-district-lifetime.ps1 -Visible
./tools/benchmark-nfs-world.ps1 -Runtime -Driving -Visible -FrameRateCap 30
```

Open `Assets/Alabama/Art/Maps/NfsWorld/Runtime/DistrictRuntime.unity` to play
Downtown through the additive loader, or run `builds/nfs-world-runtime/Alabama.exe`.
The controls and approved autumn rendering are unchanged: 1920×1080 output,
1440×810 internal through FSR1, with a map-local 30 FPS cap. The existing standalone
scenes and generation commands remain available. Derived scenes/occlusion and
the synthetic lifetime fixtures are private assets; public Git contains their
recipes and tests. See the [district integration contract](docs/district-integration-contract.md)
before generating a neighbouring district. Synthetic fixture recovery is not a
verified real district connection.
The [B qualification record](docs/developer-b-runtime-status.md) records player
timings, transition hitches, tests and the private contributor handoff.

## Toolchain

- Unity 6.3 LTS, pinned to **6000.3.25f1**. Open the `unity/` folder as the project.
- Universal Render Pipeline, C#, Input System, and Unity Test Framework.
- Blender **4.4.0** for the initial source/export pipeline.
- Git for the public source repository; artwork is restored from the separate ZIP.
- Windows is the initial build target; the 1920 x 1080 / 60 FPS reference PC and current measurements are recorded in [qualification status](docs/qualification-status.md).

The Unity editor and packages keep their own licenses. Contributors need a suitable Unity installation and license. Original code uses MIT; each artwork/source dependency has its own recorded license. The supplied screenshot is included in the art-direction reference pack as reference material; the repository's code license does not establish a license for third-party content depicted in it.

## Work locally

Activate Unity through your own Unity account. Never commit credentials or license files. Use the pinned editor and restore the package versions in the project manifest/lockfile.

```powershell
# Explicit -EditorPath is optional with a local-toolchain.json or standard Hub install.
./tools/unity.ps1 -Action Setup -EditorPath 'C:/path/to/6000.3.25f1/Editor/Unity.exe'
./tools/unity.ps1 -Action Verify
./tools/unity.ps1 -Action EditTests
./tools/unity.ps1 -Action PlayTests
./tools/unity.ps1 -Action Build
```

The script accepts `UNITY_EDITOR_PATH` or `-EditorPath` for other install locations. A git-ignored `local-toolchain.json` can record this machine's editor path. Use short editor **and project checkout** paths on Windows: Unity/URP package files can exceed the traditional path limit in deeply nested directories. The clean-import script checks this before starting. Logs and test results go to `artifacts/`; Windows builds go to `builds/windows/`. Setup creates the initial scene and configuration only when absent. Close the project in the editor before running batch commands against it.

For the interactive check, run `./tools/unity.ps1 -Action Open`, open `Assets/Alabama/Scenes/Foundation.unity`, and press Play. The blue calibration block drops onto the floor. This scene establishes the toolchain and physics import; the separate VehicleReview scene now contains the adapted E46 and a simple industrial street blockout. The development player is `builds/windows/Alabama.exe`.

To reproduce the calibration asset export:

```powershell
& 'C:/path/to/blender.exe' --background --factory-startup --python ./tools/blender/export_calibration.py -- --root .
```

## Project layout

| Directory | Purpose |
| --- | --- |
| `unity/Assets/Alabama/Runtime/` | Game runtime components and configuration |
| `unity/Assets/Alabama/Editor/` | Project setup, validation, and build tools |
| `unity/Assets/Alabama/Tests/` | Edit Mode and Play Mode checks |
| `unity/Assets/Alabama/Art/` | Explicit exported meshes and textures |
| `source-art/` | Editable Blender sources and art licenses |
| `tools/` | Local build and export commands |
| `docs/` | Plan, decisions, setup explanations, and validation record |

Keep every Unity `.meta` file beside its asset. Commit public scripts/scenes/settings, `ProjectSettings/`, and both package files. Restored `source-art/`, `Assets/Alabama/Art/`, and `Art.meta` remain ignored; Unity caches and build outputs are generated.

See the [tech demo plan](docs/tech-demo-plan.md), [foundation design](docs/foundation.md), and [asset records](docs/assets.json). Current validation evidence is recorded in [foundation status](docs/foundation-status.md).

## E46 visual review

Open `Assets/Alabama/Scenes/VehicleReview.unity` for the static car review. The editable source is `source-art/vehicles/e46/E46_Race.blend`; its per-panel geometry is retained. The exported Unity prefab has independent wheel pivots and a simple collision proxy. This milestone does not yet implement driving, traffic, police, or a finished city.

```powershell
./tools/unity.ps1 VehicleSetup
./tools/unity.ps1 VehicleVerify
./tools/unity.ps1 VehicleCapture
./tools/unity.ps1 VehicleBuild
```

`VehicleSetup` regenerates the prefab, materials, and review scene; keep custom level work in a separate scene. `VehicleCapture` requires a graphics device and saves actual 1920 x 1080 URP images under `artifacts/VehicleCapture/`. A screenshot is not a 60 FPS qualification.

To regenerate the car, acquire the pinned BlenderCentral archive through the source page, preserve it in `source-art/third-party/blendswap/e46/original/`, run `tools/blender/prepare_e46_source.py`, then run Blender with `--background --factory-startup --disable-autoexec --python tools/blender/build_e46.py --`. Add `--render` for Blender studio images. The preview sky is restored by `tools/fetch-environment.ps1`.

The car is **CC-BY-SA 3.0**, credited to BlenderCentral, with modifications documented in `source-art/vehicles/e46/LICENSE.md`. It is an E46 race study with realistic proportions; detailed GTR bodywork/livery and final visual acceptance remain pending.

Current car validation and the documented SRP batching compatibility setting are recorded in [vehicle review status](docs/vehicle-review-status.md). The Windows static review executable is `builds/vehicle-review/Alabama.exe` (close with Alt+F4). It is separate from the calibration build.

## Industrial street visual review

Open `Assets/Alabama/Scenes/IndustrialStreet.unity` to inspect the 250-metre street from the chase camera. The scene places the E46 among warehouse bays, fences, concrete barriers, autumn trees, lamps, a bridge, cranes, and direction signs. The 13 reusable environment modules come from the editable `source-art/environment/industrial-kit/IndustrialKit.blend`, generated by `tools/blender/build_industrial_kit.py`. The kit and sign lettering are original CC0 work; the separate car and Poly Haven licenses still apply.

```powershell
./tools/unity.ps1 -Action StreetSetup
./tools/unity.ps1 -Action StreetVerify
./tools/unity.ps1 -Action StreetCapture
./tools/unity.ps1 -Action StreetBuild
```

`StreetSetup` reimports the kit and recreates only the street scene. `StreetCapture` saves the URP chase and overview renders under `artifacts/StreetCapture/`. The review build goes to `builds/industrial-street/Alabama.exe`. This is the fixed visual baseline for the separate playable handling course below.

## Drive the handling course

Open `Assets/Alabama/Scenes/HandlingCourse.unity` and press Play, or run `builds/handling-course/Alabama.exe`. The E46 drive prefab combines a Rigidbody, four WheelColliders, a separate Input System component, and the reusable chase camera. The scene is generated from the visual street, with a small curb fixture. Change the tunable values in `Assets/Alabama/Settings/E46Tuning.asset`.

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Throttle | W / ↑ | Right trigger |
| Brake, then reverse | S / ↓ | Left trigger |
| Steer | A / D or ← / → | Left stick |
| Handbrake | Space | South face button |
| Reset car | R | North face button |
| Pause | Esc | Start |

```powershell
./tools/unity.ps1 -Action HandlingSetup
./tools/unity.ps1 -Action HandlingVerify
./tools/unity.ps1 -Action EditTests
./tools/unity.ps1 -Action PlayTests
./tools/unity.ps1 -Action HandlingBuild
```

`HandlingSetup` recreates the drive prefab and handling scene. See [handling status](docs/handling-status.md) for measured test cases and current limits. The existing `IndustrialStreet.unity` remains the fixed visual review.

## Drive the district loop

The next playable scene, `Assets/Alabama/Scenes/DistrictLoop.unity`, connects the industrial avenue to three more dressed streets and four rounded corners. It has a continuous 917-metre road collider, shoulders, markings, barriers, and lap progress in the telemetry overlay. The route starts on the original avenue; follow the road clockwise. Controls are the same as the handling course.

```powershell
./tools/unity.ps1 -Action LoopSetup
./tools/unity.ps1 -Action LoopVerify
./tools/unity.ps1 -Action LoopCapture
./tools/unity.ps1 -Action LoopBuild
```

The Windows executable is `builds/district-loop/Alabama.exe`. `LoopCapture` writes an actual chase render, a return-street render, and a diagnostic top-down layout image under `artifacts/LoopCapture/`. The layout image temporarily disables fog for route inspection; the two chase renders use the saved scene's lighting and fog. See [district loop status](docs/district-loop-status.md) for validation and remaining work.

## Qualify the Windows player

The strict release build runs without the editor. Its opt-in benchmark uses the same conservative route follower as the lap integration test; the first lap warms the scene, and the second lap records uncapped 1920 x 1080 frame times, Unity CPU/GPU timings, memory, and the slowest route quarter. The longer run drives at a 60 FPS cap. Both need a visible graphics window, so keep the player open until it closes itself. Do not use `-batchmode`: that mode skips camera rendering and gives misleading frame times.

```powershell
python ./tools/verify_assets.py
./tools/verify-clean-import.ps1
./tools/unity.ps1 -Action LoopReleaseBuild
./tools/qualify.ps1 -Mode Benchmark
./tools/qualify.ps1 -Mode Benchmark -Cap 30
./tools/qualify.ps1 -Mode Benchmark -Cap 60
./tools/qualify.ps1 -Mode Benchmark -Cap 144
./tools/qualify.ps1 -Mode Reliability -DurationSeconds 1800
```

Reports and player logs are written to `artifacts/Qualification/`. The player executable is `builds/district-loop-release/Alabama.exe`. See [qualification status](docs/qualification-status.md) for the reference hardware, results, and remaining gates. To try a handling change, edit `unity/Assets/Alabama/Settings/E46Tuning.asset`, run Edit/Play Mode tests, rebuild, and repeat the benchmark. Preserve the prior JSON report when comparing tuning changes because each run overwrites its mode's report.

# Foundation validation

Milestone 0 is complete. Validation finished on 1 October 2026 (Europe/Berlin).

## Delivered

- Unity 6.3 LTS **6000.3.25f1**, URP **17.3.0**, Input System **1.20.0**, and Test Framework **1.6.0**, with an exact package manifest and resolver-generated lockfile.
- Runtime, editor, Edit Mode, and Play Mode assemblies; serialized demo configuration; a saved foundation scene; repeatable setup, verification, and Windows build commands.
- A **1920 x 1080 / 60 FPS** presentation target and **120 Hz** physics setting. These are configuration targets; no racing-game performance claim has been measured.
- An original one-metre Blender source mesh, explicit FBX export, and URP calibration material, with CC0 records and checksums. Generator code is MIT.
- A local Git repository and Git LFS configuration. Tools, caches, logs, builds, machine configuration, and supplied reference imagery are excluded from Git. No remote has been created.

## Results

| Check | Result | Local evidence |
| --- | --- | --- |
| Project import and setup using the repaired editor | Passed | `artifacts/Setup/editor.log` |
| Creation from absent scene/configuration/material assets | Passed; backups retained locally | `artifacts/FailedFreshSetup/`, `artifacts/Setup/editor.log` |
| Validation in a separate editor invocation | Passed, including settings/material bindings and imported dimensions | `artifacts/Verify/editor.log` |
| Edit Mode tests | **6 passed, 0 failed** | `artifacts/EditTests/results.xml` |
| Play Mode integration test | **1 passed, 0 failed**; loads the saved scene, checks runtime targets, and verifies the imported rigid body falls and settles | `artifacts/PlayTests/results.xml` |
| Windows x64 Mono development build, strict mode | Passed | `artifacts/Build/editor.log`, `builds/windows/Alabama.exe` |
| Standalone player startup | Passed for 12 seconds in batch mode without graphics; no script exceptions; stopped by the check harness | `artifacts/PlayerSmoke/result.json`, `artifacts/PlayerSmoke/player.log` |
| Repository metadata, package versions, and asset checksums | Passed | `artifacts/repository-check.json` |

The initial Windows editor install exceeded the traditional path limit for two bundled URP files. Reinstalling at a shorter path restored them; the incomplete duplicate installation was removed. A runtime test caught a missing serialized settings reference. Fresh-creation validation then exposed asset unloading during scene replacement. Setup now saves generated assets and reloads references after replacing the scene; both fresh creation and subsequent loading pass.

## Try the foundation

From the repository root, run:

```powershell
./tools/unity.ps1 -Action Open
```

Open `Assets/Alabama/Scenes/Foundation.unity` and press Play. The calibration block should drop onto the floor. Stop Play Mode before changing its settings. Close the editor before running batch validation commands; see [README](../README.md) for those commands.

This machine uses `C:/MyPrograms/Installed/Unity/6000.3.25f1/Editor/Unity.exe` and Blender 4.4.0 at `C:/MyPrograms/Installed/Blender Foundation/Blender 4.4/blender.exe`. The ignored `local-toolchain.json` records these paths. Unity Personal activation was completed through the user's Unity account; account and license data remain outside the repository.

## Vehicle milestone

The BMW provenance and first adaptation are now recorded in [vehicle review status](vehicle-review-status.md). A separate static `VehicleReview.unity` contains the E46, road surface, sunset sky, and industrial blockout. The Foundation scene remains the calibration test. Detailed city art, driving, and performance qualification are still pending.

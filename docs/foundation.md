# Foundation design

The first milestone establishes a repeatable Unity workflow. The foundation scene is an import, lighting, camera, and physics fixture. The BMW and city art are the next milestone.

## Decisions

Unity 6.3 LTS is the fixed engine; 6000.3.25f1 is the pinned patch selected from Unity's release catalog. URP is the render pipeline. Packages use exact versions, with the resolver-generated lockfile committed after import.

Use a conventional Unity project in `unity/`, original art sources in `source-art/`, and tools outside the runtime asset directory. A small runtime assembly owns game components; an editor assembly owns setup/validation/build code. Separate test assemblies keep test dependencies out of player builds. Further architectural layers should solve a demonstrated need.

`DemoSettings` is immutable configuration during a session. `DemoBootstrap` applies the 120 Hz simulation rate and 60 FPS presentation cap. A serialized asset makes those targets visible and reviewable; mutable game state will live in runtime instances.

The foundation setup command creates assets and the scene only when they do not already exist. It saves configuration before a scene serializes its reference and reloads assets after replacing the scene, since scene replacement can unload temporary asset instances. It uses Unity APIs to preserve GUIDs on subsequent runs. Verification checks configuration, URP assignment, build-scene registration, metadata settings, the scene's configuration and material bindings, and the calibration import. Interactive setup offers to save modified scenes before switching scenes.

The original Blender calibration block measures one meter on each axis. Its exported FBX and source stay separate. An import check verifies those actual dimensions and a Play Mode test exercises rigid-body contact with the foundation floor.

Edit Mode tests exercise invalid configuration and import integrity. Play Mode tests load the saved scene and validate its runtime configuration and physical contact. A Windows development build proves the project produces a runnable player; visual quality and performance need later in-engine measurement.

## Source control and licensing

Git tracks Unity assets with `.meta` files, ProjectSettings, and package manifest/lockfile. It ignores tool installations, Library/Temp, logs, test results, builds, and the supplied screenshot. Git LFS tracks art binaries. The project's original code is MIT; the original calibration artwork is CC0. Unity/template/package files retain their own licenses, recorded in the asset/dependency notes.

## Local commands

Run Setup after a clean import, followed by Verify, EditTests, PlayTests, and Build through `tools/unity.ps1`. Logs and NUnit XML are local artifacts. Setup must finish before scene-based tests are run. Batch commands require the project's pinned editor and an activated Unity license, and cannot share the same project with a running editor instance. The command waits for the editor process itself, allowing Unity's background services to outlive that process.

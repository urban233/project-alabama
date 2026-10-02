# Developer B district runtime milestone

Accepted baseline: `origin/main` at `0194d80` (merged PR #1), 2 October 2026.
The checkout was clean and is available exclusively for B. Work branch:
`codex/developer-b-district-runtime`. Base 1.0.0 and NFS World 0.1.0 remain pinned.

## Inspection and implementation plan

`NfsWorldSetup` generates gameplay and map roots together. The visual/art recipes
retain the car, bootstrap, camera, sunlight and volume and add a scene-local URP
override. `MapRecovery` resets to the controller's Awake pose. Distance culling
already retains colliders and keeps its targets on a scene-local component.

1. Preserve all existing generation commands and standalone scenes. Derive new
   ignored runtime/content scenes from the accepted art scene through Editor APIs.
   Move gameplay and shared render owners into the runtime scene; keep that scene
   active throughout additive operations so its fog/ambient settings remain authoritative.
2. Define district metadata in code: stable identity, common-frame bounds,
   supported recovery poses, connection ownership and scene-local culling. Validate
   scene structure and register/unregister metadata with content lifetime.
3. Serialize explicit additive load/unload operations. Prevent duplicate loads and
   reject unsafe unloads; optionally recover onto verified retained collision before
   unloading supporting content. Keep input and physics owners alive and preserve pause.
4. Test repeated fixture cycles, rejection paths, persistent object/input/render
   identity, recovery and culling cleanup. Test real Downtown driving and reloads.
5. Rebuild the runtime configuration's occlusion, build a strict Windows player,
   compare visible capped/uncapped stationary and moving measurements with standalone
   under identical settings, and report load/unload hitches separately.
6. Verify the original asset pack after work. Generate only additive derived assets;
   prepare a private NFS contributor candidate if needed without advancing its lock.
   Commit/push coherent public source checkpoints and open the main-targeted B PR
   only after review readiness, requesting urban233.

No automatic streaming, neighbour/seam claim, source conversion, model/texture
improvement or accepted asset-release publication belongs to this milestone.

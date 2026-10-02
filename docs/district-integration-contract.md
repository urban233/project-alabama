# District integration contract v1

Developer B owns this runtime contract. Developer A should review it before
generating dependent neighbouring content. It implements the first B milestone
in [the two-developer plan](two-developer-plan.md). No real neighbour or seam is
qualified by the synthetic fixtures.

## Scene structure and coordinate frame

`DistrictRuntime.unity` is the persistent gameplay scene for a driving session.
It remains loaded and active. It owns exactly one E46 controller/input, chase
camera, telemetry, DemoBootstrap, sunlight, global atmosphere volume and
NfsWorldRenderSettings (approved URP copy and 30 FPS cap). Loading districts uses
`LoadSceneMode.Additive`; changing the active scene or loading another gameplay
scene during a session is outside the contract. Ending the session unloads its
host and restores its rendering override. There is no distance-based streaming.
The initial scene list can load multiple accepted districts together.

A content scene contains exactly one enabled `DistrictContent` with a stable
unique district ID, world-space bounds, fall-recovery height, recovery poses and
connection records. Its metadata transform is identity. Geometry can keep its
existing category roots (`RoadsPhysical`, `Roads`, etc.) to preserve source audits.
Visible meshes, static collision, exit closures, occlusion areas/data and each
`NfsWorldDistanceCulling` component/target array belong to this scene. A content
scene cannot contain a player, input, camera, bootstrap, runtime, pipeline override,
directional light or global post-processing volume. Its serialized fog/sky values
are not applied because the host remains active. Do not move content into the host
or retain it with `DontDestroyOnLoad`.

All districts use the established source origin `AC_PIT_0`:
`(1978.0977783, 99.6069107, 1165.7668457)` metres. Subtract that origin in source
space; source XYZ becomes Blender X/-Z/Y, then Unity -X/Y/Z. Preserve the FBX X
reflection and 10-metre axis-marker checks. No per-district recentering, rotation,
scale adjustment or floating-origin change is introduced. Bounds, spawns, road
collision, closures and connection boxes all use this same Unity world frame.

## Recovery and connections

`DistrictRecoveryPose` contains a unique local pose ID, world-space chassis
position and Euler heading. At the current E46 setup, the position is .24 metres
above the measured road. Supply clear poses on retained physical roads. Registration
requires at least one pose with four tyre-footprint raycasts onto that scene's
enabled collision and a clear chassis volume. Upper/lower bridge levels and another
scene's road cannot validate missing support. Recheck support at unload/recovery
time; disabling or deleting supporting collision invalidates the destination.
The original standalone Downtown fall threshold remains -80 metres.

Each `DistrictConnection` records its stable ID, neighbouring district ID,
reciprocal connection ID, world-space seam bounds, outward direction, explicit
collision owner, closure object and `seamVerified`. A supplies the paired records
and the road ownership/overlap decision; neither developer should generate duplicate
driving surfaces over a seam. Include road height, lane-edge alignment, ramps/bridge
level and ownership of the exit barrier in the review. Use a single collision owner
for an overlap, or complementary non-overlapping collider extents.

All six accepted Downtown barriers remain active. Their generated records use
`downtown-exit-1` through `downtown-exit-6`, with unresolved neighbour/reciprocal
data and `seamVerified=false`. They identify closures, not accepted crossings.
Only replace those placeholders and remove the relevant closure after both
districts load and the seam is driven in both directions with source-road contacts,
height/lane/scale checks and no duplicate contact. Retain all other closures.
`seamVerified` is reviewed metadata; the loader does not automatically open exits.

## Lifetime API and hooks

Use `yield return runtime.LoadDistrict(scenePath)` and inspect `LastFailure`.
Paths must be enabled in the player build. Loading an already registered path is
idempotent. Duplicate district IDs, missing/invalid descriptors, unsupported
recovery and cross-scene culling targets fail registration; a rejected loaded scene
is unloaded. Operations are serialized; callers must await completion before the
next request. Keep content scripts passive during scene activation: contract
validation occurs after Unity activates the scene, so it is not an isolation boundary
for arbitrary scripts in an unreviewed scene.

`yield return runtime.UnloadDistrict(id)` rejects the last district and content
whose bounds contain the car or whose collider supports a wheel. To deliberately
remove supporting content, specify `UnloadDistrict(id, retainedDistrictId)`.
The loader verifies the retained pose, holds physics, moves the existing car and
snaps the existing camera before removing source support. It preserves input,
pause and rendering. This is an explicit recovery transition, not a seamless seam.
Unloading distant content updates a reset destination if it belonged to that scene.
Normal wheel contact updates reset ownership when driving into a loaded district.

`DistrictRegistered` and `DistrictUnregistered` are runtime hooks for future
systems. Register stores only the district descriptor; culling remains local.
Descriptor disable/unload removes registration and restores hidden renderers.
If all content disappears through an external editor/session operation, the car's
physics is held rather than continuing to fall. Runtime content removal must go
through the loader; directly unloading supporting scenes bypasses its safety check.
Subscribers must detach their hooks and references when their own lifetime ends.

## Generation, assets and acceptance

Restore the pinned ZIPs with the editors closed, then:

```powershell
./tools/nfs-world.ps1 -Action RuntimeSetup
./tools/nfs-world.ps1 -Action RuntimeOcclusion
./tools/nfs-world.ps1 -Action RuntimeVerify
./tools/nfs-world.ps1 -Action RuntimeTest
./tools/unity.ps1 -Action EditTests
./tools/unity.ps1 -Action PlayTests
./tools/nfs-world.ps1 -Action RuntimeBuild
```

RuntimeSetup reads the accepted standalone art scene and saves derived scenes
under ignored `Assets/Alabama/Art/Maps/NfsWorld/Runtime/`. It does not save the
source scene or change models, textures, materials, colliders or their metadata.
New assets retain their own generated metadata on subsequent saves. Existing
standalone setup/build/driving commands remain available. A should generate its
own content scene/asset root using this structure, rather than regenerate B's host.
B adds the accepted neighbouring path to the host's initial list and build scenes.

RuntimeOcclusion bakes the host-plus-Downtown configuration. It does not qualify a
future host-plus-two-district configuration; rebake/review that combination. Preserve
the approved sunlight/shadows, .75 URP render scale, FSR1 (1440×810 internal at
1920×1080 output), map-local 30 FPS cap and source collision. Measure uncapped
headroom, capped pacing, load/unload wall time and maximum frame spikes on the
Ryzen 7 5700U integrated GPU on mains before considering streaming.

The fixtures are synthetic support platforms far outside Downtown. They exercise
lifetime/rejection/recovery and must never be described as the neighbouring district.
Real Downtown checks must cover tyre support, driving, recovery, reload, uniqueness,
unchanged shared rendering and stale registration cleanup. Two-way seam validation
and combined performance remain dependent on A's accepted content.

Derived scenes/occlusion are private assets. B provides a contributor candidate
and a path/change inventory for A to integrate; B does not advance the NFS lock.
Preparing that ZIP does not transfer it. Code, recipes, tests, this contract and
script metadata are public; scenes/artwork/full inventories/builds remain ignored.

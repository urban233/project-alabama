# Startup, streaming and road-constrained reset

This implementation uses Unity 6000.3.25f1 and the existing district host.
Roads, terrain support, collision, exit closures and panorama remain resident. Only
bounded scenery visuals stream; this avoids removing the road beneath a car.
Vehicle torque, top speed and the 120 Hz physics step are unchanged.

## Phase 1 — Establish measurements

Record process-to-ready time separately from engine-to-ready time, core preload,
district loading and scenery readiness. Record scene/tile wall times, p95 frame
time, maximum frame time and failures. Unity Profiler markers identify core
preload, streaming demand, visibility, LOD and road reset queries. Unity's own
scene-loader markers show deserialization and main-thread integration.

Compare repeated runs on the same machine, power plan, resolution and cache
state. The baseline fixed-route process timing includes 6.5 seconds of driving
and captures: it is a comparable launch-plus-route measurement, not a precise
cold-start measurement. Use an external stopwatch for cold process launch;
BeforeSplashScreen instrumentation begins after native engine initialization.

Targets: remove unnecessary splash delay; move collision cooking into the build;
keep steady driving within 33.3 ms at the 30 FPS target, with 16.7 ms preferred
when hardware permits. Loading-screen frames have a separate budget. Diagnose
CPU integration, GPU upload and storage latency independently before raising
quality or shrinking safety margins.

## Phase 2 — Shorten startup and amortize asset upload

A minimal bootstrap starts Resources.LoadAsync for the small core preload
catalog and loads the gameplay host asynchronously. The district loader already
uses additive asynchronous scene loading. The empty bootstrap scene remains
loaded; the gameplay host owns the active scene, car, camera and render settings.
Increase background-loading priority
and upload budget only during initial loading, then restore gameplay settings.
Disable the optional splash and enable build-time collision baking in the editor
build configuration. Preload only the car and gameplay pipeline, not the map.
Do not call Shader.WarmupAllShaders or synchronous Resources.Load in gameplay.

## Phase 3 — Stream scenery before it becomes visible

Derive private runtime scenes from the accepted artwork, grouping bounded
scenery renderers into 256 metre cells. Retain original transforms, materials,
LOD assets and independent collision ownership. Reject scenery cells containing
colliders or persistent gameplay owners. Roads, long-distance visuals and
panorama stay in the core content scene; streaming never affects map support.

Load cells asynchronously, nearest first, one operation at a time. Demand uses
the current and predicted camera/car position, each cell's reviewed visibility
limit, a minimum lead distance and observed load latency. Unload only outside
a larger hysteresis radius and after a residence interval. Cache failed cells
instead of retrying each frame. Preserve the original culling target set so
disabled support geometry does not enter the recurring visibility loop.
Prepare the visible spawn neighbourhood before
Ready is published. Record load times, resident counts and missing visible cells.

The existing LOD/visibility hysteresis remains; extend motion lookahead to
cover fast travel. Low-end graphics adaptation keeps its private URP pipeline.
There is no unconditional latency guarantee for arbitrary storage hardware:
qualify rapid driving, first visits, revisits and reset teleports. Resident road
and background geometry provide continuity while higher-detail scenery loads.

## Phase 4 — Reset only to designated drivable roads

Bake a separate road NavMesh from source VISROAD road surfaces, with a vehicle
clearance radius and no automatic links. Exclude non-drivable geometry and
obstacles. Retain scene-owned NavMesh data and road-direction segments derived
from each source road region's principal axis. Junctions may choose the closest
compatible branch; the direction is flipped to retain the nearest heading.

For a reset, query the nearest loaded road NavMesh with increasing search radii,
including vertical distance
so bridges do not silently select a road underneath. Validate the complete
chassis footprint against loaded physical and visible ground and require an
unoccupied chassis volume. Align the heading to the road direction and the
up axis to the road normal; clear linear/angular velocity, wheel torque and
commands, wake the body and snap the chase camera. Never accept a roof, lawn
or sidewalk merely because a downward ray hits it. If no designated road is
valid, fail the reset safely rather than silently selecting arbitrary ground.
If the nearest NavMesh point is obstructed, rank validated alternatives sampled
at increasing radial distances; this finite search can fail when no candidate
has sufficient clearance. Road-region headings use source geometry rather than
an authored traffic spline; complex curved junctions need route qualification.
Legacy synthetic fixtures without road data retain their existing recovery.
The generated gameplay host explicitly requires road data, so missing or
disabled navigation cannot silently enable that legacy fallback. After a reset
to unloaded scenery, hold the car while the new neighbourhood is prepared;
automatic graphics sampling excludes this intentional wait.
Automatic unwedging is a separate operation: it retains validated local ground
history or finds nearby supported, clear ground within 24 metres. A car crashed
on a drivable plaza must not jump to a distant road merely to regain wheel
contact. Explicit R resets always follow the required-road policy.

## Delivery and qualification

`Alabama.Editor.NfsWorldArtDirection.BuildGame` derives the optimized scenes and
builds `builds/windows/Alabama.exe`. Generated artwork, NavMesh data and cell
scenes stay in the ignored private art tree; generator code and settings are
tracked. The bootstrap preload catalog contains private references and is also
generated under that tree. Run EditMode geometry/policy tests, affected PlayMode
lifetime/reset tests, native fast-motion and collision/reset reviews, then a
native startup/streaming report. `EngineSystemsProbe` runs only with
`-systems-review -systems-output <absolute-directory>` and writes
`systems-results.json`. It reports real frame times with frame pacing, not GPU
work time; it records first/repeat visits and confirms each final reset is on the
designated road. No screenshots run during its frame samples.
Preserve earlier failures and report measured
limits instead of claiming every device will meet the target.

For Editor play, use `Alabama/Play/Open Styled Game`; this now generates and opens
the same bootstrap and cell layout used by the player. The source art scenes
remain unchanged. Build with the pinned Editor using `-batchmode -quit
-projectPath <repo>/unity -executeMethod Alabama.Editor.NfsWorldArtDirection.BuildGame
-logFile <absolute-log-path>`. On this device the graphics-enabled batch build
completed after the headless build stalled during source-scene loading.

The initial 128-metre layout passed six native drives but its launch-plus-route
measurement increased from 22.10 to 26.04 seconds, including the added teleport
scenery preparation. Its engine-ready time was 12.76 seconds in the uncapped
systems review and 14.43 seconds in the fixed collision review. This motivated
256-metre cells and preserving the original visibility target set. Keep those
initial measurements in `artifacts/SystemsNativeFirst` and
`artifacts/SystemsLaunchComparison`; final qualification follows below.

## Final qualification on the local device

The strict graphics-enabled Windows build passed with 119 cells and 13,320
streamed scenery renderers. The generator retains 21,005 other renderers,
including disabled support geometry, but registers only the original visibility
targets for recurring culling. Road navigation contains 408 approved regions and
71,414 source road triangles. AI Navigation is locked to 2.0.15.

`artifacts/SystemsNativeFinal/systems-results.json` passed on the Ryzen 7 5700U /
AMD Radeon device at 1920x1080. Engine-to-ready took 7.426 seconds: core catalog
0.117 seconds, initial district loading 4.809 seconds, and initial scenery
preparation 2.222 seconds. Engine-to-ready excludes native process initialization.
The initial layout measured 12.759 seconds under the same systems-review method;
the final layout reduced that measurement by about 42%. These are warm-cache runs.

Six physical first/repeat drives covered approximately 100–105 metres each.
Their p95 frame times were 16.69–20.08 ms and maximum frame time was 26.45 ms,
including frame pacing. Required visible-cell misses, failed loads and physical
support failures were all zero. Every final reset was on the designated road;
torque, maximum speed and fixed timestep were unchanged. Maximum observed cell
load time was 0.149 seconds. Explicit teleport preparation took 0.087–1.769
seconds; this wait happens before driving resumes rather than showing unloaded
nearby scenery.

All 29 EditMode tests passed (`SystemsEditFinal`). Ten affected PlayMode tests
passed (`SystemsPlayFinal`), and the expanded road-policy test passed separately
(`SystemsRoadPolicyVerified`), including rejecting a reset when required road
navigation is disabled. The earlier compiler failure from an Editor scene-path
filter is preserved in `SystemsRoadPolicyFinal`; it was corrected before the
final build.

Implementation entry points:

| Subsystem | Code |
| --- | --- |
| Async initialization and metrics | `Runtime/Driving/GameStartup.cs` |
| Core preload references | `Runtime/Driving/StartupPreloadCatalog.cs` |
| Scene generation, road bake, splash/collision settings | `Editor/NfsWorldSystemsBuild.cs` |
| Prefetch, residency and cleanup | `Runtime/Districts/DistrictVisualStreamer.cs` |
| Scene-owned road navigation and headings | `Runtime/Districts/DrivableRoadMap.cs` |
| Validated road placement and cleared crash state | `Runtime/Districts/DistrictBoundaryGuard.cs` |
| Required-road policy and reset preparation | `Runtime/Districts/DistrictRuntime.cs` |
| Native profiling report | `Runtime/Driving/EngineSystemsProbe.cs` |

Paths in this table are relative to `unity/Assets/Alabama`.

The first full crash review (`SystemsCollisionFinal`) failed two cases because
automatic unwedging incorrectly used the explicit road-reset policy. This was
corrected to keep automatic recovery local. The prop case also exposed a probe
setup issue: the car could settle during asynchronous scenery preparation,
changing its intended impact trajectory. The native collision probe now holds
the body during preloading, then runs the original fixed settling/impact steps.
Both isolated cases passed in `SystemsProbeRecoveryVerified`; the expanded
fixture test confirms road-only R resets and local plaza unwedging together
(`SystemsRecoveryPolicyVerified`). These changes preserve engine tuning.

For runtime-only repairs, `NfsWorldArtDirection.RebuildPreparedGame` reuses the
validated generated-scene list. Regenerate with `BuildGame` whenever scene
geometry, generator policy or settings change. It still uses a strict player
build and refuses missing or out-of-scope scene paths.

The final strict rebuild passed (`SystemsBuildProbeFixed`). All 20 native crash
cases passed in `SystemsCollisionQualified`, with physical impact/edge checks,
escape movement, continued support and designated-road R resets. The maximum
automatic recovery hop was 3.067 metres. Explicit resets can be longer when
crashing far from roads: the furthest reviewed plaza-to-road reset was 164.08
metres, consistent with the nearest-designated-road requirement.

The final fixed launch-plus-route measurement was 18.679 seconds, compared with
the 22.103-second baseline, about 15.5% faster. The final measurement includes
teleport scenery preparation, the same 6.5-second driving workload and a capture
(`SystemsLaunchQualified/timing.json`). It is not a cold-start promise.
The opt-in collision probe logs harmless kinematic-velocity warnings when it
initializes its held test body; ordinary gameplay does not run that probe.

The Windows runtime assembly was rebuilt at 23:28:58 Berlin time on 5 October
2026. Launch `builds/windows/Alabama.exe` with its accompanying data folders.

Reference APIs: [asynchronous scene loading](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html),
[async upload budget](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/QualitySettings-asyncUploadTimeSlice.html),
[NavMesh sampling](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AI.NavMesh.SamplePosition.html).

# Visual stability follow-up

The approved whole-map style remains the visual baseline. The user reports a
flashlight-like bright flicker on the left when starting to move, occasional
flashes elsewhere, unstable building windows, and noisy white vehicle paint.

## Plan

1. Capture repeat frames and small camera movements at the starting street.
   Isolate ambient occlusion, visibility culling and depth priming. Inspect
   building surfaces, shadow ownership and the vehicle material before changing
   assets.
2. Correct the identified unstable rendering paths and any overlapping facade
   surfaces. Keep window coverage solid and preserve the approved material detail.
3. Make the car's light paint a clean, consistent off-white while retaining its
   blue livery, lighting and readable body shape.
4. Compare the same views over time, check driving/collision regressions and
   rebuild the default Windows game. Record remaining limits explicitly.

## Diagnosis and results

The source vehicle paint texture contains two flat colour regions;
it does not contain grain. The pre-fix renderer used randomly offset blue-noise
SSAO at half resolution without temporal accumulation. Editor isolation captures
show that removing SSAO removes blotches from the light body panels. The running
Windows player also visibly shows the reported grain.

The first geometry audit covered 639,169 triangles near the starting district:
1,434 coincident triangles and 60 zero/invalid normals. Most duplicates use the
same texture and are not sufficient evidence of a visible defect. Eight triangles
do overlap between the original window texture and the authored window atlas;
those competing surfaces require a targeted correction.

The visible native-player isolation recorded 200 frames: eight stationary frames
and 6.4 metres of matching motion in each of five rendering variants. On identical
stationary frames, the original paint had 2,399 pixels changing by more than eight
8-bit colour values; disabling SSAO reduced this to zero. The scene-local styled
renderer now disables SSAO. Solid light paint and blue livery keep their existing
colours, material response and body geometry.

The window correction removes only exactly coincident triangles competing with
the authored `windows-atlas-a.png` layer. It removes 40 active triangles in five
spatial batches; 56 more were corrected in retained older derived batches. All
vertices, UVs, normals and bounds remain exact. Separate panes, other duplicate
surfaces and all collision stay untouched. Regeneration applies the same rule
before building distance LODs. Two added tests cover stable surface selection,
idempotence, unchanged surface data and preservation of separate/non-window faces.
All 22 EditMode tests passed.

The subsequent native acceleration run with the normal chase camera reproduced
the flashlight-like flash. In `native-driving/current/044.png`, a bright bloom
spot appears on the left pavement at approximately 9 km/h. Across 120 captured
frames per variant, the current, no-shadow and no-occlusion variants contain five
large positive brightness spikes. The no-depth-priming variant contains none.
For the fixed left-pavement region (x=0..699, y=150..779), the largest mean RGB
increase is 34.92/255 in the current variant and 0.25/255 with priming disabled.
That initial result suggested priming, but a fresh 600-frame run disproved it:
`native-driving-fixed/current` still contains flashes at frames 4 and 36 with
priming and SSAO disabled, verified by per-frame telemetry. Its five variants
must not be treated as proof that disabling priming fixes the flash.

The map shader used unsafe normalization of imported zero-length normals.
The existing mesh export contains zero normals on thin faces around
x=48, z=14, in the left-side flash area; the local `zero-normal-audit.json`
records their source positions and areas. The whole-map export audit covers
2,307 meshes and 6,649,372 vertices: 257 zero/short normals, no stored non-finite
normals. The shader now uses `SafeNormalize`
for the surface normal, road mask and relief-adjusted normal. Valid normals,
surface data and geometry retain their authored values. The original forced
depth-priming policy is restored because disabling it was not a sufficient fix.

The driving probe's `current` and `current-repeat` variants now respect the built
renderer settings. Per-frame `poses.csv` records car/camera positions, speed,
priming mode and AO state. The rebuilt safe-normal player recorded 600 frames
in `native-driving-safe-normals`: two ordinary acceleration repeats plus
no-shadow, no-occlusion and no-priming isolation. None contains a large positive
brightness spike in the left-pavement region. Both ordinary repeats retain
forced priming with SSAO disabled, drive 14.19 metres and reach 26.8 km/h.
Their largest positive mean RGB changes are 0.31/255 and 0.30/255, respectively.
An 8x4 tile check across the scene, excluding the top/bottom HUD, also finds no
positive mean brightness jumps above 20/255 in any of the 600 frames. The largest
tile increases in the two ordinary repeats are 2.68/255 and 2.65/255; the earlier
flash frame measured 81.56/255. Metrics and telemetry are saved with the captures
in `temporal-analysis.json`. This verifies the reproduced starting-street case,
not every camera pose throughout the map.

The default Windows build with the shader correction succeeded. All 22 EditMode
tests passed after that correction. The latest 24 PlayMode tests passed before
this follow-up, including styled driving/recovery, high-speed wheel contact,
exit walls and district reloads; driving/collision implementation is unchanged.
All 45 Python tests passed using workspace-local temporary files. Exact test
times and build/source hashes are local in `Stability/verification.json`.

The separate rendered performance benchmark ran on AC power (queried before
and after the run), without screenshot capture or a fixed capture timestep.
On the local Ryzen 7 5700U / Radeon integrated GPU at 1920x1080, 0.75 render
scale and FSR1, the five stationary views averaged 61.0–95.2 FPS. Nine high-speed
traversals averaged 64.2–88.3 FPS, travelled 68.8–71.1 metres each and retained
wheel support on every measured frame. Their p95 frame times were 12.35–21.03 ms;
no measured frame exceeded 40 ms. The renderer retained 146 mesh LOD targets,
zero statically batched LOD targets and the active authored diffuse fill.
All fourteen p95 values are lower than the saved earlier benchmark, although
separate runs are not a controlled attribution of the improvement. Results and
power state are in `performance-safe-normals.json` and `benchmark-power.json`.
This samples five views and three corridors repeated three times, not every
street or machine.

The visual probe is not a mains-performance qualification. The first hidden
player run produced black frames and is excluded from evidence. The existing
before/after window captures retain solid panes and are pixel-identical at all
eight matched views. Their small camera movements do not reproduce the original
intermittent window report, so these captures establish coverage only.

## Reproduction

After restoring the existing private Developer B candidate, run
`tools/nfs-world.ps1 -Action ArtDirectionStability`, then rebuild with
`tools/unity.ps1 -Action Build`. This updates only derived assets; the earlier ZIP
is not silently treated as containing the new fixes.

The opt-in player arguments `-nfs-stability-probe -nfs-stability-output <absolute-directory>`
record rendering isolation. Add `-nfs-stability-driving` for acceleration and the
actual chase camera. Run the player visibly. Normal gameplay does not instantiate
the probe. Evidence is local under `artifacts/NfsWorld/Stability/`.

For the separate rendered driving benchmark, run the rebuilt player with
`-nfs-benchmark -nfs-art-direction-benchmark -nfs-driving-benchmark` without
the stability-probe flags. The benchmark writes
`artifacts/NfsWorld/player-art-direction-benchmark-driving.json`.

## Road signs, containment and remaining window overlaps (4 October follow-up)

The supplied screenshots show the source `SFX_HWYSIGNSPOTLIGHTS_01_D` cards
covering direction signs with opaque dark ovals. The reviewed suppression list
in `visual-stability-materials.json` removes that texture layer's triangles from
derived near, middle and far meshes. Sign lettering, arrows, UVs, lamp fixtures
and source textures are retained. The assembly and LOD import paths apply the
same rule, so regeneration cannot bring the cards back. The first application
removed 4,144 triangles across current and older derived asset files; verification
of the runtime's 3,204 active/LOD meshes finds zero remaining card triangles.
The native before/after review covers twelve sign locations, including the
Riverfront Stadium junction from the user's screenshot. All twelve sampled
signs now show unobscured destinations and arrows.

The six existing exit barriers remain. `DistrictBoundaryGuard`, installed by
the additive runtime, also checks the E46 chassis footprint (1.9 x 4.45 metres)
against static ground collision owned by loaded content. It probes the next
physics movement in half-metre intervals and holds the last supported pose
when a corner would leave supported ground. This covers sideways/reverse
escapes and routes around a barrier. Ground above the car is rejected, so a
bridge deck over empty space cannot validate an escape. Short crest jumps,
slopes, scene loading and recovery continue to work. Source collision meshes
and the retained exit geometry are unchanged.

The expanded geometry audit finds 10,692 window-triangle centres within 12 mm
of a nearly parallel competing surface; 8,538 are within 2 mm and 5,738 within
0.1 mm. Exact triangle deduplication alone cannot resolve faces with different
triangulation or slight offsets. Reviewed authored window atlases receive
20 mm of outward separation in the vertex shader; retained source window layers
receive 10 mm. Colour, depth and normal passes use the same displaced positions,
so forced depth priming remains consistent. Stored geometry, UVs and collision
are unchanged. Other texture layers receive no displacement. This gives the
authored panes priority over their underlying facades and source window layers.

The native review records twelve moving-camera frames at each of twelve
roadside facades, with no missing panes or obvious flashes in the inspected
captures. The short before sequences also do not reproduce the user's
intermittent flicker. Registered image residuals are similar before and after;
they establish visual coverage, not proof that every possible flicker was
reproduced and eliminated. The correction addresses the measured depth overlap.
Routes are retained in `visual-stability-review-views.json`.

Validation: 23 EditMode and 26 PlayMode tests passed. A further nine targeted
district tests passed after extending the guard to chassis overhangs and
rejecting overhead decks; those include five escape directions, foreign-scene
collision, short jumps and district lifetime/recovery. The Windows build uses
the same scene/collision contract validation as the earlier candidate.

Run `tools/nfs-world.ps1 -Action ArtDirectionReadabilityBuild` after restoring the
private candidate to apply the corrections, verify active/LOD assets and build
`builds/windows/Alabama.exe`. The executable's Unity launcher timestamp can stay
unchanged; the updated game code is in `Alabama_Data/Managed/Alabama.Runtime.dll`.
Keep the launcher, data directory and sibling runtime files together.

For native visual verification, pass `-nfs-map-review <absolute-path-to-visual-stability-review-views.json>`
and `-nfs-stability-output <absolute-output-directory>` to the visible player.
The review runs only with that explicit flag and freezes vehicle physics during
camera captures. Separate high-speed driving verification uses the regular
rendered benchmark, with no screenshot capture or fixed capture timestep.
Evidence remains under `artifacts/NfsWorld/Stability/`.

The final rebuild updated `Alabama.Runtime.dll` at 20:34 Berlin time. Its native
benchmark recorded 5,040 camera renders and 3,047 GPU timing samples. All nine
high-speed drives travelled 68.8–71.1 metres and retained wheel support on every
measured frame, confirming that the containment guard permits these corridors.
On battery with the Power saver scheme, driving averaged 71.9–95.0 FPS with
11.19–16.10 ms p95 frame times and no driving frame above 40 ms. Stationary views
averaged 69.0–106.3 FPS; one stationary frame exceeded 40 ms. These are local
battery results, not a controlled comparison with the earlier mains run.
The final report is `performance-readability-final.json`; the earlier diagnostic
run is preserved separately because geometry auditing was still active then.

## Scenery collision and visible map edges (4 October collision follow-up)

The next screenshot shows an exposed road with no surrounding terrain. The
source contract also explains the drive-through scenery: only three of 2,752
building meshes have collision, and the 96 prop and 92 tree mesh groups have
none. These counts describe source mesh groups, not individual objects.

`NfsWorldSceneryCollision` generates a separate persistent collision root from
full-resolution source geometry. It is independent of visual culling and LODs.
Buildings, roadside signs, poles, props and woody tree geometry receive static
mesh collision from both sides. Effect cards and transparent leaf rectangles
are excluded. Alpha masks are sampled to avoid transparent cutout regions. The original
driving collision and six existing exit barriers remain intact and are checked
against their previous signatures during runtime verification.

The supplement contains 807 obstacle chunks with 5,530,294 triangles,
including reverse faces. It covers 2,633 building, 95 prop
and 41 woody tree mesh groups. Another 386 chunks hold 129,754 visible ground
triangles. Generated meshes and scenes remain in the private art asset folder;
the public repository contains their generation and verification code.

Visible ground coverage is used for boundary queries. It ignores collisions with
the chassis and wheels, including in standalone review scenes, so original
source road collision continues to provide suspension contact. The runtime
requires visible and physical ground at matching heights under each chassis
corner. Invisible source collision beyond an exposed road cannot extend the
playable area, and distant lower ground cannot validate a fall from a bridge.
Movement checks include predicted yaw and a 15 cm margin for impulses applied
by the physics simulation after the guard's update.

`ArtDirectionReadabilityBuild` regenerates the supplement before verification
and the Windows build. `ArtDirectionCollisionReview` prepares native impact and
exposed-edge routes. Launch the visible player with
`-nfs-collision-probe <absolute-route-json> -nfs-stability-output <absolute-output-directory>`
to drive the real car into scenery and toward unsupported ground. The probe
records physical contacts, progress, boundary interventions, chassis support
and screenshots. It runs only with the explicit flag. Local evidence is stored
under `artifacts/NfsWorld/Stability/`.

The collision follow-up passed all 27 PlayMode tests and 23 EditMode tests.
After adding the physics margin and yaw prediction, all ten district boundary,
recovery and lifetime tests passed again. The Windows build passed scene,
source-collision and sign-layer verification; its runtime assembly was updated
at 22:29 Berlin time.

Early automatic edge candidates included city floor triangles beneath building
stairs. The original start did not satisfy the final chassis margin; moving it
inward spawned the car inside the stairs. Those diagnostic captures are retained
in `native-collision-final`, `native-collision-validated` and
`native-collision-margin-final`. Final route selection requires the runtime's
margin and a clear scenery approach. The saved routes sample this imported map;
the supplied bend has not been positively matched to a world coordinate.

The final native run passed all 20 cases across 2,400 measured physics steps.
Thirteen impacts covered signs/poles, buildings, woody trees and props, including
reverse travel. Each recorded physical contact and stopped before its planned
penetration limit. Six exposed-edge approaches and one reverse approach recorded
boundary interventions; maximum progress was 2.37–5.21 metres. Every case kept
the chassis footprint over matching visible and physical ground throughout its
measured steps. A separate distance check rejected recovery to a different part
of the map. Evidence is in `native-collision-final-20`; the routes are retained
in `collision-review-routes.json`.

The final rendered benchmark recorded 4,933 camera renders and 3,053 GPU timing
samples. All nine high-speed drives travelled 69.1–71.1 metres with source-road
wheel support on every measured frame, and no driving frame exceeded 40 ms.
Driving averaged 64.8–90.4 FPS with 12.12–16.66 ms p95 frame times. AC power was
connected before and after this run with the Power saver scheme active. This is
a local machine check, without controlled attribution against earlier runs.
The report is `performance-collision-final.json`.

## Fast travel and crash recovery (5 October motion follow-up)

The custom visibility and mesh-detail controllers previously updated every
0.2 seconds, with one threshold for switching in either direction. At 126 km/h
that interval covers seven metres. Camera movement around a threshold could
also repeatedly hide geometry or change its detail mesh. Visibility now checks
every 0.05 seconds, while the 146 spatial detail targets update each rendered
frame. Both use bounded camera-motion lookahead (0.35 seconds, at most 30 metres)
and different approach/departure thresholds. Teleports and long pauses clear
the motion estimate. Collision stays independent of those visual states.

The boundary guard previously rejected the car's current pose when its 15 cm
safety margin reached unsupported ground. Repeatedly restoring that pose and
zeroing velocity could block reverse as well as the original outward movement.
It now validates the exact chassis first and permits supported inward movement
until the full margin fits again. An unsafe turn can be cancelled independently
of a safe reverse movement. Invalid physical poses still roll back; outward
movement still stops, with the same visible/physical ground requirements.
Extreme or non-finite crash impulses cannot create an unbounded probe loop.

Moving-camera capture also exposed a separate repositioning problem: the camera
snap used the car's interpolated Transform, which could still contain its old
pose. The camera then travelled through geometry while catching up. Snapping now
uses the current Rigidbody pose and skips interpolation for that rendered frame.
Camera obstruction checks protect the actual smoothed position and inspect all
hits while excluding the player's own colliders. The previous nearest-hit-only
check could miss a wall when its first hit belonged to the car.

A bounded history records road-aligned poses with tyre contact and clearance
from scenery. Recovery revalidates support and occupancy before using a pose
within 25 horizontal metres of the car. R and fall recovery use that history.
If no recorded pose remains usable, recovery searches nearby real road surfaces,
matching visible/physical support and checking full chassis clearance.
A car that remains nearly stationary for two seconds while the player tries
to drive, and is tipped, overlapping scenery, continuously contacting an
obstacle, repeatedly blocked at a boundary or has no driven-wheel support,
automatically returns to a nearby clear road pose. Recovery reserves 35 cm of
horizontal clearance and moves a trapped car at least two metres. The check runs
before boundary rollback branches, so they cannot bypass it. Drive input wakes
the rigidbody. Initial district startup and explicit district transfers retain
the original supported spawn contract; the district spawn is also the emergency
fallback if no supported, clear local road position can be found.

All 32 PlayMode tests and 23 EditMode tests passed. The new checks exercise
threshold jitter, approach lookahead, simultaneous reversing/steering at an
edge, nearby recovery away from the district start, and a physically high-centred
car with its driven wheels off the road. The first full attempt stalled during
real-map loading and was stopped; its log is retained as
`artifacts/PlayTests/stalled-editor.log`. The final complete run passed.
After extending local search, all 14 district tests passed again. Five focused
boundary/recovery tests passed after the clearance and rollback corrections;
the final camera/recovery group also passed all five, including the new camera
snap regression. In total, 34 distinct PlayMode tests have been exercised across
these runs. Native crash qualification passed all 20 saved cases: 13 physical
scenery impacts and seven forward/reverse exposed-edge stops. All cases retained
support through the impact phase and drove more than two metres afterward.
Six trapped cases used automatic local recovery. Subsequent resets moved
0.02–2.84 metres, with none returning to the district start. Evidence is retained
in `native-motion-qualified`; earlier motion collision/recovery folders preserve
the failures that led to the corrections.

Native recovery review adds `-nfs-collision-recovery` to the existing collision
probe command. It tries to back away and steer after each impact or boundary
stop, counts physical travel separately from recovery teleports, and checks a
subsequent local reset. Escape review allows five seconds and chooses forward or
reverse from the car's orientation after the impact; steering starts after the
first second. Wheels are braked during initial settling so each case starts
without RPM carried over from the previous drive.
`-nfs-fast-review -nfs-stability-output <absolute-directory>` records first and
repeat passes through three high-speed corridors. Its fixed
30 FPS visual time and screenshot capture are for inspecting geometry, not
performance measurement. Use the rendered benchmark separately with
`-Visible -Game -Driving -FrameRateCap 30`. Normal gameplay retains its 60 FPS
cap, allowing higher rates on hardware with sufficient headroom.

The final moving-camera review passed all six first/repeat drives, travelling
69–72 metres per pass. All 360 recorded frames retained chassis support. The
90 captures show the road, car and nearby scenery from the first frame, without
the camera travelling through the map after repositioning. Reviewed corridors
showed no missing geometry or repeated visibility flashes. This samples three
routes; it does not establish that every intermittent rendering issue throughout
the map is gone. Evidence is in `fast-motion-camera-final`. Earlier
`fast-motion-qualified` captures retain the initial camera interpolation failure.

The final rendered benchmarks used the AMD Radeon integrated GPU / Ryzen 7 5700U,
1920×1080 output with 1440×810 internal rendering and FSR. AC power was connected
with the Power saver scheme active. At a 30 FPS cap, eight of nine drives held
approximately 30 FPS with 33.34–33.39 ms p95 frame times. One repeated drive had
a brief spike: 29.68 FPS average, 46.10 ms p95 and four frames above 40 ms.
The uncapped check then averaged 59.86–81.15 FPS while driving, with
15.68–19.20 ms p95 frame times and no driving frame above 40 ms. Every driving
frame in both reports retained source-road wheel support. These are local
machine measurements; the isolated capped-run spike has no proven attribution.
Reports are `performance-motion-camera-final-cap30.json` and
`performance-motion-camera-final-uncapped.json` under
`artifacts/NfsWorld/Stability/`. The strict Windows build passed; the final
`builds/windows/Alabama_Data/Managed/Alabama.Runtime.dll` was updated at
01:38:34 Berlin time on 5 October.

## Automatic performance and free crash motion (5 October)

Ordinary native gameplay now starts automatic graphics control from the map's
render-settings owner. GPU/RAM information only selects a starting quality
level; sustained measured frame times determine later changes. The controller
owns a private runtime copy of the URP pipeline, restores it and the prior FPS
cap on exit, and never writes quality changes to the imported art assets.
Fixed-setting benchmarks and collision/lifetime/capture probes retain their
explicit settings. Editor sessions do not start the controller automatically.

The target starts at 60 FPS. Two slow two-second windows lower graphics quality
before selecting 30 FPS at the lowest level. Render scale stays between 0.55
and 1.0; shadow distance stays between 60 and the reviewed 150 metres. Existing
FSR, sign materials, visibility lookahead and collision meshes remain available.
Strong sustained headroom increases quality and can return from 30 to 60 FPS.
Decisions have cooldowns; loading, pause and unfocused play discard samples.
Devices without reliable work timings periodically make a bounded 60 FPS retry.
No setting changes simulation time, the 120 Hz physics step, engine torque,
brakes or top speed. Extremely slow hardware can still miss the minimum target.
Active CPU/GPU work counters are used for headroom, rather than treating
FPS-cap waits as spare processing time; see the
[Unity 6.3 frame timing counter reference](https://docs.unity3d.com/6000.3/Documentation/Manual/frame-timing-manager-counter-reference.html).

Crashes retain pitch/roll impulses and supported motion along a map edge.
Only unsupported outward translation or yaw is constrained; projection never
adds momentum. A small 3 cm prediction allowance absorbs contact corrections
when the full 15 cm edge margin no longer fits. Short airborne rolls and jumps
can check their horizontal footprint at the last grounded road height, up to
12 metres above it, while retaining visible/physical map support requirements.
Grounded driving keeps its original downforce. Airborne, sideways or overturned
cars receive no aerodynamic force that could glue them to scenery or launch
them upward. The chase camera follows an upright heading through rolls. Local
recovery still releases a stopped trapped car when the player tries to drive;
it does not reset an ongoing fast roll.
Combined slide/turn predictions must fit together, so two individually safe
motions cannot push a corner off the map. A tyre on a tall prop cannot replace
the verified road-height reference. Recovery searches extend down to the road
below a high crash, with the same visible-ground and obstacle-clearance checks.
Support is checked again immediately after physics: suspension/contact impulses
can cross an edge after the prediction has run. The correction projects the
completed displacement and remaining velocity onto supported directions, rather
than cancelling every part of a step. A repeated late-impulse regression checks
every chassis footprint while requiring continued travel along the road.

`-nfs-auto-review -nfs-stability-output <absolute-directory>` qualifies the
controller in a visible native player: 20 seconds of actual high-speed driving,
45 seconds with an explicitly injected 28 ms CPU delay, then 40 seconds after
removing that delay. It checks selection of 30 FPS under sustained load,
return to 60 FPS, source-ground support, and unchanged tuning/physics time.
The injected phase tests decisions; it is not a hardware performance benchmark
and never runs in ordinary gameplay.

The native automatic test passed on the local Ryzen 7 5700U / AMD Radeon
integrated GPU. The final rebuilt player selected a 30 FPS target at 42.26
seconds under the injected load and returned to 60 at 72.98 seconds, eight
seconds after that load ended. The subsequent 1,711 frames averaged 59.94 FPS.
The injected phase actually averaged 25.06 FPS; selecting a target does not guarantee it under an artificial
CPU stall. Mean driving speed stayed between 34.91 and 34.94 m/s across the
three phases, with unchanged engine torque, maximum speed and physics step.
All sampled frames retained ground support. Evidence is
`artifacts/NfsWorld/Stability/automatic-native-verified/automatic-results.json`.
The earlier `automatic-native-final` report also passed before the added
post-physics support check; the repeated final run validates that extra work.

All 27 EditMode tests passed. PlayMode qualification covers 40 distinct tests:
the full run passed 37 of the then-38 tests, with the old assertion that every
edge impact must stop all motion subsequently corrected to require continuous
ground support and boundary intervention while allowing tangent travel. That
case and the new regressions passed focused reruns. After the final post-physics
change, all 11 affected crash/edge/airborne/automatic-pipeline tests passed in
`artifacts/AutoPostPhysicsFinalTests/results.xml`. Earlier failing results are
retained, including the late-impulse test that caught lost tangent displacement.
The attempted standalone real-map editor regression was stopped during an
unusually long scene load (`AutoRealEdgeTests/editor.log`); native qualification
uses the saved exact map route instead.

The final native collision review passed all 20 saved routes, including signs,
buildings, trees, props, six map edges and reverse impacts. Every 120-step impact
sample retained verified map support, allowing the bounded ground column during
airborne rolls. Boundary qualification requires support and intervention, while
scenery qualification also checks impact contacts and limited penetration; safe
travel along a boundary is not an escape simply because approach progress grows.
All 20 escape drives succeeded, with 3.25–38.33 metres of actual movement
excluding recovery teleports. All local resets stayed within 3.46 metres. The
formerly failing `edge-4` passed with three boundary interventions and 23.89
metres of subsequent driving. Evidence is `automatic-crashes-verified` under
`artifacts/NfsWorld/Stability/`; its predecessor and isolated
`automatic-edge-diagnostic` preserve the corner raycast that found empty ground
after a post-prediction suspension impulse.

The strict Windows build passed in `artifacts/AutoBuildFinal/editor.log`, updating
`builds/windows/Alabama_Data/Managed/Alabama.Runtime.dll` at 03:42:17 Berlin time
on 5 October. Run `builds/windows/Alabama.exe` with its accompanying data folders.

## Startup, scenery streaming and road-only resets — 5 October

The next systems implementation is documented in
[the phased engine plan](../engine-startup-streaming-reset-plan.md), including
code entry points, profiling methods and preserved intermediate failures.
The generated player now uses an asynchronous bootstrap, no optional splash,
build-time collision cooking, and 119 visual scenery cells. Road/terrain support,
exit closures and collision stay resident. Explicit R resets use road-only
navigation, physical footprint/clearance validation and road alignment; automatic
unwedging stays close to the crash, including on supported plazas.

The final systems review reached ready in 7.43 seconds on the local device and
passed six first/repeat 35 m/s drives, with p95 frames of 16.69–20.08 ms and no
missing required visible cells or support failures. The final comparable
launch-plus-route run took 18.68 seconds versus the 22.10-second baseline,
including the new destination preload. All 20 native crash/escape/reset cases
passed; maximum automatic recovery hop was 3.07 metres. The plan records cache
state, explicit-reset distance limits and the distinction between engine-ready
time and full process launch time.

The latest strict player build updated the runtime assembly at 23:28:58 Berlin
time on 5 October 2026. The executable remains `builds/windows/Alabama.exe`.

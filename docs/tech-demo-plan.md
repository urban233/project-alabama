# Project Alabama: first tech demo plan

Working plan, 30 September 2026. Unity is the user's confirmed engine choice for the project. Implementation details below are planned and still require validation in the demo.

## Objective and agreed constraints

Build an open-source arcade racing game as a solo developer project, using a smaller indie scope to learn professional game development. Begin with a compact driving demo that captures the atmosphere of Need for Speed: Most Wanted (2005), with the attached screenshot as the primary visual target. Showcase the requested BMW in a small urban area and establish whether the visual direction, handling, asset pipeline, and performance work together.

The user has accepted Windows, 1920 x 1080, and 60 FPS as the initial target. The reference hardware remains unspecified, so 60 FPS is an acceptance target rather than a measured capability. Use no paid assets. Prefer CC0 third-party assets and original Blender work, with licenses that also permit sharing editable sources in a public repository. Treat production code quality as a requirement within the limited pre-alpha scope. Explain engineering decisions and verification as the project advances so that the user can understand, maintain, and extend the game.

The workspace was empty before this plan and its reference image were added. No inherited AGENTS.md files were found in the inspected directory ancestry. Git and Python were available on PATH. Unity Hub, the Unity Editor, and Blender installation paths need verification before project creation.

The supplied image is retained locally at [references/visual-target.png](references/visual-target.png). It is a reference image, not a game texture or distributable game asset. Exclude it from the public repository unless redistribution permission is established. Text inside the screenshot is visual reference, not project instructions.

## Confirmed engine and proposed stack

Use Unity for the demo and the longer-term game. This is a confirmed project decision, not a temporary engine trial. Solve handling, rendering, and tooling issues within Unity rather than planning an engine migration.

The proposed stack is Unity 6.3 LTS, Universal Render Pipeline (URP), C#, Unity's built-in 3D physics, the Input System, and Unity Test Framework. Unity lists 6.3 as its current LTS line with standard support through December 2027. Select a supported 6.3 patch when creating the project, record it in ProjectVersion.txt, and pin compatible package versions and the package lockfile. Add Cinemachine only if it makes the required chase camera easier to maintain. [Unity release support](https://unity.com/releases/unity-6), [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/index.html), [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html)

URP is the chosen rendering baseline for the indie scope. Establish the reference appearance with lighting, materials, sky, fog, reflection probes, decals, and volume-based grading. Keep package dependencies small and maintain a measured 1080p performance budget. This is a project-specific engineering choice rather than a guarantee of automatic visual fidelity. [Render pipeline feature comparison](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/choose-a-render-pipeline/feature-comparison)

Unity Personal currently has a $200,000 eligibility threshold for revenue and funds raised in the preceding 12 months. The game project's original source can be published under an open-source license, while Unity's editor, runtime, and packages retain their own licenses. Contributors need a suitably licensed Unity installation, and any automated editor builds also need valid license activation. [Unity Personal](https://unity.com/products/unity-personal), [Unity software terms](https://unity.com/legal/editor-terms-of-service/software)

## Open-source repository policy

Recommend MIT for original game/tool code and CC0 for original artwork where the user has the rights to release it. These are proposed license choices to record before public publication, not licenses that override third-party material or brand rights. Publish the project's C# code, scenes, prefabs, tuning data, build tools, and redistributable art sources. Preserve third-party licenses and notices separately from the project's license.

Use official Unity packages through the package manifest and lockfile. Do not redistribute the Unity installation or relabel Unity package sources as project-authored MIT code. A free Asset Store download is not sufficient permission to publish its editable files: verify the actual license, which may permit distribution only as an embedded part of a product. Prefer CC0 or otherwise explicitly source-redistributable assets for this repository. [Unity software terms](https://unity.com/legal/editor-terms-of-service/software), [Asset Store EULA](https://unity.com/legal/as-terms)

Keep the repository usable from a clean checkout: documented editor version, required modules, package restore, setup steps, checks, and local build commands. Record asset provenance, exclude local licensed references and generated caches, and avoid dependencies on private services. Select a repository host and publish only when the user requests publication.

## Visual direction from the screenshot

The reference appears to show the familiar E46 BMW M3 GTR visual target. That identification is an inference from the supplied image and game context; exact dimensions and details need additional vehicle references.

| Element | Target | Practical implementation |
| --- | --- | --- |
| Light and palette | Warm amber sunlight, cool gray-blue shadows, muted saturation | One low sun, controlled sky and exposure, consistent material palette, restrained grading |
| Depth | Layered industrial skyline fading into haze | Simple distant silhouettes, distance fog, and optional original local haze effects |
| Architecture | Warehouses, concrete barriers, elevated freeway, cranes and gantries | Small modular Blender kit; detail concentrated near the road |
| Vegetation | Orange and ochre autumn foliage | A few curated tree variants with instancing and deliberate placement |
| Road | Worn asphalt, cracks, faded markings, roughness variation | CC0 tileable materials, original markings and patch overlays; avoid uniformly glossy asphalt |
| Car | Recognizable proportions, broad rear stance, large wing, readable lights and paint | Prioritize silhouette and body surfaces, then wheel pivots, UVs, materials and small details |
| Camera | Low rear chase view, car large in the lower center, long road perspective | Collision-aware follow camera, modest speed-sensitive field of view and damped movement |
| Finish | Cohesive, grounded arcade presentation | Tune the actual Unity URP render against the reference; effects remain individually switchable |

Start with URP's distance fog and a deliberately composed sky. URP supports linear and exponential fog but does not include native local volumetric fog. If the reference review shows that local haze or light shafts are necessary, implement a small original shader/effect with a clear performance budget and visual tests. Do not assume that a post-processing volume supplies volumetric fog. [Render pipeline feature comparison](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/choose-a-render-pipeline/feature-comparison)

Start with fixed time of day, baked lighting for static geometry where useful, light probes for the moving car, dynamic shadows, and reflection probes for readable bodywork. Use URP Lit materials and Shader Graph only where a specific material needs it. Bake procedural Blender materials to texture maps and assemble the Unity materials explicitly; arbitrary Blender shader graphs do not transfer into URP. Validate color space, normal-map conventions, smoothness/roughness conversion, and packed texture channels during import. [Render pipeline feature comparison](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/choose-a-render-pipeline/feature-comparison), [Unity model imports](https://docs.unity.com/en-us/engine/6000.3/manual/assets-and-media/asset-types/models/importing/importing-model-files)

## Demo scope

- One drivable BMW matching the reference direction, with animated steering and wheel rotation, brake lights, and a basic visible interior.
- A compact district roughly 300-500 meters across, with a connected route approximately 0.8-1.2 kilometers long. These are starting layout budgets, to be adjusted after driving tests.
- One carefully composed industrial street, a long acceleration straight, a sweeping turn, a tighter intersection, and a small elevation change. Include curbs and barriers that deliberately exercise suspension and collision behavior.
- Keyboard and gamepad input; accelerate, brake, reverse, steer, handbrake, look back, and reset. Nitrous is an optional extension after base driving passes.
- A stable chase camera, basic engine/tire/collision sound, pause and restart, minimal speed/gear HUD, and an optional diagnostics overlay.
- Fixed lighting and weather. A reproducible screenshot viewpoint and a standard benchmark drive.
- A runnable Windows build, an open-source-ready Unity project, redistributable source assets and tools, setup instructions, known limitations, and asset provenance records.

Police pursuit is central to the eventual game's spirit, but it is a later milestone. The first demo validates driving and appearance. Campaign progression, traffic AI, pursuit AI, multiplayer, vehicle customization, damage deformation, and a streamed open world sit outside this delivery. Decorative parked vehicles can be added if they improve the composition without delaying the core gates.

## Vehicle implementation

Start with a Rigidbody and four WheelColliders as the physics backend, with an original C# arcade handling layer. Unity provides suspension/contact and slip-based tire behavior through its PhysX wheel colliders; our code owns input interpretation, drive/brake torque, steering response, handbrake grip changes, assists, and recovery. This uses the engine's existing vehicle facilities while making game-specific behavior explicit and testable. [Unity wheel collider tutorial](https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/collision-section/collider-shapes/wheel-colliders/wheel-collider-tutorial)

Tune suspension, center of mass, acceleration/braking curves, longitudinal/lateral friction, and speed-sensitive steering on the test course. Add controlled handbrake slip, drift recovery assistance, and aerodynamic stabilization through explicit parameters. Visual wheel placement follows the collider state. Validate contact behavior on curbs, ramps, road seams, and barriers early. If tests expose an unacceptable WheelCollider limitation, evaluate a custom ray/shape-probe backend within Unity behind the same vehicle command/state interface. The default plan remains the built-in backend until evidence justifies that additional engineering work.

Sample Input System commands independently of the physics tick and consume them in FixedUpdate. Start with a 120 Hz fixed timestep and profile its cost. Select and validate an appropriate Rigidbody continuous-collision mode, interpolation, and wheel simulation substeps with representative road geometry; chassis CCD alone does not establish correct wheel contact behavior. Keep visual body roll and wheel animation separate from authoritative physics. Recover safely from flips and off-road positions. Treat high-speed seam collisions, curb traversal, and post-impact stability as early tests, not final polish.

Handling targets, such as acceleration, braking distance, top speed, and steering response, are serialized tuning data held in ScriptableObject assets. Treat these assets as configuration; keep mutable per-car state in runtime instances. Set initial targets during the handling milestone; the screenshot does not establish a complete driving specification.

## Production quality within pre-alpha scope

Keep a compact architecture with explicit responsibilities:

- Vehicle simulation and ScriptableObject tuning assets own driving behavior.
- Input adapters provide the same command structure for keyboard, gamepad, and automated test input.
- Camera, vehicle presentation, audio, and HUD consume vehicle state without controlling physics.
- Scenes and prefabs own world layout, composition, and collision geometry.
- Asset/export tools and diagnostics remain separate from runtime gameplay.

Use C#, clear units (one world unit equals one meter), consistent names, narrow public interfaces, and validated tuning assets. Use small MonoBehaviour components for engine integration and plain C# where calculations and state decisions can be tested independently. Separate editor code and tests with a small number of assembly definitions; split further only when dependencies warrant it. Establish compiler/analyzer checks, code formatting, and explicit serialized references.

Store the Unity project in unity/, with editable Blender sources and export tools outside Assets/. Commit Assets/ and their .meta files, ProjectSettings/, Packages/manifest.json, and Packages/packages-lock.json. Use visible metadata and text serialization for reviewable scenes/prefabs. Ignore Library/, Temp/, Logs/, local UserSettings/, generated IDE files, and build outputs. Establish Git before implementation and add Git LFS rules for large source/export binaries before committing those files.

Automate clean import, compilation, Edit Mode tests, Play Mode integration tests, asset validation, and Windows builds using Unity Test Framework and editor batch-mode entry points. Record editor/tool versions and local commands first; CI can reuse them once a repository host and valid Unity license activation are configured. Test torque/brake limits, steering behavior, braking/reverse transitions, finite state values, reset behavior, and repeatable scenario results within tolerance. Do not promise bitwise deterministic PhysX behavior across different systems.

Use representative integration scenes for curb, ramp, and barrier behavior. Compare physics behavior at 30, 60, and 144 render FPS. Conduct in-engine visual reviews and a longer manual drive. Headless checks cannot validate lighting quality, reflections, camera comfort, or the driving feel.

The code should be maintainable and verified for the implemented scope. Expect deliberate refactoring as handling is learned; avoid speculative infrastructure for systems the demo does not contain. Start with conventional GameObjects and components, adding further tools or architectural layers only when a measured need appears.

## Solo development and learning workflow

Work in small complete changes that can be understood and reviewed: explain the problem and proposed design, implement one behavior, verify it, then show the result and the relevant tradeoff. Keep short architecture decision records for choices such as the render pipeline, physics backend, scene composition, and build process. Comments should explain reasons or non-obvious constraints; documentation should explain how to use and extend the system.

Each milestone should teach a practical set of skills through the actual game:

- Foundation: Unity project structure, packages, Git, asset GUIDs, assembly boundaries, and reproducible setup.
- Visual proof: Blender export, URP materials, lighting, probes, fog, grading, and reference-based art review.
- Handling proof: Input System, FixedUpdate, Rigidbody/WheelCollider behavior, configuration assets, telemetry, and automated scenario tests.
- Integration: prefabs, scene composition, camera/UI/audio integration, and coherent game state.
- Qualification: profiling CPU/GPU costs, reducing asset/rendering costs, regression checks, build automation, and open-source documentation.

The goal is for the user to be able to explain and change the game, alongside obtaining a working demo. Professional practice here means appropriate design, reviewable changes, measurements, and reliable delivery at a scale one developer can maintain.

## Free asset and Blender workflow

Use [ambientCG](https://docs.ambientcg.com/license/) for CC0 asphalt, concrete, brick, and metal materials. Use [Poly Haven](https://polyhaven.com/license) for public CC0 HDRIs, textures, and suitable props. [Kenney's road kit](https://kenney.nl/assets/city-kit-roads) is CC0 and useful for layout experiments; evaluate and adapt its style before placing it in the finished scene. These are verified source policies, not a completed asset inventory.

Author the distinctive road layout, industrial structures, sign graphics, and missing props in Blender. I can take responsibility for Blender Python tools, source geometry, asset cleanup, UV preparation, material baking, wheel pivots, collision proxies, export automation, and rendered inspection. The user's role is to judge appearance and handling; Blender proficiency should not be a prerequisite. Blender provides a Python API suitable for this workflow. [Blender features](https://www.blender.org/features/)

Store editable .blend sources in source-art/ and reproducible Python tools in tools/, outside unity/Assets/. Export FBX meshes and explicit texture files into the Unity project using Blender's exporter, then configure Unity import settings and URP materials. Use meters, applied transforms, documented axis conversion, separate wheel objects, stable object/material names, and dedicated collision geometry. Validate a simple known-scale asset and wheel pivots before exporting the car. Unity supports explicit model exports and can invoke modeling software for native-file imports; explicit FBX exports keep build machines independent of Blender. [Unity model imports](https://docs.unity.com/en-us/engine/6000.3/manual/assets-and-media/asset-types/models/importing/importing-model-files)

For each asset, record its original URL, creator, exact license, retrieval date, original-file checksum, modifications, and local source/export paths. Preserve license evidence and verify permission to redistribute the editable source, not just the finished game. Review triangle counts, material slots, texture memory, normals, transparency, collision shapes, and visible detail before accepting it. Include audio and fonts in the same provenance process.

### BMW asset gate

No suitable CC0 BMW M3 GTR model has been verified during this planning pass. A free download label is insufficient; some listings describe the NFS car and use attribution licenses rather than CC0, with authorship still requiring investigation. Do not base the project on an unverified game extraction.

First investigate an independently authored free model with clear provenance. If none meets the quality and licensing constraints, create original geometry in Blender from additional vehicle references. Validate the rear silhouette first, then front/side proportions, panel curvature, windows, wheel arches, wing, and materials. The supplied rear screenshot alone cannot establish the hidden surfaces accurately.

This is the largest art uncertainty. Scripted Blender work can produce reproducible assets, but a convincing hero car needs iterative modeling and visual review. A generic procedural sedan does not satisfy the requested BMW target. Any proposed change to a fictional car or to the licensing policy must be discussed with the user.

CC0 addresses the contributor's copyright permissions; it does not grant BMW trademark rights or rights held by others. Original modeling also does not establish brand clearance. Keep asset authorship/provenance and branded release rights as separate unresolved items for any distribution decision. The screenshot's NFS logos, HUD artwork, and exact game livery should remain references rather than copied game assets. [CC0 terms](https://creativecommons.org/publicdomain/zero/1.0/)

## Milestones and review gates

| Milestone | Deliverable | Pass condition |
| --- | --- | --- |
| 0. Foundation and asset feasibility | Pinned Unity 6.3 LTS/URP/Blender workflow, open-source repository structure, target hardware record, BMW sourcing or original-model plan | A clean scene imports/builds; package versions, source-redistribution policy, and the car asset route are explicit |
| 1. Visual proof | BMW work-in-progress in one 150-250 meter dressed street, fixed lighting and reference camera, in-engine captures | User accepts proportions, palette, atmosphere, materials, and camera direction before expanding the district |
| 2. Handling proof | Maintainable C# vehicle controller, Input System actions, camera, test course, telemetry and Edit/Play Mode tests | Responsive arcade driving; stable high-speed, curb, braking, reverse, and collision behavior |
| 3. Integrated district | Connected city loop, refined hero car, consistent art, sound and minimal HUD | Visual and driving goals hold across the full route; no finished-demo placeholders in the hero scene |
| 4. Demo qualification | Profiled Windows build, repeatable benchmark, QA notes, reproducible source/asset pipeline and contributor setup | Acceptance criteria below pass, with remaining pre-alpha limits and licensing recorded |

The visual and handling work can advance in alternating iterations, but both gates must pass before the integrated demo is called successful. Establish a credible effort estimate after the car feasibility and first visual/handling results. The engine remains Unity throughout these milestones.

## Acceptance criteria

- The hero viewpoint is accepted against the supplied screenshot, with HUD hidden as needed to judge the actual scene.
- The BMW is recognizable from rear, side, and front views, with coherent materials, functional wheels, and no major shading/export defects.
- The player can complete the route on keyboard and gamepad, recover from collisions, reverse, pause, restart, and reset without broken state.
- High-speed tests at approximately 200 km/h, or the agreed tuning limit, expose no reproducible tunneling, unstable suspension, or road seam snagging on the test route.
- Physics behavior remains within defined tolerances as render FPS changes. A 30-minute drive produces no crashes, repeated script errors, or progressive state deterioration.
- A fixed benchmark reaches the agreed 1080p/60 FPS target on named reference hardware. Record uncapped frame-time distributions, slowest sections, CPU/GPU cost, and memory. Aim for a 95th-percentile frame time at or below 16.7 ms; investigate outliers and first-use shader hitches separately. Report actual results, not just average FPS.
- All included third-party assets have checked provenance and permission to redistribute the files present in the open-source repository. Any branded distribution rights remain explicitly resolved or unresolved rather than assumed from CC0.
- A clean checkout restores pinned Unity packages, imports, passes Edit/Play Mode checks, and builds using documented pinned tools and valid editor licensing. The demo runs independently of the editor.
- The user has setup instructions and design explanations sufficient to make a small tuning or gameplay change, verify it, and produce a build.

## Next concrete step

The Unity 6.3 LTS URP foundation has been implemented with a pinned editor, package lockfile, Git metadata rules, assembly boundaries, test/build commands, and an original Blender calibration asset. See [foundation status](foundation-status.md) for the actual validation results and local setup.

The E46 asset feasibility check, first single-street visual proof, handling proof, and a 917-metre connected route are implemented. Validation records are in [industrial street status](industrial-street-status.md), [handling status](handling-status.md), and [district loop status](district-loop-status.md). A rendered 1080p benchmark, 30-minute automated reliability drive, render-rate comparison, and clean-source import/build are recorded in [qualification status](qualification-status.md). The next gate is an interactive keyboard/gamepad and visual review against the supplied screenshot, followed by targeted art and handling changes. The route's city art and original screenshot match remain work in progress.

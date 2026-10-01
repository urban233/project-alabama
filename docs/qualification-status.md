# District demo qualification — 1 October 2026

The qualification target is the 917.0-metre `DistrictLoop` player at 1920 × 1080 on Windows. This is a pre-alpha engineering measurement, not visual approval or a claim that the complete first demo is finished.

The measurements below used the complete local demo with artwork restored. The public source repository excludes artwork and starts with a separate code-only history. Follow the README to import the matching asset ZIP before reproducing these checks. The earlier asset-inclusive Git/LFS checkout check below is historical local evidence, not the public distribution workflow.

## Reference system and method

- Windows 11 Pro, build 26200; AMD Ryzen 5 3400G (4 cores / 8 threads); 16 GB system RAM; NVIDIA GeForce RTX 4060 (8 GB), driver 610.74.
- Unity 6000.3.25f1, Universal Render Pipeline, Direct3D 11, strict **non-development** Windows build, windowed 1920 × 1080, VSync off.
- `tools/qualify.ps1 -Mode Benchmark` launches a visible rendering player with no frame cap. Its `RouteFollower` drives two laps. Lap one warms the scene; the second supplies frame-time and route-section samples. Unity's frame timing API supplies CPU main/render and GPU samples. Resident and allocated memory come from Unity profiling counters.
- `tools/qualify.ps1 -Mode Reliability -DurationSeconds 1800` runs the same route follower for 30 minutes at a 60 FPS cap. It checks off-road/height bounds, lap completion, error logs, and memory. The automated driver follows the route conservatively; it does not test subjective handling or every player input.
- A graphics-enabled **batch** player was rejected as a measurement method after producing approximately 5,193 FPS with zero GPU timing samples: it did not render camera frames. Its data is excluded. The accepted final benchmark has 9,831 GPU timing samples and a visible game window.

## Rendered benchmark

| Metric | Result |
| --- | ---: |
| Measured frames (second lap) | 10,262 |
| Mean FPS (uncapped) | 230.1 |
| Frame time p50 / p95 / p99 / max | 4.43 / 6.06 / 6.62 / 10.78 ms |
| CPU main-thread p95 | 3.47 ms |
| CPU render-thread p95 | 3.91 ms |
| GPU p95 | 4.42 ms |
| Slowest route quarter | First, 6.33 ms p95 |
| Resident memory start / end / peak | 197.2 / 315.5 / 316.1 MB |
| Allocated memory start / end / peak | 71.2 / 119.4 / 131.4 MB |
| Script errors, maximum road offset | 0, 1.32 m |

The final release executable's 6.06 ms frame-time p95 is below the 16.7 ms target on this PC. It is a single local run, so it is a baseline rather than a cross-machine guarantee. The JSON and player log are in `artifacts/Qualification/benchmark.json` and `artifacts/Qualification/benchmark.log` (ignored local evidence). Frame timings are sampled in the player; the instrumentation itself has a cost. The first-use/warmup lap is deliberately excluded from the percentile.

## Thirty-minute reliability drive

The visible 1920 × 1080 player drove for **1,800.0 wall-clock seconds** at a 60 FPS cap, completing **40 laps** and recording **107,585 rendered frames**. It reported zero script errors, a maximum 2.31 m offset from the route centreline, and a car-height range of 0.09–0.22 m. Frame time p50/p95/p99 was **16.67/16.69/17.85 ms**; GPU p95 was 8.73 ms. One 804 ms outlier occurred while a separate clean Unity project was importing and building on the same PC. Because of that concurrent workload, the isolated uncapped benchmark above is the FPS acceptance measurement.

Unity allocated memory was **119.0 MB after the warmup lap** and **119.8 MB at the end** (+0.9 MB, with a 131.1 MB peak). Resident memory was **309.1 MB after warmup** and **262.1 MB at the end** (310.9 MB peak). These samples show no progressive growth over this automated 30-minute drive. Full data and logs are in `artifacts/Qualification/reliability.json` and `.log` (ignored local evidence).

## Render-rate comparison

The same two-lap visible-player run used caps of 30, 60, and 144 FPS with VSync off. Lap one warmed the scene; the measured second lap followed the same route using 120 Hz fixed physics.

| Render cap | Measured lap | Maximum route offset | Car-height range | Frame-time p95 | Errors |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 30 FPS | 44.602 s | 1.312 m | 0.099–0.104 m | 33.34 ms | 0 |
| 60 FPS | 44.604 s | 1.321 m | 0.099–0.104 m | 16.67 ms | 0 |
| 144 FPS | 44.615 s | 1.311 m | 0.099–0.104 m | 6.95 ms | 0 |

The lap-time spread is 0.013 seconds and the maximum-offset spread is 0.010 m. These conservative guided-lap results show no material render-rate sensitivity in this scenario; they do not cover collisions or player steering at every cap. JSON and player logs are in `artifacts/Qualification/benchmark-cap{30,60,144}.*` (ignored local evidence).

## Source and asset checks

Seven Edit Mode and six Play Mode tests pass, including road-collider lane raycasts and a guided full lap. The strengthened straight-line case starts at 200.2 km/h, checks lane and suspension height at every fixed step, and ended 111.2 m down the street at 198.4 km/h. This does not test a corner or curb at that speed. The strict release player built successfully. `python tools/verify_assets.py` matched SHA-256 for 97 recorded files across five assets, found their license evidence, and checked Unity `.meta` files. A separate copy of the final `Assets`, `Packages`, and `ProjectSettings` imported from an empty Library, passed all 7 Edit Mode and 6 Play Mode tests, and produced a strict release executable. The final-source run is under `artifacts/CleanImport/20261001-183751/` (ignored local evidence), and can be repeated with `tools/verify-clean-import.ps1`. The source tree has a pinned Unity editor version and package lockfile. A local initial commit contains the demo; a clean Git/LFS clone restored all 97 recorded asset files without checksum differences, imported, passed all 13 tests, and built directly from its checkout. That asset-inclusive commit remains local; public distribution uses the code-only repository plus the separately shared ZIP.

An intentionally deeper nested clean-import path in that clone failed during URP preprocessing: a package shader resolved to 263 characters. Building directly from the shorter clone path (225 characters for the same shader) succeeded. `tools/verify-clean-import.ps1` now uses a shorter output path and checks a representative URP path before launching Unity. The revised script passed import, all 13 tests, and a strict build from the cloned source at `artifacts/Checkout/final2/artifacts/c/213408/` (ignored local evidence). Keep the project near a drive root on Windows; this is a toolchain path constraint, not a game-code failure.

## Remaining acceptance gates

Interactive keyboard/gamepad use, comfort and handling feel, and high-speed stability through the loop's corners still need direct testing. The current visual capture is substantially brighter and simpler than the requested Most Wanted-inspired screenshot; the user's atmosphere and car approval remain open. Sound, traffic, and HUD polish are also incomplete. The E46 adaptation is CC-BY-SA 3.0 with source credit recorded; BMW branding rights for public distribution are unresolved.

For current commands and tuning instructions, see the [README](../README.md). The broader acceptance criteria are in the [tech demo plan](tech-demo-plan.md).

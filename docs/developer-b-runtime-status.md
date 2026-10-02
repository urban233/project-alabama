# Developer B district runtime qualification

Verified on 2 October 2026. Accepted starting point: merged `origin/main` at
`0194d80`. Tested implementation: `05ee091` on
`codex/developer-b-district-runtime` (preceding checkpoint `dbdf37f`).

## Implemented behavior

The generated persistent scene owns one car/input, chase camera, bootstrap,
telemetry and shared sunlight, atmosphere, pipeline and map cap. It loads
`DowntownContent.unity` additively and remains the active scene. District metadata,
collision, culling and recovery data have explicit registration/unregistration
lifetimes. Repeated loads are idempotent. Invalid/duplicate districts are rejected.

The loader rejects removal of the last district or a district whose horizontal
bounds/current wheel contacts support the car. An explicit retained recovery
destination permits controlled removal after checking that scene's road footprint
and chassis clearance. The existing car/camera move before source support is
removed; input and pause remain intact. Airborne cars retain protection. Normal
wheel contact changes reset ownership when entering another loaded district.

The standalone generators and playable art scene remain intact. All original
NFS assets are unchanged, including the approved palette, lighting/shadows,
collision and six closed exits. No automatic streaming or model/texture changes
were introduced. The [v1 integration contract](district-integration-contract.md)
defines scene ownership, the shared coordinate frame, bounds, recovery poses,
paired connections/collision ownership and lifetime hooks for A's review before
dependent district generation.

## Verification

| Check | Result | Local evidence |
| --- | --- | --- |
| Python suite | 42 passed | `python -m unittest discover -s tools/tests -v` |
| Full Unity EditMode suite | 16 passed, zero skipped | `artifacts/EditTests/results.xml` |
| Full Unity PlayMode suite | 17 passed, zero skipped | `artifacts/PlayTests/results.xml` |
| Lifetime PlayMode cases | Six included in full suite: repeated/duplicate/invalid loads, controlled paused recovery, airborne support, invalidated destination and real Downtown driving/reloads | `DistrictLifetimeTests` |
| Additive Downtown collision | 13,834/13,834 samples over 1,158 road meshes; maximum error 0.765 mm | `artifacts/NfsWorld/collision-RuntimeContent.json` |
| Standalone Downtown collision | Same samples/error; original static collision retained | `artifacts/NfsWorld/collision-Art.json` |
| Runtime configuration occlusion | Host plus Downtown baked; does not qualify a future neighbouring district | `artifacts/NfsWorld/RuntimeOcclusion/editor.log` |
| Strict Windows builds | Both runtime and standalone art players succeeded | `artifacts/NfsWorld/RuntimeBuild/editor.log`, `ArtBuild/editor.log` |
| Original artwork/metadata/licenses | 97 recorded files across five assets verified | `tools/verify_assets.py` |
| Accepted NFS inventory | All 86,417 expected files present and unmodified | `asset_pack.py verify --lock docs/asset-packs/nfs-world.lock.json` |
| PowerShell tools | Syntax parsed successfully; new build/probe/benchmark paths executed | `tools/nfs-world.ps1`, `qualify-district-lifetime.ps1`, `benchmark-nfs-world.ps1` |

The lifetime tests exercised six synthetic reload cycles and two real Downtown
reload-and-drive cycles, including four-wheel contact and movement on imported
collision. Existing standalone driving/elevated-road/high-speed/exit-wall checks
also passed. Synthetic platforms are lifecycle fixtures, not a neighbouring district.

## Visible Windows player lifetime

The opt-in rendered probe verified six distant fixture cycles and two real
Downtown unload/reload cycles with controlled recovery onto retained fixture
support. It rendered 488 camera frames, balanced 10 registrations with 10
unregistrations, and returned to exactly **38,298 GameObjects / 2,280 colliders**.
Player/camera/input identity, input enabled state, pipeline, fog, sun/shadows,
30 FPS cap, pause, closed exits and real-road support/recovery all passed.
There were no reported script errors or failed checks.

| Operation | Wall time | Mean measured interval | Maximum frame interval |
| --- | ---: | ---: | ---: |
| Distant fixture load (six cycles) | 64.9–82.6 ms | 31.3–33.4 ms | 33.8–34.7 ms |
| Distant fixture unload (six cycles) | 30.3–33.9 ms | 32.6–33.4 ms | 34.3–35.2 ms |
| Controlled Downtown unload (two cycles) | 117.5 / 127.5 ms | 42.2 / 43.9 ms | 117.7 / 127.7 ms |
| Downtown reload (two cycles) | 955.9 / 994.8 ms | 38.5 / 38.6 ms | 188.9 / 195.4 ms |

These are explicit transitions with visible hitches. The probe measures wall-clock
Update intervals across each operation and five following frames; startup and
120 warmup frames precede measurement. Reloads use warm assets. It does not claim
seamless streaming or cold-load latency. Allocated Unity memory was **1,112.3 MB
initially / 1,153.8 MB finally** (+41.5 MB, decimal units). Object/registration counts
returned to baseline; this short run is not evidence of long-duration memory
stability or a guarantee that unused asset memory is released on scene unload.

Probe JSON/log and the inspected post-reload player image are retained under
`artifacts/NfsWorld/DeveloperB/player-runtime-lifetime.*` and
`runtime-player-after-reload.png`.

## Comparable rendering and driving samples

Both rebuilt strict players ran sequentially with no Unity Editor/Blender workload:
AMD Ryzen 7 5700U, AMD Radeon(TM) integrated graphics, Direct3D 11, mains confirmed
before/after each run, unchanged Windows **Power saver**, VSync off. Output was
1920×1080; the approved .75 render scale / FSR1 gave 1440×810 internal resolution.
Sunlight, fog, exposure, shadows, materials and source geometry were retained.

The existing benchmark uses five stationary road viewpoints (120 warmup / 360
measured frames per view) and nine two-second high-speed traversals across three
clear source-road corridors. It requires camera rendering and GPU samples.

| Samples | Standalone mean FPS | Runtime mean FPS | Standalone p95 range | Runtime p95 range |
| --- | ---: | ---: | ---: | ---: |
| Uncapped stationary | 40.3–61.5 | 45.7–62.7 | 18.26–32.21 ms | 17.80–25.11 ms |
| Uncapped moving | 46.0–58.5 | 41.9–58.7 | 19.79–25.69 ms | 19.45–26.96 ms |
| 30 FPS stationary | 29.99–30.00 | 29.99–30.00 | 33.34–33.37 ms | 33.34–33.36 ms |
| 30 FPS moving | 29.99–30.00 | 29.99–30.00 | 33.34–33.39 ms | 33.34–33.41 ms |

The uncapped runtime's maximum measured stationary/moving frame times were
**32.06 / 29.65 ms**, versus **52.89 / 33.97 ms** standalone. Standalone recorded
five uncapped stationary frames over 40 ms; runtime recorded none. One runtime
moving repeat was slower than its standalone counterpart. These single sequential
runs include thermal/cache variance and a fresh runtime occlusion bake; they do
not establish a guaranteed speedup.

Each capped player measured 2,340 frames and 4,020 camera renders. Both had
**540/540 supported moving frames** (at least three source-road wheel contacts),
zero measured frames over 40 ms and near-30 FPS pacing in these samples.
Runtime maximum frame time was **33.83 ms** (moving 33.72 ms), versus **33.90 ms**
standalone. Maximum per-view p99 was 33.64 ms runtime / 33.61 ms standalone.
Uncapped moving support was 963/963 runtime / 970/970 standalone.
End-of-benchmark allocated-memory snapshots were approximately 1,121 MB in both
players. Capped raw GPU timestamps are retained as driver output; use uncapped
timing for bottleneck comparison.

All four JSON reports/logs and the compact comparison are retained in
`artifacts/NfsWorld/DeveloperB/`. Prior accepted capped evidence was copied to
`DeveloperB/accepted-main-cap30/` before the fresh run. This is sample-based
Downtown regression evidence, not qualification of every street or two districts.

## Private contribution and integration dependencies

Root candidate prepared with the documented contributor command:

`project-alabama-work-nfs-world-developer-b-0.0.20261002212635505859.zip`

SHA-256: `2b0bf68622ddcc50e1872a0d7d78631d5a9b13394a18ef682e36b9ca8b9f4408`

The ZIP is 3,079,302,020 bytes and has a matching `.zip.sha256` sidecar in the
project root. Privately share **both files**, together with this B branch/code
revision and validation record. Nothing has been transferred by preparing it.

This is a full contributor snapshot of 86,435 files, based on accepted NFS World
0.1.0 (`ca86c3db…`). Comparison with the installed baseline found **18 additions,
zero modifications and zero deletions**. A should integrate only the owned additions:
`unity/Assets/Alabama/Art/Maps/NfsWorld/Runtime.meta` and `Runtime/**`, preserving
all their metadata. These comprise the runtime/content scenes, shared occlusion
data and the explicitly synthetic fixture scenes. The complete delta inventory
is private at `artifacts/NfsWorld/DeveloperB/private-contribution-inventory.json`.

Accepted locks and installed receipts remain **base 1.0.0 / NFS World 0.1.0**.
The original ZIPs and assets remain intact. The local baseline and B's derived
assets are verified; the candidate is not a newly accepted release. A owns
review/integration and publication, after which B must receive/verify that release.

Existing root ZIP checksums (retained, no new transfer required for a colleague
who already has these matching releases):

- `project-alabama-assets.zip` (base 1.0.0 legacy filename):
  `46db668164b01a8a54b0b55338574a8ebcb58eb4d72fee0ccb9e3aa88c4a8a38`
- `project-alabama-nfs-world-0.1.0.zip`:
  `db8fdd53a814573df1066f862980ef375c2964509d53826df76a2c6fd8ee1076`

**Not performed because A's accepted neighbouring content/paired connection data
has not been supplied:** a real two-district crossing in both directions, seam
height/lane/overlap qualification, combined occlusion and combined performance.
All six exits therefore stay closed. A must review the contract before dependent
generation and provide the neighbouring scene, supported spawns/bounds and paired
connection/collision ownership. Longer combined route/soak qualification belongs
to that integration delivery. No real neighbouring connection is claimed here.

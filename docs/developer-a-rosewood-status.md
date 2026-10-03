# Developer A Rosewood qualification

Qualification date: 2026-10-03. Source baseline: `e1e6232b0bfe0d9210ce1c67a07c618ea5c0cf4a`.
Developer B's private runtime contribution was reviewed separately: 18 additions,
with no changes or deletions to the accepted 0.1.0 inventory. A integrated those
owned additions and retains their original Unity metadata.

## Connected content

Downtown exit 3 and Rosewood exit 7 form the accepted reciprocal connection.
The other five Downtown and eight Rosewood exits remain closed. Both districts
use the original shared coordinate frame and one persistent car, input, camera,
sunlight and rendering owner. The combined occlusion bake and strict Windows
player build passed. See [the recipe](rosewood-integration.md) for reproduction.

Rosewood collision verification passed 12,504 samples across 1,050 road meshes;
maximum height error was 0.001523 m. The seam surface check passed 305 samples
with no missing surface or duplicate district contact. Protected texture checks
passed, including 135 exact alpha comparisons. Accepted colour geometry contains
3,203,842 triangles versus 3,208,879 source triangles (0.157% reduction); exact
32-metre shadow chunks retain all 3,203,842 accepted triangles. This is a baseline
conversion, not a claim of substantial geometry reduction.

Runtime packaging verified 32,465 Downtown and 50,031 Rosewood mesh copies with
identical vertex/index bytes, bounds and submesh ranges. Original assets and
standalone scenes remain unchanged. During the final rebuild, packaged editor
scene loading measured about 25 seconds for Downtown and 30 seconds for Rosewood;
these are workstation measurements, not target-hardware guarantees.

## Rendered driving measurements

Hardware: Ryzen 5 3400G, NVIDIA RTX 4060 (8 GB), 16 GB system RAM. Windows was on
mains with the Balanced power plan before and after both runs. Output was
1920×1080, internal rendering 1440×810 through FSR1 (0.75 scale), with the approved
lighting and shadows. Unity 6000.3.25f1 rendered actual camera frames and supplied
nonzero GPU timings. No editor or other conversion job ran during these drives.

| Two opposing source-lane legs | Capped | Uncapped |
| --- | ---: | ---: |
| Distance / measured driving time | 1.972 km / 202.27 s | 1.972 km / 202.26 s |
| Average FPS | 30.00 | 162.52 |
| Frame time p95 / p99 | 33.34 / 33.36 ms | 8.73 / 13.24 ms |
| Maximum frame / frames over 40 ms | 33.88 ms / 0 | 24.62 ms / 0 |
| GPU frame time p95 | 13.40 ms | 2.47 ms |
| Peak Unity allocated memory | 2.246 GB | 2.250 GB |
| Initial combined player startup | 13.36 s | 12.80 s |

Each run crossed the seam once per leg, retained at least three supported wheels
on every sampled physics step, and preserved gameplay/render ownership. The two
legs have independent initial spawns; neither leg teleports or writes body
position/velocity while driving. Both endpoints stay inside the retained Palmont
frontier. Captures cover the seam and route quarters in each direction. The first
untrimmed source-lane trial stopped at that closed frontier; the corrected recipe
does not open it or change original traffic data.

The rendered lifetime regression also passed: ten registrations/unregistrations,
unchanged final object/collider counts, fixture cycles, controlled recovery and
two Downtown reloads. It unloads Rosewood first and measures Downtown lifetime;
the actual combined route supplies separate Rosewood seam proof. First/second
Downtown reloads took 963/626 ms with maximum rendered frames of 339/193 ms.
These transition hitches remain material; districts currently load together at
startup rather than streaming during the measured drive.

## Verification and private handoff

Python: 48 tests passed. Asset file/license/metadata verifier passed. Unity
EditMode: 16 tests passed. Full PlayMode: all 19 tests passed with no skips,
including reciprocal gate lifetime, the original driving/high-speed/closure
checks and all eight retained Rosewood barriers. The barrier test selects an
actually settled road-supported approach on banked geometry before each impact;
its production-car impact and stop/pass-through assertions remain intact.
The release preservation audit passed: all 86,417 accepted 0.1.0 records remain
byte-identical, with no deletions; all reviewed B candidate metadata is retained.
The three superseded runtime assets are the combined host, derived Downtown scene
and occlusion data. The release adds 108,179 files beyond the baseline and B's 18
additions. Base 1.0.0 is unchanged. Integrated `nfs-world` 0.2.0 contains 194,614
files (14,173,582,956 bytes), with content ID
`26c9d89d7aa3780d1faeb027ed0062be6fc112fd420fcd5a5ef1116352dfc237`.
Export and receipt verification passed: all 194,614 ZIP entries were validated,
with zero files changed or removed. Both required/installed releases match:
base 1.0.0 and `nfs-world` 0.2.0. The full workspace check found zero missing or
modified files. A subsequent strict accepted-release rebuild also passed and
preserved the host, both content scenes, combined occlusion and their metadata
byte for byte. Accepted builds retain the publisher's qualification identity;
regenerated trial builds still refresh it before seam qualification.

The accepted ZIP/checksum and separate private verification ZIP/checksum are
prepared directly in the project root. The latter contains the handoff record,
reports, captures and test evidence. No private transfer has been performed.

Main subsequently advanced to `fe2e19f` with documentation/reference-pack changes
only. The new [adaptation guide](art-direction/2026-10-02-nfsmw/agent-reference-pack/ADAPTATION-GUIDE.md)
supports the existing guarded geometry, UV/alpha, texture-bake and approved-lighting
workflow. This conversion remains a baseline; it does not claim a complete match
to every new concept image. No runtime code or asset lock changed in that merge.

Reports and captures remain private under `artifacts/NfsWorld/Rosewood/`, with
lifetime evidence under `artifacts/NfsWorld/player-runtime-lifetime.*` and Unity
XML/logs under `artifacts/EditTests/` and `artifacts/PlayTests/`.

Developer B must privately receive the matching release and source revision,
review the conversion and independently drive the connection. The Ryzen 7 5700U
integrated-GPU 30 FPS target still requires B's mains-powered qualification;
RTX 4060 measurements do not establish it. Request `urban233` on the completed
PR only after both developers verify the same revision. Artwork and ZIPs remain
outside Git and GitHub.

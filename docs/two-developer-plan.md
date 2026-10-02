# Two-developer plan: connect and improve Rockport

The next milestone is a several-minute drive through Downtown and one connected
neighbouring district, with an improved Downtown route and measured 30 FPS on the
Ryzen 7 5700U integrated GPU, on mains at 1080p output. Keep the approved autumn
lighting/shadows. Traffic, police and pursuit systems are later milestones.
A and B are role labels; choose which developer takes each.

## Shared starting point

Review the import PR with `urban233` and choose the accepted code/asset revision
as the baseline. Both developers restore the pinned base and NFS packs using
[asset versioning](asset-versioning.md). Use Unity 6000.3.25f1, Blender 4.4, the
same package lock and world origin. Keep assets/ZIPs outside Git; commit and push
code, recipes, profiles, small locks and docs on dedicated `codex/` branches.
Use the [same chat prompt](shared-asset-prompt.md) in both checkouts. Place privately
received ZIPs in each project root and run `python tools/asset_handoff.py receive`.
Exported releases and contributor candidates also go in the root, ignored by Git.

Agree on the source-to-Unity coordinate frame and district connection records.
Independently centering each district would misalign adjoining roads. Record
ownership of each connection, collider/road overlap and exit barrier.

## Ownership

| Area | Developer A: expansion | Developer B: Downtown quality/gameplay |
| --- | --- | --- |
| Conversion | Parameterize district inputs and own next-district configuration | Review coordinate/scene contracts |
| Content | Own next district's Blender sources, visuals and collision | Own Downtown's authored visual improvements |
| Shared runtime | Supply district load/spawn/bounds data | Own persistent gameplay, common rendering and district loader |
| Performance | Test new district and connections | Own benchmarks, Downtown LOD work and combined qualification |
| Asset release | Publish the integrated `nfs-world` pack | Supply accepted changes and verify the received pack |
| Review | Review B's code and drive the improved route | Review A's conversion and drive the connection |

A owns publication of the shared pack so its lock is not updated independently
by both developers. Shared runtime/import contracts need the other's review
before dependent generation. Use separate workspaces; avoid concurrent edits to
one Blender file, Unity scene, config or generated asset directory.
A configures `--publish-pack nfs-world` in the root handoff helper; B configures
no publishing pack and uses `contribute` for privately reviewed artwork changes.
Private transfer remains separate from code pushes. A integrates owned files and
deletions, then publishes one accepted version that both developers receive.

## Deliverable 1: reusable district boundaries

Developer A inspects Camden/Rosewood candidates against the physical-road audit,
choosing one verified connection with useful road variety and manageable scope.
Parameterize source members, shared origin/transform, output root, spawn candidates
and bounds. Import roads and a visible baseline into a separate district scene/
asset root while retaining Downtown outputs.

Developer B separates persistent gameplay from content: one car, camera, input,
bootstrap and owner of common sunlight, fog, pipeline and frame cap. Load districts
additively; initially load both together and introduce streaming if combined
measurements require it. Recovery/spawns and culling registrations must follow
district lifetime. Unloading content must not reset shared rendering or remove
the player/camera.

Together, inspect the combined overview and drive the seam both ways. Check
height, scale, lane edges, bridge levels, duplicate collision and missing surfaces.
Remove only the barrier at a verified imported connection. Retain the others.
Review/bake occlusion for the combined configuration; the standalone bake does
not automatically validate two loaded districts.

Acceptance: one persistent car crosses without teleporting, falling, duplicate
road contact or a lighting/pipeline change. Other exits remain visibly closed;
Downtown standalone generation still works.

## Deliverable 2: expansion and art improvement in parallel

Developer A applies the established texture/material recipe to the neighbour,
checks protected lettering and cutout alpha, validates multi-level roads and
remaining exit barriers, and captures representative views including the seam.
Keep the content scene free of duplicate cars/cameras/bootstrap objects.

Developer B chooses a dense street, junction and elevated/industrial segment
that can join the new district in a useful driving route. Author simpler building
silhouettes, facades and props while preserving landmarks, openings and markings.
Add useful distance LODs to measured expensive objects. The present 0.50% triangle
reduction is the starting point, not a substantial geometry reduction already done.
Keep source driving collision independent of visuals. Compare fixed before/after
camera views and geometry counts, plus frame times/draw calls/memory with lighting
unchanged. Extend accepted changes to further Downtown streets afterward.

Acceptance: both districts share the visual direction; the selected Downtown
route shows real geometry improvement in captures and counts, with driving intact.

## Deliverable 3: integrated qualification and asset release

A integrates accepted district and Downtown assets in the publisher's workspace,
regenerates affected scene data and creates the next NFS snapshot/ZIP. B imports
that ZIP and independently verifies the same code/asset revision.

Run Python and full Unity EditMode/PlayMode suites. Extend map checks to the new
district, seam and remaining barriers. Build the Windows player and drive a
several-minute route both ways, including curves, junctions, ramps and crossings.
The short repeatable benchmarks remain regression samples rather than a substitute
for this longer run. Test load/unload transitions explicitly if streaming is added.

Record output/internal resolution, charger/power plan, frame percentiles, hitches,
memory and relevant geometry counts. The intended frame budget is 30 FPS / 33.3 ms;
report occasional spikes alongside averages. Compare uncapped runs for headroom
and capped runs for play pacing.

Handoff: version/content ID, ZIP SHA-256, code commit, capture/test locations,
performance and remaining issues. Commit only the small lock/profile and source
changes; share the ZIP privately. Both developers verify it before requesting
`urban233` on the completed PR. Main changes through the reviewed merge process.

## Following milestone

Add a small checkpoint race/time trial on the validated route: start, ordered
checkpoints, finish time, restart and recovery. Playtest it before choosing another
district. Use combined measurements to decide between more content, deeper art/
LOD work or streaming; keep each subsequent district a bounded feature change.

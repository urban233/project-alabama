# Separate asset releases

The existing README ZIP handoff remains supported. Named releases add a human
version, a checked-in lock and safe upgrades while keeping artwork outside Git.
Run commands from the repository root with Unity and Blender closed. Use Python
3.11 or newer; this tool needs no additional packages.

Both developers use the [same chat prompt](shared-asset-prompt.md). Asset ZIPs
are imported from and exported into each checkout's **project root**, then
transferred privately. Root ZIPs, checksums and `local-asset-handoff.json` are
ignored by Git. No private storage service or GitHub asset upload is configured.

## Packs and records

| Pack | Initial version | Contents | Git records |
| --- | --- | --- | --- |
| `base` | `1.0.0` | Existing reviewed car, original environment, sources and licenses | `docs/asset-pack.json`, `docs/asset-packs/base.lock.json` |
| `nfs-world` | `0.1.0` | Local map scenes/models/materials/textures, editable Blender files, extracted map inputs and neighbouring roads | `docs/asset-packs/nfs-world.profile.json`, `docs/asset-packs/nfs-world.lock.json` |

The base lock pins the original manifest without changing it. The existing
`project-alabama-assets.zip` and original README commands remain compatible.
The NFS pack is a separate private prototype handoff; conversion does not establish
redistribution rights. Neither ZIP belongs in Git, GitHub releases or public downloads.

A profile selects explicit asset roots and dependencies. A lock records the
name/version, content ID, full-manifest identity, file/byte totals and dependencies.
The detailed NFS file list stays in ignored
`artifacts/asset-packs/nfs-world/<version>/asset-pack-manifest.json` and inside the
ZIP. The receiver does not need that manifest separately.

The NFS profile excludes Blender backups, Unity caches, builds, benchmark evidence,
the supplied screenshot, experimental shadow-source copies and the full original
7-Zip archive. Keep the archive separately if more districts will be extracted.
Rebuild source-specific reports with the documented map pipeline when authoring;
the received scenes can be verified and played without those old reports.

## Receive or update assets

Check out the agreed code revision, close the editors and obtain the ZIP versions
named by its locks through the private handoff. Put the ZIPs in the project root:

```powershell
python ./tools/asset_handoff.py status
python ./tools/asset_handoff.py receive
```

The helper discovers checked-in locks and restores dependencies first. It accepts
the original base ZIP name too. `status` checks presence and installed receipts;
`receive` verifies the actual manifest and every asset before changing that pack.
It preflights all filenames, but imports are atomic per pack, not across packs.
For individual packs, the equivalent commands are:

```powershell
python ./tools/asset_pack.py import ./project-alabama-assets.zip --lock docs/asset-packs/base.lock.json
python ./tools/asset_pack.py import ./project-alabama-nfs-world-0.1.0.zip --lock docs/asset-packs/nfs-world.lock.json
python ./tools/asset_pack.py status --lock docs/asset-packs/nfs-world.lock.json
python ./tools/asset_pack.py verify --lock docs/asset-packs/base.lock.json
python ./tools/asset_pack.py verify --lock docs/asset-packs/nfs-world.lock.json
python ./tools/verify_assets.py
./tools/nfs-world.ps1 -Action ArtVerify
```

The map dependency check requires the pinned base files to be present and unchanged.
Use the README toolchain instructions for an editor outside the standard Hub path.
`ArtBuild` generates the local player; the ZIP contains assets, not executable builds.
Use the current lock's version/filename when it advances beyond these examples.

The same import command handles a newer release. Every ZIP entry, declared size
and SHA-256 is checked before changing assets. The installed manifest is retained
in ignored `artifacts/asset-packs/installed/<name>.json`. Files can be replaced or
removed only if their bytes match the installed release. Missing files are restored,
matching files are left alone, and unrelated assets are retained. Each ZIP is a full
snapshot, so direct upgrades between nonadjacent versions are supported.

If local edits conflict, import stops before changing assets. Copy/export that
work into a separate folder, or integrate it with the publisher before updating.
There is no force-overwrite flag. Never regenerate metadata to resolve a conflict.
Replaced/removed bytes and `upgrade.json` are saved in
`artifacts/asset-backups/<name>/<timestamp>/`. A failed install reverses prior writes.

Keep the installed receipt: deleting all of `artifacts/` loses the old inventory
needed for safe upgrades. Re-import the previously installed pinned ZIP to restore
the receipt if it was deleted; local edits remain protected. `status` and `verify`
exit nonzero on missing/modified expected files. They do not inventory unrelated,
unversioned assets in a developer's workspace.

## Publish a revision locally

One developer publishes each pack. Combine both developers' accepted changes in
the publisher's workspace before taking a snapshot; do not release competing
contents under the same name/version. Assign the role once on that machine:

```powershell
python ./tools/asset_handoff.py configure --publish-pack nfs-world
```

The contributor runs `configure` without `--publish-pack`. This ignored local
setting lets the same chat prompt distinguish publication from contributions;
it is a coordination convention, not authentication. If the contributor changes
artwork, `python tools/asset_handoff.py contribute --pack nfs-world --label developer-b`
creates a separate full candidate ZIP/checksum in the root. Its embedded manifest
records the installed baseline (or the current lock if no receipt exists). The
shared release locks/receipts stay unchanged. The publisher reviews/integrates
owned files and explicit deletions before releasing a combined pack. Candidate
snapshots are not automatically merged or installed as accepted releases.

1. Integrate and inspect changes with original metadata. Finish relevant collision
   checks, captures, tests and rendered-player measurements for an accepted map release.
2. Close Unity/Blender and update the profile only if new roots are needed.
3. Snapshot a strictly newer version and export it into the project root. The
   helper registers the publisher's receipt by importing its own verified ZIP;
   matching assets stay untouched:

```powershell
# Example next revision; 0.1.0 is already pinned.
python ./tools/asset_pack.py snapshot --profile docs/asset-packs/nfs-world.profile.json --version 0.1.1 --lock docs/asset-packs/nfs-world.lock.json
python ./tools/asset_handoff.py export --pack nfs-world
python ./tools/asset_pack.py verify --lock docs/asset-packs/nfs-world.lock.json
```

Snapshot checks sidecars and duplicate Unity GUIDs. Export streams large files,
verifies they still match the snapshot, refuses existing output paths and prints
the ZIP SHA-256, also saving a `.zip.sha256` sidecar. Fast compression and ZIP64
are the default; `--compression stored`
trades a larger ZIP for faster packing.

4. Commit only the profile, lock, tooling/recipes and notes on the feature branch.
   Privately share the ZIP, its SHA-256 and the corresponding code commit. Retain
   old accepted ZIPs as well as code revisions. The receiver places the files in
   their project root and runs `receive`; exporting locally does not transfer
   files to another machine. GitHub carries only the code/locks/docs.
5. The colleague restores and verifies the exact revision and runs relevant Unity
   checks before review. Request `urban233` on the PR and use the reviewed merge process.

Use patch versions for corrections/art/performance changes, minor versions for
new districts or asset families, and major versions for incompatible scene or
dependency layouts. Editing an existing asset preserves its GUID; a version bump
is not permission to regenerate metadata.

For another pack, add a reviewed profile with `schemaVersion: 1`, `name`, explicit
`include` roots and optional `exclude` patterns/dependency lock paths. An
`includeManifest` can select files from an existing curated manifest, so the base
can evolve without collecting unrelated map files. Update provenance/license
records separately when its artwork changes. Asset paths are restricted to
`source-art/` and `unity/Assets/Alabama/Art/`.

## Return to a previous release

Check out the old code containing its old locks, close the editors, then restore
the retained ZIP explicitly:

```powershell
python ./tools/asset_pack.py import ./project-alabama-nfs-world-0.1.0.zip --lock docs/asset-packs/nfs-world.lock.json --rollback
python ./tools/asset_pack.py verify --lock docs/asset-packs/nfs-world.lock.json
```

The target lock must describe an older version than the installed receipt. Local
edits are protected during rollback too, and newer bytes are backed up. A fresh
workspace uses normal import. Git alone cannot restore ignored artwork.

## Tests

Run `python -m unittest discover -s tools/tests -v`. Synthetic fixtures cover old
ZIP compatibility, pins, GUIDs, corruption/path rejection, local edits, obsolete
files, backups, direct upgrades, rollback and recovery after a failed write.
Root handoff fixtures also cover dependency ordering, missing private ZIPs,
publisher configuration, immutable output names, contributor baselines and
preservation of shared release locks/installed receipts.

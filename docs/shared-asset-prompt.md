# The same asset prompt for both developers

Paste this exact prompt into a chat opened in either developer's checkout. Give
the development task in the same message or a following message. Roles are stored
locally, so the prompt does not need a different developer name or ZIP path.

> Read README.md, applicable repository instructions, docs/asset-versioning.md,
> docs/two-developer-plan.md and docs/shared-asset-prompt.md. Follow the session
> procedure in docs/shared-asset-prompt.md before and after my development task.
> Import and export asset ZIPs directly in this project's root. Keep the assets,
> ZIPs and local handoff settings out of Git and GitHub; we transfer ZIPs privately.
> Preserve Unity metadata and local artwork edits. Use the asset versions pinned
> by our agreed code revision. Complete the task, verify the result, and prepare
> the appropriate private ZIP if artwork changed. Ask for missing role/revision
> information rather than guessing. Report the exact files I must privately send
> or obtain, the code commit and the verification results.

## Session procedure for the agent

1. Read the instructions above. Inspect the Git branch and local changes. Work on
   a dedicated feature branch; preserve existing work. Establish the agreed code
   revision using session context. If the developers' code revisions differ, use
   their agreed feature/integration revision before receiving its assets. Respect
   existing authorization for Git fetch/push; do not reset or switch away from
   uncommitted work. Never push assets or merge/push directly to main.
2. Run `python tools/asset_handoff.py status`. It discovers the current checked-in
   pack locks, installed receipts and matching root ZIP filenames. This is a
   presence check; it does not prove ZIP integrity or unchanged workspace assets.
   Ignore a newer ZIP if this checkout's locks still require an older version.
3. Establish editor state. Keep Unity and Blender closed for an import/snapshot/
   export; do not terminate an editor with unsaved work. Once closed, run
   `python tools/asset_handoff.py receive`. It imports all pinned packs from the
   project root in dependency order. Then run `python tools/verify_assets.py` and
   the relevant documented Unity verification, such as `ArtVerify` for Rockport.
   Use the pinned toolchain and the user's activated Unity license.
4. If a ZIP is absent, request that exact filename/version through the private
   handoff. Never download artwork from GitHub or substitute/regenerate missing
   GUIDs. On a local-edit conflict, preserve the work and report the conflicting
   paths; do not force import, delete files or roll back implicitly. Continue
   independent code work when possible. Do not claim that a failed receive made
   the workspace current: imports are atomic per pack, not across all packs.
5. Perform the requested development task within the ownership in the developer
   plan. Keep original `.meta` files. Record which asset roots/files were changed,
   added or removed; `status`/`verify` alone cannot detect new untracked artwork.
   Avoid jointly editing the same scene/Blender file. Finish relevant rendering,
   collision, performance and code checks before accepting an asset change.
6. After the task, if no artwork changed, keep the existing ZIPs. If artwork
   changed, read ignored `local-asset-handoff.json`. If publishing ownership is
   unknown, ask which pack this developer publishes; do not assign both machines
   the same pack. Save agreed roles with `configure` as shown below. This is a
   coordination setting, not an access-control system.
7. **Assigned publisher:** integrate the other developer's accepted contributions
   first. Review their embedded baseline and file inventory in a separate ignored
   review directory. Integrate only agreed owned files, including explicit
   deletions, with their metadata; do not replace the whole workspace with a
   contribution snapshot. Resolve overlapping edits with the other developer.
   Complete shared checks, choose a strictly newer version according to the
   documented version rules, snapshot it and export it as below. Do not export
   modified artwork against the old release lock. Never overwrite an existing
   published ZIP/version. Commit/push only the reviewed code/recipes/locks/docs
   on the authorized feature branch. Prepare the matching code commit and ZIP
   checksum for the private handoff.
8. **Contributor:** run `contribute` as below to capture current artwork in a
   separate root ZIP. Use a short developer/work label. This includes the
   profile's full asset snapshot
   and its baseline identity; send the publisher the ownership/change/deletion
   list and validation results too. The candidate does not advance the team's
   release lock. After integration, privately obtain the publisher's accepted
   ZIP, update the agreed code revision and receive it. Preserve the original
   contribution before resolving any remaining local conflicts.
9. End with the exact root ZIP filenames/checksums, pinned/installed versions,
   code revision, tests and outstanding private transfers/conflicts. Label
   contributions as candidates. Say whether the local workspace is verified or
   still awaiting an accepted ZIP. A local export does not deliver files to the
   other machine. Transfer only through the user-authorized private channel;
   otherwise leave the files ready for the user to send. Request `urban233` when
   the completed code change is ready for PR review; do not merge main.

## One-time publishing roles

The plan assigns Developer A publication of the integrated `nfs-world` pack.
Confirm who holds A's role before setting up a machine. In that checkout:

```powershell
python tools/asset_handoff.py configure --publish-pack nfs-world
```

In the contributor's checkout:

```powershell
python tools/asset_handoff.py configure
```

For multiple publishing assignments, repeat `--publish-pack`. Configure replaces
the list. It writes only ignored `local-asset-handoff.json`; no private path,
credentials or artwork are committed. Receiving assets needs no configuration.

## Publisher commands after accepted artwork changes

Run from the project root with the editors closed. The version is an example;
read the current lock and choose its next appropriate version first.

```powershell
python tools/asset_pack.py snapshot --profile docs/asset-packs/nfs-world.profile.json --version 0.1.1 --lock docs/asset-packs/nfs-world.lock.json
python tools/asset_handoff.py export --pack nfs-world
python tools/asset_pack.py verify --lock docs/asset-packs/nfs-world.lock.json
```

Export produces `project-alabama-nfs-world-0.1.1.zip` and its `.zip.sha256` directly
in the root and registers the publisher's installed receipt. Retain accepted old
ZIPs. Privately transfer the new ZIP/checksum and matching code commit. Each
developer places received files directly in their own checkout's root:

```powershell
python tools/asset_handoff.py receive
```

## Contributor commands after artwork changes

```powershell
python tools/asset_handoff.py contribute --pack nfs-world --label developer-b
```

This writes `project-alabama-work-nfs-world-developer-b-0.0.<timestamp>.zip` and
its checksum in the root. The timestamp is an identifier for a candidate, not an
accepted release version. The shared locks and installed receipts stay unchanged;
no publishing role is required. The selected pack needs its checked-in profile.
Keep the printed filename and send it privately with a clear change/deletion list.

## What stays up to date

Both developers can verify identical accepted assets after the matching private
transfer and receive. Their in-progress artwork can differ until it is integrated.
GitHub carries code and small version locks. ZIPs carry artwork and editable
sources privately. There is no automatic upload, cloud synchronization or merge
of two developers' binary edits. The same prompt coordinates these steps and
reports an outstanding transfer instead of silently claiming success.

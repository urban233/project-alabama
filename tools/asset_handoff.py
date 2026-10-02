"""Import/export the pinned private asset ZIPs in the project root."""
import argparse
from datetime import datetime, timezone
from pathlib import Path
import zipfile

import asset_versions as assets

CONFIG = "local-asset-handoff.json"


def configure(publishers):
    for name in publishers:
        assets.pack_name(name)
    assets.save_json(assets.local(CONFIG), {"schemaVersion": 1, "publishPacks": sorted(set(publishers))})
    print("Saved this machine's publishing roles in ignored local-asset-handoff.json.")


def publishers():
    path = assets.local(CONFIG)
    if not path.exists():
        return []
    config = assets.read_json(path)
    if config.get("schemaVersion") != 1 or not isinstance(config.get("publishPacks"), list):
        raise ValueError("Invalid local handoff configuration")
    for name in config["publishPacks"]:
        assets.pack_name(name)
    return config["publishPacks"]


def locks():
    """Read required releases from this code checkout, dependencies first."""
    found = {}
    for path in sorted(assets.local("docs/asset-packs").glob("*.lock.json")):
        lock = assets.load_lock(path)
        assets.safe_relative(lock["archiveName"])
        if Path(lock["archiveName"]).name != lock["archiveName"]:
            raise ValueError("Archive name must be a filename")
        if lock["archiveName"] != f"project-alabama-{lock['name']}-{lock['version']}.zip":
            raise ValueError("Archive name must use the ignored project-alabama-<pack>-<version>.zip format")
        if lock["name"] in found:
            raise ValueError(f"Duplicate pack lock: {lock['name']}")
        found[lock["name"]] = (path, lock)
    if not found:
        raise ValueError("No asset release locks found")
    ordered, visiting, visited = [], set(), set()
    def visit(name):
        if name in visiting:
            raise ValueError("Asset dependency cycle")
        if name in visited:
            return
        visiting.add(name)
        path, lock = found[name]
        for reference in lock.get("dependencies", []):
            dependency = assets.load_lock(assets.local(reference["lockPath"]))
            if reference["name"] not in found or dependency != found[reference["name"]][1]:
                raise ValueError("Dependency lock is not in the handoff lock directory")
            if any(dependency[key] != reference[key] for key in ("name", "version", "packId")):
                raise ValueError(f"Dependency pin changed: {reference['name']}")
            visit(reference["name"])
        visiting.remove(name)
        visited.add(name)
        ordered.append((path, lock))
    for name in found:
        visit(name)
    return ordered


def archive_for(lock):
    archive = assets.local(lock["archiveName"])
    # The original reviewed base ZIP remains supported without renaming it.
    if not archive.is_file() and lock["name"] == "base" and lock["version"] == "1.0.0":
        archive = assets.local("project-alabama-assets.zip")
    if not archive.is_file():
        raise ValueError(f"Place the privately supplied {lock['archiveName']} in the project root.")
    return archive


def status():
    assigned = publishers()
    available = True
    print("ZIP folder: project root. Required releases follow this code checkout's locks.")
    for _, lock in locks():
        receipt = assets.installed(lock)
        current = receipt["version"] if receipt else "unregistered"
        print(f"{lock['name']}: required={lock['version']}, installed={current}, publisher={lock['name'] in assigned}")
        try:
            archive = archive_for(lock)
            print(f"  {archive.name}: present (receive verifies content, not just the filename).")
        except ValueError as error:
            print(f"  {error}")
            available = False
    return available


def receive():
    # Preflight all required filenames before importing any pack. Atomicity is
    # per pack; a later conflict leaves any successfully restored dependencies.
    required = [(path, lock, archive_for(lock)) for path, lock in locks()]
    for path, _, archive in required:
        assets.import_release(argparse.Namespace(lock=path, zip=archive, rollback=False))
    print("Received the exact pinned releases from the project root. Local edits remain protected.")


def export(name):
    if name not in publishers():
        raise ValueError("Assign one publisher for this pack, then run configure --publish-pack <name> on that machine.")
    candidates = [(path, lock) for path, lock in locks() if lock["name"] == name]
    if not candidates:
        raise ValueError("Unknown pack")
    path, lock = candidates[0]
    output = assets.local(lock["archiveName"])
    assets.export(argparse.Namespace(lock=path, output=output, compression="fast"))
    # Register the authoring baseline too: the next upgrade can distinguish
    # accepted assets from new local edits. Export already checked snapshot bytes.
    assets.import_release(argparse.Namespace(lock=path, zip=output, rollback=False))
    print("ZIP and SHA-256 sidecar are in the project root. Transfer them privately; Git does not send them.")


def contribute(name, label):
    """Capture a contributor's work without advancing the team's release lock."""
    assets.pack_name(label)
    work_name = assets.pack_name(f"work-{name}-{label}")
    candidates = [lock for _, lock in locks() if lock["name"] == name]
    if not candidates:
        raise ValueError("Unknown pack")
    baseline = candidates[0]
    receipt = assets.installed(baseline)
    if receipt:
        baseline = {"name": receipt["name"], "version": receipt["version"],
                    "packId": receipt["manifest"]["packId"]}
    profile = assets.read_json(assets.local(f"docs/asset-packs/{name}.profile.json"))
    if profile["name"] != name:
        raise ValueError("Profile has the wrong pack name")
    profile["name"] = work_name
    # Each contribution is a distinct full snapshot; accepted release locks stay
    # untouched. Its manifest identifies the release against which edits began.
    release = "0.0." + datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S%f")
    directory = assets.local(f"artifacts/asset-packs/contributions/{work_name}/{release}")
    profile_path, lock_path = directory / "profile.json", directory / "lock.json"
    assets.save_json(profile_path, profile)
    assets.snapshot(argparse.Namespace(profile=profile_path, version=release, lock=lock_path))
    lock = assets.load_lock(lock_path)
    manifest_path = assets.local(lock["manifestPath"])
    manifest = assets.read_json(manifest_path)
    manifest["contribution"] = {key: baseline[key] for key in ("name", "version", "packId")}
    assets.save_json(manifest_path, manifest)
    lock["manifestSha256"] = assets.identity(manifest)
    assets.save_json(lock_path, lock)
    assets.export(argparse.Namespace(lock=lock_path, output=assets.local(lock["archiveName"]), compression="fast"))
    print("Private contribution prepared in the project root. The publisher must review/integrate owned files before releasing it.")
    print("This candidate is not an accepted release; the shared locks and installed receipts are unchanged.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="action", required=True)
    p = sub.add_parser("configure")
    p.add_argument("--publish-pack", action="append", default=[])
    sub.add_parser("status")
    sub.add_parser("receive")
    p = sub.add_parser("export")
    p.add_argument("--pack", required=True)
    p = sub.add_parser("contribute")
    p.add_argument("--pack", required=True)
    p.add_argument("--label", required=True, help="Short developer/work label, for example developer-b")
    args = parser.parse_args()
    try:
        if args.action == "configure": configure(args.publish_pack)
        elif args.action == "status":
            if not status(): parser.exit(1, "Some required private ZIPs are missing.\n")
        elif args.action == "receive": receive()
        elif args.action == "export": export(args.pack)
        else: contribute(args.pack, args.label)
    except (ValueError, OSError, KeyError, TypeError, zipfile.BadZipFile) as error:
        parser.exit(1, f"Private handoff failed: {error}\n")


if __name__ == "__main__":
    main()

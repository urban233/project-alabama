"""Named, pinned asset ZIP releases; binary assets and full map manifests stay local."""
import argparse
from datetime import datetime, timezone
import fnmatch
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import tempfile
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MANIFEST_NAME = "asset-pack-manifest.json"
STATE = Path("artifacts/asset-packs/installed")
MAX_MANIFEST_BYTES = 64 * 1024 * 1024
CHUNK = 1024 * 1024


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")


def identity(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def read_json(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"Duplicate JSON key: {key}")
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding="utf-8-sig"), object_pairs_hook=unique)


def save_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".partial")
    try:
        temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8", newline="\n")
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def version(value):
    if not re.fullmatch(r"(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)", value):
        raise ValueError("Use a numeric MAJOR.MINOR.PATCH version, for example 0.1.0")
    return tuple(map(int, value.split(".")))


def pack_name(value):
    if not re.fullmatch(r"[a-z][a-z0-9-]{0,63}", value):
        raise ValueError("Pack name must be lowercase letters, digits and hyphens")
    return value


def safe_relative(value, asset=False):
    path = PurePosixPath(value)
    if not value or path.is_absolute() or path.as_posix() != value or ".." in path.parts or "\\" in value or ":" in value:
        raise ValueError(f"Invalid relative path: {value}")
    reserved = {"CON", "PRN", "AUX", "NUL", *(f"COM{i}" for i in range(1, 10)), *(f"LPT{i}" for i in range(1, 10))}
    if any(part.endswith((".", " ")) or part.split(".")[0].upper() in reserved for part in path.parts):
        raise ValueError(f"Invalid Windows path: {value}")
    if asset and not (value.startswith(("source-art/", "unity/Assets/Alabama/Art/")) or value == "unity/Assets/Alabama/Art.meta"):
        raise ValueError(f"Path is outside the asset roots: {value}")
    return path


def local(value, asset=False, checked_parents=None):
    safe_relative(value, asset)
    path = ROOT / value
    if reparse_path(path):
        raise ValueError(f"Refusing symbolic-link path: {value}")
    for parent in path.parents:
        if checked_parents is not None and parent in checked_parents:
            continue
        if reparse_path(parent):
            raise ValueError(f"Refusing symbolic-link path: {value}")
        if checked_parents is not None:
            checked_parents.add(parent)
    return path


def reparse_path(path):
    # Junctions can redirect Windows directories even when is_symlink() is false.
    try:
        attributes = path.lstat()
    except FileNotFoundError:
        return False
    return stat.S_ISLNK(attributes.st_mode) or bool(getattr(attributes, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))


def hash_stream(stream, destination=None):
    digest = hashlib.sha256()
    size = 0
    while chunk := stream.read(CHUNK):
        digest.update(chunk)
        size += len(chunk)
        if destination is not None:
            destination.write(chunk)
    return size, digest.hexdigest()


def matches(path, record):
    if not path.is_file() or path.stat().st_size != record["size"]:
        return False
    with path.open("rb") as stream:
        return hash_stream(stream)[1] == record["sha256"]


def validate_manifest(manifest):
    if manifest["schemaVersion"] not in (1, 2):
        raise ValueError("Unsupported asset manifest schema")
    if manifest["schemaVersion"] == 2:
        pack_name(manifest["name"])
        version(manifest["version"])
    seen = set()
    for record in manifest["files"]:
        safe_relative(record["path"], asset=True)
        if record["path"].casefold() in seen:
            raise ValueError(f"Duplicate asset path: {record['path']}")
        seen.add(record["path"].casefold())
        if type(record["size"]) is not int or record["size"] < 0 or not re.fullmatch(r"[0-9a-f]{64}", record["sha256"]):
            raise ValueError(f"Invalid file record: {record['path']}")
    if identity(manifest["files"]) != manifest["packId"]:
        raise ValueError("Asset manifest identity does not match its file list")
    return manifest


def load_lock(path):
    lock = read_json(path)
    if lock["schemaVersion"] != 1:
        raise ValueError("Unsupported asset lock schema")
    pack_name(lock["name"])
    version(lock["version"])
    safe_relative(lock["manifestPath"])
    for key in ("packId", "manifestSha256"):
        if not re.fullmatch(r"[0-9a-f]{64}", lock[key]):
            raise ValueError(f"Invalid {key} in asset lock")
    return lock


def check_lock(manifest, lock):
    validate_manifest(manifest)
    if manifest["packId"] != lock["packId"] or identity(manifest) != lock["manifestSha256"]:
        raise ValueError("This asset manifest belongs to a different pinned release")
    if manifest["schemaVersion"] == 2 and (manifest["name"], manifest["version"]) != (lock["name"], lock["version"]):
        raise ValueError("Asset release name/version does not match the lock")
    if len(manifest["files"]) != lock["fileCount"] or sum(r["size"] for r in manifest["files"]) != lock["totalBytes"]:
        raise ValueError("Asset release totals do not match the lock")


def release_lock(manifest, name, release, manifest_path, dependencies):
    return {"schemaVersion": 1, "name": name, "version": release,
            "packId": manifest["packId"], "manifestSha256": identity(manifest),
            "manifestPath": manifest_path, "archiveName": f"project-alabama-{name}-{release}.zip",
            "fileCount": len(manifest["files"]), "totalBytes": sum(r["size"] for r in manifest["files"]),
            "dependencies": dependencies}


def load_release_manifest(lock):
    return validate_manifest(read_json(local(lock["manifestPath"])))


def dependencies(lock, verify=False):
    owned = {}
    for reference in lock.get("dependencies", []):
        dependency = load_lock(local(reference["lockPath"]))
        if any(dependency[key] != reference[key] for key in ("name", "version", "packId")):
            raise ValueError(f"Dependency pin changed: {reference['name']}")
        manifest = load_release_manifest(dependency)
        check_lock(manifest, dependency)
        for record in manifest["files"]:
            if verify and not matches(local(record["path"], True), record):
                raise ValueError(f"Restore the pinned {reference['name']} pack first: {record['path']}")
            owned[record["path"]] = record
    return owned


def pin(args):
    manifest_path = args.manifest.resolve().relative_to(ROOT.resolve()).as_posix()
    manifest = validate_manifest(read_json(local(manifest_path)))
    pack_name(args.name)
    version(args.version)
    if args.lock.exists():
        raise ValueError("Lock already exists; create a new snapshot to publish an upgrade")
    if manifest["schemaVersion"] == 2 and (manifest["name"], manifest["version"]) != (args.name, args.version):
        raise ValueError("Pin name/version must match the existing manifest")
    save_json(args.lock, release_lock(manifest, args.name, args.version, manifest_path, []))
    print(f"Pinned {args.name} {args.version}; existing ZIP contents are unchanged.")


def snapshot(args):
    profile = read_json(args.profile)
    if profile.get("schemaVersion") != 1:
        raise ValueError("Unsupported asset profile schema")
    name = pack_name(profile["name"])
    version(args.version)
    previous = load_lock(args.lock) if args.lock.exists() else None
    if previous and (previous["name"] != name or version(args.version) <= version(previous["version"])):
        raise ValueError("A new snapshot needs the same name and a strictly newer version")
    selected = set()
    excludes = profile.get("exclude", []) + ["**/*.blend1", "**/*.blend2", "**/__pycache__/*", "**/*.pyc"]
    if "includeManifest" in profile:
        curated = validate_manifest(read_json(local(profile["includeManifest"])))
        selected.update(record["path"] for record in curated["files"])
    for value in profile.get("include", []):
        base = local(value, True)
        if not base.exists():
            raise ValueError(f"Missing profile input: {value}")
        for path in ([base] if base.is_file() else base.rglob("*")):
            relative = path.relative_to(ROOT).as_posix()
            if reparse_path(path):
                raise ValueError(f"Refusing symbolic-link input: {relative}")
            if path.is_file() and not any(fnmatch.fnmatchcase(relative, pattern) for pattern in excludes):
                selected.add(relative)
    # Include folder metadata up to Art.meta and require a sidecar for every Unity
    # asset/folder. Both developers restore identical GUIDs, rather than generating them.
    metadata = set()
    for value in list(selected):
        if not value.startswith("unity/Assets/Alabama/Art/"):
            continue
        asset = local(value, True)
        candidates = [asset] if not value.endswith(".meta") else []
        candidates += [parent for parent in asset.parents if parent == ROOT / "unity/Assets/Alabama/Art" or parent.is_relative_to(ROOT / "unity/Assets/Alabama/Art")]
        for candidate in candidates:
            meta = candidate.with_name(candidate.name + ".meta")
            relative = meta.relative_to(ROOT).as_posix()
            metadata.add(relative)
    for relative in sorted(metadata):
        # File metadata is already present in the directory inventory. Only
        # sidecars outside the selected roots require a separate filesystem probe.
        if relative not in selected and not local(relative, True).is_file():
            raise ValueError(f"Missing Unity metadata: {relative}")
        selected.add(relative)
    print(f"Selected {len(selected)} files; hashing the snapshot.", flush=True)
    if not selected:
        raise ValueError("The profile selected no asset files")
    references = []
    for value in profile.get("dependencies", []):
        dependency = load_lock(local(value))
        references.append({"lockPath": value, **{key: dependency[key] for key in ("name", "version", "packId")}})
    dependency_files = dependencies({"dependencies": references}, verify=True)
    records = []
    guids = {}
    checked_parents = set()
    for index, value in enumerate(sorted(selected)):
        path = local(value, True, checked_parents)
        with path.open("rb") as stream:
            size, checksum = hash_stream(stream)
        record = {"path": value, "size": size, "sha256": checksum}
        if value in dependency_files and record != dependency_files[value]:
            raise ValueError(f"This pack would change a dependency-owned asset: {value}")
        if value.startswith("unity/") and value.endswith(".meta"):
            if not path.with_name(path.name[:-5]).exists():
                raise ValueError(f"Orphaned Unity metadata: {value}")
            match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8-sig"), re.MULTILINE)
            if not match:
                raise ValueError(f"Invalid Unity GUID: {value}")
            guid = match.group(1)
            if guid in guids:
                raise ValueError(f"Duplicate Unity GUID: {guids[guid]} and {value}")
            guids[guid] = value
        records.append(record)
        if index and index % 10000 == 0:
            print(f"Hashed {index}/{len(selected)} asset files.", flush=True)
    manifest = {"schemaVersion": 2, "name": name, "version": args.version,
                "packId": identity(records), "previousPackId": previous["packId"] if previous else None,
                "files": records}
    path = f"artifacts/asset-packs/{name}/{args.version}/{MANIFEST_NAME}"
    if local(path).exists():
        raise ValueError("This release already exists; use a new version")
    validate_manifest(manifest)
    save_json(local(path), manifest)
    save_json(args.lock, release_lock(manifest, name, args.version, path, references))
    print(f"Snapshotted {name} {args.version}: {len(records)} files, {sum(r['size'] for r in records):,} bytes.")


def export(args):
    lock = load_lock(args.lock)
    manifest = load_release_manifest(lock)
    check_lock(manifest, lock)
    output = args.output.resolve()
    temporary = output.with_name(output.name + ".partial")
    checksum_file = output.with_name(output.name + ".sha256")
    if output.exists() or temporary.exists() or checksum_file.exists():
        raise ValueError("Output or partial output already exists; preserve it or choose a different path")
    output.parent.mkdir(parents=True, exist_ok=True)
    compression = zipfile.ZIP_STORED if args.compression == "stored" else zipfile.ZIP_DEFLATED
    try:
        with zipfile.ZipFile(temporary, "w", compression=compression, compresslevel=1 if compression else None) as archive:
            archive.writestr(MANIFEST_NAME, json.dumps(manifest, indent=2) + "\n")
            checked_parents = set()
            for index, record in enumerate(manifest["files"]):
                with local(record["path"], True, checked_parents).open("rb") as source, archive.open(record["path"], "w", force_zip64=True) as destination:
                    size, checksum = hash_stream(source, destination)
                if size != record["size"] or checksum != record["sha256"]:
                    raise ValueError(f"Asset changed since snapshot: {record['path']}")
                if index and index % 10000 == 0:
                    print(f"Packed {index}/{len(manifest['files'])} files.", flush=True)
        os.replace(temporary, output)
    finally:
        temporary.unlink(missing_ok=True)
    with output.open("rb") as stream:
        checksum = hash_stream(stream)[1]
    checksum_file.write_text(f"{checksum}  {output.name}\n", encoding="utf-8", newline="\n")
    print(f"Exported {lock['name']} {lock['version']}: {output}\nZIP SHA-256: {checksum}")


def installed(lock):
    path = local((STATE / (lock["name"] + ".json")).as_posix())
    if not path.exists():
        return None
    receipt = read_json(path)
    validate_manifest(receipt["manifest"])
    if receipt["name"] != lock["name"]:
        raise ValueError("Installed receipt has the wrong pack name")
    return receipt


def import_release(args):
    lock = load_lock(args.lock)
    dependency_files = dependencies(lock, verify=True)
    old = installed(lock)
    previous = {r["path"]: r for r in old["manifest"]["files"]} if old else {}
    old_id = old["manifest"]["packId"] if old else None
    staging_root = local("artifacts/asset-import")
    staging_root.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.zip) as archive, tempfile.TemporaryDirectory(dir=staging_root) as staging:
        infos = archive.infolist()
        names = [info.filename for info in infos]
        if len(names) != len({name.casefold() for name in names}) or MANIFEST_NAME not in names:
            raise ValueError("Duplicate ZIP entries or missing manifest")
        if archive.getinfo(MANIFEST_NAME).file_size > MAX_MANIFEST_BYTES:
            raise ValueError("Oversized pack manifest")
        manifest_file = Path(staging) / MANIFEST_NAME
        manifest_file.write_bytes(archive.read(MANIFEST_NAME))
        manifest = read_json(manifest_file)
        check_lock(manifest, lock)
        expected = {r["path"]: r for r in manifest["files"]}
        if set(names) != set(expected) | {MANIFEST_NAME}:
            raise ValueError("ZIP must contain exactly the pinned file list")
        if args.rollback:
            if not old or version(lock["version"]) >= version(old["version"]):
                raise ValueError("Rollback requires an installed newer version")
        elif old and version(lock["version"]) < version(old["version"]):
            raise ValueError("Restoring an older release needs --rollback")
        elif old and lock["version"] == old["version"] and old_id != manifest["packId"]:
            raise ValueError("A published version cannot change its asset identity")
        expected_case = {name.casefold(): name for name in expected}
        for name in previous:
            if name.casefold() in expected_case and name != expected_case[name.casefold()]:
                raise ValueError("Case-only asset renames need an intermediate name in a separate release")
        writes = []
        approved = {}
        checked_parents = set()
        for index, record in enumerate(manifest["files"]):
            name = record["path"]
            target = local(name, True, checked_parents)
            if name in dependency_files and record != dependency_files[name]:
                raise ValueError(f"Pack changes dependency-owned asset: {name}")
            unchanged = matches(target, record)
            if target.exists() and not unchanged and (name not in previous or not matches(target, previous[name])):
                raise ValueError(f"Local file differs; preserve or rename it before import: {name}")
            info = archive.getinfo(name)
            if info.file_size != record["size"]:
                raise ValueError(f"Unexpected ZIP file size: {name}")
            staged = Path(staging) / "payload" / name
            with archive.open(info) as source:
                if unchanged:
                    size, checksum = hash_stream(source)
                else:
                    staged.parent.mkdir(parents=True, exist_ok=True)
                    with staged.open("wb") as destination:
                        size, checksum = hash_stream(source, destination)
            if size != record["size"] or checksum != record["sha256"]:
                raise ValueError(f"Corrupt or modified ZIP asset: {name}")
            if not unchanged:
                writes.append((name, staged))
                approved[name] = previous[name] if target.exists() else None
            if index and index % 10000 == 0:
                print(f"Verified {index}/{len(manifest['files'])} ZIP assets.", flush=True)
        removals = []
        for name, record in previous.items():
            if name in expected or name in dependency_files:
                continue
            target = local(name, True)
            if target.exists():
                if not matches(target, record):
                    raise ValueError(f"Locally edited obsolete asset; refusing removal: {name}")
                removals.append(name)
                approved[name] = record
        # Nothing changes until all ZIP bytes and every conflicting local file
        # have been checked. Keep replaced/deleted bytes and undo partial writes.
        backup = local("artifacts/asset-backups/" + lock["name"] + "/" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:8])
        saved = []
        mutations = []
        try:
            for name in [name for name, _ in writes] + removals:
                target = local(name, True)
                allowed = approved[name]
                if (allowed is None and target.exists()) or (allowed is not None and not matches(target, allowed)):
                    raise ValueError(f"Local asset changed while verifying the ZIP: {name}")
                if target.exists():
                    copy = backup / name
                    copy.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(target, copy)
                    if not matches(copy, allowed):
                        raise ValueError(f"Local asset changed during backup: {name}")
                    saved.append(name)
            if saved:
                save_json(backup / "upgrade.json", {"from": old, "to": lock, "files": saved})
            for name, staged in writes:
                target = local(name, True)
                target.parent.mkdir(parents=True, exist_ok=True)
                os.replace(staged, target)
                mutations.append(name)
            for name in removals:
                local(name, True).unlink()
                mutations.append(name)
            save_json(local(lock["manifestPath"]), manifest)
            save_json(local((STATE / (lock["name"] + ".json")).as_posix()),
                      {"name": lock["name"], "version": lock["version"], "manifest": manifest})
        except Exception:
            for name in reversed(mutations):
                target = local(name, True)
                if name in saved:
                    shutil.copy2(backup / name, target)
                else:
                    target.unlink(missing_ok=True)
            raise
    print(f"Verified {lock['name']} {lock['version']}: restored/updated {len(writes)}, removed {len(removals)}; GUIDs preserved.")
    if saved:
        print(f"Previous asset bytes preserved at {backup}")


def status(args):
    lock = load_lock(args.lock)
    receipt = installed(lock)
    print(f"Required: {lock['name']} {lock['version']} ({lock['packId'][:12]})")
    if receipt:
        print(f"Installed: {receipt['version']} ({receipt['manifest']['packId'][:12]})")
    path = local(lock["manifestPath"])
    if not path.exists():
        print("Matching manifest is absent; import the pinned ZIP first.")
        return False
    manifest = read_json(path)
    check_lock(manifest, lock)
    dependencies(lock, verify=True)
    missing, modified = [], []
    checked_parents = set()
    for record in manifest["files"]:
        target = local(record["path"], True, checked_parents)
        if not target.exists():
            missing.append(record["path"])
        elif not matches(target, record):
            modified.append(record["path"])
    print(f"Checked {len(manifest['files'])} files: missing={len(missing)}, modified={len(modified)}.")
    for name in (missing + modified)[:20]:
        print(name)
    return not missing and not modified


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="action", required=True)
    p = sub.add_parser("pin")
    p.add_argument("--manifest", type=Path, required=True)
    p.add_argument("--name", required=True)
    p.add_argument("--version", required=True)
    p.add_argument("--lock", type=Path, required=True)
    p = sub.add_parser("snapshot")
    p.add_argument("--profile", type=Path, required=True)
    p.add_argument("--version", required=True)
    p.add_argument("--lock", type=Path, required=True)
    for action in ("export", "import", "status", "verify"):
        p = sub.add_parser(action)
        p.add_argument("--lock", type=Path, required=True)
        if action == "export":
            p.add_argument("--output", type=Path, required=True)
            p.add_argument("--compression", choices=("fast", "stored"), default="fast")
        if action == "import":
            p.add_argument("zip", type=Path)
            p.add_argument("--rollback", action="store_true", help="Restore an older pinned ZIP, preserving current local edits")
    args = parser.parse_args()
    try:
        if args.action == "pin": pin(args)
        elif args.action == "snapshot": snapshot(args)
        elif args.action == "export": export(args)
        elif args.action == "import": import_release(args)
        elif not status(args): parser.exit(1, "Asset release is missing or locally modified.\n")
    except (ValueError, OSError, KeyError, TypeError, zipfile.BadZipFile) as error:
        parser.exit(1, f"Asset release failed: {error}\n")


if __name__ == "__main__":
    main()

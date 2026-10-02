"""Export or restore the separately shared artwork, preserving Unity GUIDs."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "docs/asset-pack.json"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def load_manifest():
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if manifest["schemaVersion"] != 1:
        raise ValueError("Unsupported asset pack version")
    seen = set()
    for record in manifest["files"]:
        path = record["path"]
        parts = PurePosixPath(path)
        allowed = path.startswith(("source-art/", "unity/Assets/Alabama/Art/")) or path == "unity/Assets/Alabama/Art.meta"
        if not allowed or parts.is_absolute() or ".." in parts.parts or "\\" in path or ":" in path:
            raise ValueError(f"Invalid asset path: {path}")
        if path.casefold() in seen:
            raise ValueError(f"Duplicate asset path: {path}")
        seen.add(path.casefold())
    identity = digest(json.dumps(manifest["files"], sort_keys=True, separators=(",", ":")).encode())
    if identity != manifest["packId"]:
        raise ValueError("Asset manifest identity does not match its file list")
    return manifest


def checked_bytes(path, record):
    data = path.read_bytes()
    if len(data) != record["size"] or digest(data) != record["sha256"]:
        raise ValueError(f"Asset does not match the reviewed pack: {record['path']}")
    return data


def export_pack(output):
    manifest = load_manifest()
    output = output.resolve()
    if output.exists():
        raise ValueError(f"Output already exists: {output}")
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".partial")
    try:
        with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("asset-pack-manifest.json", json.dumps(manifest, indent=2) + "\n")
            for record in manifest["files"]:
                archive.writestr(record["path"], checked_bytes(ROOT / record["path"], record))
        os.replace(temporary, output)
    finally:
        if temporary.exists():
            temporary.unlink()
    print(f"Exported {len(manifest['files'])} files: {output}")
    print(f"ZIP SHA-256: {digest(output.read_bytes())}")


def import_pack(source):
    manifest = load_manifest()
    expected = {record["path"]: record for record in manifest["files"]}
    with zipfile.ZipFile(source) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or set(names) != set(expected) | {"asset-pack-manifest.json"}:
            raise ValueError("ZIP must contain exactly the expected pack files, with no duplicates")
        info = archive.getinfo("asset-pack-manifest.json")
        if info.file_size > 1024 * 1024:
            raise ValueError("Oversized pack manifest")
        if json.loads(archive.read(info)) != manifest:
            raise ValueError("This ZIP belongs to a different project asset-pack version")
        for name, record in expected.items():
            if archive.getinfo(name).file_size != record["size"]:
                raise ValueError(f"Unexpected file size: {name}")
            target = ROOT / name
            if target.is_symlink() or any(parent.is_symlink() for parent in target.parents):
                raise ValueError(f"Refusing symbolic-link target: {name}")
            if target.exists() and (not target.is_file() or digest(target.read_bytes()) != record["sha256"]):
                raise ValueError(f"Local file differs; preserve or rename it before import: {name}")
        staging_root = ROOT / "artifacts/asset-import"
        staging_root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=staging_root) as staging:
            pending = []
            for name, record in expected.items():
                data = archive.read(name)
                if digest(data) != record["sha256"]:
                    raise ValueError(f"Corrupt or modified ZIP asset: {name}")
                target = ROOT / name
                if not target.exists():
                    staged = Path(staging) / name
                    staged.parent.mkdir(parents=True, exist_ok=True)
                    staged.write_bytes(data)
                    pending.append((staged, target))
            for staged, target in pending:
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(staged), str(target))
    print(f"Verified {len(expected)} files; restored {len(pending)} missing files. Unity GUIDs preserved.")


def main():
    # Preserve the original reviewed ZIP and commands. Named releases use the
    # same entry point with an explicit lock, or one of these lifecycle commands.
    if "--lock" in sys.argv[1:] or (len(sys.argv) > 1 and sys.argv[1] in ("pin", "snapshot", "status", "verify")):
        from asset_versions import main as versioned_main
        versioned_main()
        return
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="action", required=True)
    sub.add_parser("export").add_argument("--output", type=Path, required=True)
    sub.add_parser("import").add_argument("zip", type=Path)
    args = parser.parse_args()
    try:
        if args.action == "export":
            export_pack(args.output)
        else:
            import_pack(args.zip)
    except (ValueError, OSError, KeyError, zipfile.BadZipFile) as error:
        parser.exit(1, f"Asset pack failed: {error}\n")


if __name__ == "__main__":
    main()

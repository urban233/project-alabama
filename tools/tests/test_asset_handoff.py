"""Project-root private ZIP handoffs, using small independent workspaces."""
import argparse
from contextlib import redirect_stdout
import io
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import asset_handoff as handoff
import asset_versions as assets


class AssetHandoffTests(unittest.TestCase):
    def setUp(self):
        (ROOT / "artifacts").mkdir(exist_ok=True)
        temporary = tempfile.TemporaryDirectory(dir=ROOT / "artifacts")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        original = assets.ROOT
        assets.ROOT = self.root
        self.addCleanup(setattr, assets, "ROOT", original)
        output = redirect_stdout(io.StringIO())
        output.__enter__()
        self.addCleanup(output.__exit__, None, None, None)
        self.base_path = self.root / "source-art/base.txt"
        self.base_path.parent.mkdir()
        self.base_path.write_bytes(b"base")
        record = {"path": "source-art/base.txt", "size": 4, "sha256": assets.hashlib.sha256(b"base").hexdigest()}
        manifest = {"schemaVersion": 1, "files": [record], "packId": assets.identity([record])}
        manifest_path = self.root / "docs/base.json"
        assets.save_json(manifest_path, manifest)
        self.base_lock = self.root / "docs/asset-packs/base.lock.json"
        assets.pin(argparse.Namespace(manifest=manifest_path, name="base", version="1.0.0", lock=self.base_lock))
        self.base_zip = self.root / "project-alabama-assets.zip"
        assets.export(argparse.Namespace(lock=self.base_lock, output=self.base_zip, compression="fast"))
        self.map_path = self.root / "source-art/map/a.blend"
        self.map_path.parent.mkdir()
        self.map_path.write_bytes(b"map v1")
        # Sort before base, so discovery must actually order dependencies.
        self.profile = self.root / "docs/asset-packs/aaa-map.profile.json"
        assets.save_json(self.profile, {"schemaVersion": 1, "name": "aaa-map", "include": ["source-art/map"],
                                      "dependencies": ["docs/asset-packs/base.lock.json"]})
        self.map_lock = self.root / "docs/asset-packs/aaa-map.lock.json"
        assets.snapshot(argparse.Namespace(profile=self.profile, version="0.1.0", lock=self.map_lock))
        self.map_zip = self.root / assets.load_lock(self.map_lock)["archiveName"]
        assets.export(argparse.Namespace(lock=self.map_lock, output=self.map_zip, compression="fast"))

    def test_receive_root_zips_dependency_first_with_legacy_base_name(self):
        self.base_path.unlink()
        self.map_path.unlink()
        assets.local(assets.load_lock(self.map_lock)["manifestPath"]).unlink()
        self.assertEqual([lock["name"] for _, lock in handoff.locks()], ["base", "aaa-map"])
        handoff.receive()
        self.assertEqual(self.base_path.read_bytes(), b"base")
        self.assertEqual(self.map_path.read_bytes(), b"map v1")
        self.assertEqual(assets.installed(assets.load_lock(self.map_lock))["version"], "0.1.0")
        handoff.receive()

    def test_missing_root_zip_is_reported_before_any_import(self):
        self.map_zip.unlink()
        self.base_path.unlink()
        with self.assertRaisesRegex(ValueError, "project root"), patch.object(assets, "import_release") as install:
            handoff.receive()
        install.assert_not_called()
        self.assertFalse(self.base_path.exists())
        self.assertFalse(handoff.status())

    def test_corrupt_map_keeps_map_bytes_and_reports_failure(self):
        self.map_zip.unlink()
        with zipfile.ZipFile(self.map_zip, "w") as archive:
            archive.writestr(assets.MANIFEST_NAME, assets.local(assets.load_lock(self.map_lock)["manifestPath"]).read_text())
            archive.writestr("source-art/map/a.blend", b"bad v1")
        self.map_path.unlink()
        with self.assertRaisesRegex(ValueError, "Corrupt or modified"):
            handoff.receive()
        self.assertFalse(self.map_path.exists())

    def test_existing_local_work_is_preserved(self):
        self.map_path.write_bytes(b"my local map")
        with self.assertRaisesRegex(ValueError, "Local file differs"):
            handoff.receive()
        self.assertEqual(self.map_path.read_bytes(), b"my local map")

    def test_publisher_export_is_root_only_and_registers_receipt(self):
        with self.assertRaisesRegex(ValueError, "one publisher"):
            handoff.export("aaa-map")
        handoff.configure(["aaa-map", "aaa-map"])
        self.assertEqual(handoff.publishers(), ["aaa-map"])
        self.map_zip.unlink()
        self.map_zip.with_name(self.map_zip.name + ".sha256").unlink()
        handoff.export("aaa-map")
        self.assertTrue(self.map_zip.exists())
        self.assertTrue(self.map_zip.with_name(self.map_zip.name + ".sha256").exists())
        self.assertEqual(assets.installed(assets.load_lock(self.map_lock))["version"], "0.1.0")
        with self.assertRaisesRegex(ValueError, "already exists"):
            handoff.export("aaa-map")

    def test_bad_archive_name_cannot_export_outside_root(self):
        lock = assets.read_json(self.map_lock)
        lock["archiveName"] = "other/export.zip"
        assets.save_json(self.map_lock, lock)
        with self.assertRaisesRegex(ValueError, "filename"):
            handoff.locks()

    def test_dependency_pin_mismatch_is_rejected(self):
        lock = assets.read_json(self.map_lock)
        lock["dependencies"][0]["version"] = "1.0.1"
        assets.save_json(self.map_lock, lock)
        with self.assertRaisesRegex(ValueError, "pin changed"):
            handoff.locks()

    def test_duplicate_packs_are_rejected(self):
        shutil.copyfile(self.map_lock, self.map_lock.with_name("duplicate.lock.json"))
        with self.assertRaisesRegex(ValueError, "Duplicate pack"):
            handoff.locks()

    def test_contribution_preserves_release_and_receipt_and_captures_new_work(self):
        handoff.receive()
        lock_bytes = self.map_lock.read_bytes()
        receipt_path = assets.local("artifacts/asset-packs/installed/aaa-map.json")
        receipt_bytes = receipt_path.read_bytes()
        self.map_path.write_bytes(b"new local model")
        (self.map_path.parent / "b.blend").write_bytes(b"new source")
        handoff.contribute("aaa-map", "developer-b")
        candidates = list(self.root.glob("project-alabama-work-aaa-map-developer-b-*.zip"))
        self.assertEqual(len(candidates), 1)
        with zipfile.ZipFile(candidates[0]) as archive:
            self.assertEqual(archive.read("source-art/map/a.blend"), b"new local model")
            self.assertEqual(archive.read("source-art/map/b.blend"), b"new source")
            import json
            manifest = json.loads(archive.read(assets.MANIFEST_NAME))
            self.assertEqual(manifest["contribution"]["packId"], assets.load_lock(self.map_lock)["packId"])
            self.assertEqual(manifest["contribution"]["name"], "aaa-map")
        self.assertEqual(self.map_lock.read_bytes(), lock_bytes)
        self.assertEqual(receipt_path.read_bytes(), receipt_bytes)
        self.assertTrue(handoff.status())  # Presence is not a claim about local edits.

    def test_contribution_records_installed_baseline_when_code_lock_advanced(self):
        handoff.receive()
        original = assets.load_lock(self.map_lock)
        self.map_path.write_bytes(b"new local model")
        assets.snapshot(argparse.Namespace(profile=self.profile, version="0.2.0", lock=self.map_lock))
        handoff.contribute("aaa-map", "developer-b")
        candidate = next(self.root.glob("project-alabama-work-aaa-map-developer-b-*.zip"))
        with zipfile.ZipFile(candidate) as archive:
            import json
            manifest = json.loads(archive.read(assets.MANIFEST_NAME))
            self.assertEqual(manifest["contribution"]["version"], "0.1.0")
            self.assertEqual(manifest["contribution"]["packId"], original["packId"])


if __name__ == "__main__":
    unittest.main()

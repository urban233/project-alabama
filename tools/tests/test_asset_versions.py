"""Named releases, safe upgrades and rollback using small synthetic assets."""
import argparse
from contextlib import redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("asset_versions", ROOT / "tools/asset_versions.py")
assets = importlib.util.module_from_spec(spec)
spec.loader.exec_module(assets)


class AssetVersionTests(unittest.TestCase):
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
        self.source = "source-art/maps/test/a.blend"
        self.model = "unity/Assets/Alabama/Art/Test/a.fbx"
        self.initial = {self.source: b"source version one", self.model: b"model version one",
                        self.model + ".meta": b"fileFormatVersion: 2\nguid: " + b"1" * 32 + b"\n",
                        "unity/Assets/Alabama/Art/Test.meta": b"fileFormatVersion: 2\nguid: " + b"2" * 32 + b"\n",
                        "unity/Assets/Alabama/Art.meta": b"fileFormatVersion: 2\nguid: " + b"3" * 32 + b"\n"}
        self.restore_initial()
        self.profile = self.root / "docs/profile.json"
        assets.save_json(self.profile, {"schemaVersion": 1, "name": "test-map",
                                      "include": ["source-art/maps/test", "unity/Assets/Alabama/Art/Test"]})
        self.lock = self.root / "docs/test.lock.json"
        assets.snapshot(argparse.Namespace(profile=self.profile, version="0.1.0", lock=self.lock))
        self.lock1 = self.root / "docs/v1.lock.json"
        shutil.copy2(self.lock, self.lock1)
        self.zip1 = self.root / "v1.zip"
        self.export(self.zip1)
        self.install(self.zip1)

    def write(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def restore_initial(self):
        for name, data in self.initial.items():
            self.write(name, data)

    def export(self, output):
        assets.export(argparse.Namespace(lock=self.lock, output=output, compression="fast"))

    def install(self, archive, lock=None, rollback=False):
        assets.import_release(argparse.Namespace(lock=lock or self.lock, zip=archive, rollback=rollback))

    def upgrade(self, remove=False):
        self.write(self.source, b"source version two")
        self.write("source-art/maps/test/b.blend", b"new asset")
        if remove:
            (self.root / self.model).unlink()
            (self.root / (self.model + ".meta")).unlink()
        assets.snapshot(argparse.Namespace(profile=self.profile, version="0.2.0", lock=self.lock))
        archive = self.root / "v2.zip"
        self.export(archive)
        (self.root / "source-art/maps/test/b.blend").unlink()
        self.restore_initial()
        return archive

    def test_clean_restore_and_repeat_preserve_every_byte_and_guid(self):
        for name in self.initial:
            (self.root / name).unlink()
        self.install(self.zip1)
        self.install(self.zip1)
        for name, data in self.initial.items():
            self.assertEqual((self.root / name).read_bytes(), data)
        self.assertTrue(assets.status(argparse.Namespace(lock=self.lock)))

    def test_upgrade_backs_up_replaced_and_obsolete_assets(self):
        archive = self.upgrade(remove=True)
        self.install(archive)
        self.assertEqual((self.root / self.source).read_bytes(), b"source version two")
        self.assertFalse((self.root / self.model).exists())
        self.assertFalse((self.root / (self.model + ".meta")).exists())
        backups = list((self.root / "artifacts/asset-backups").rglob("a.blend"))
        self.assertEqual(len(backups), 1)
        self.assertEqual(backups[0].read_bytes(), self.initial[self.source])
        self.assertEqual(assets.installed(assets.load_lock(self.lock))["version"], "0.2.0")

    def test_locally_edited_upgrade_file_blocks_all_writes(self):
        archive = self.upgrade()
        self.write(self.source, b"developer work")
        with self.assertRaisesRegex(ValueError, "Local file differs"):
            self.install(archive)
        self.assertEqual((self.root / self.source).read_bytes(), b"developer work")
        self.assertFalse((self.root / "source-art/maps/test/b.blend").exists())

    def test_locally_edited_obsolete_file_blocks_removal(self):
        archive = self.upgrade(remove=True)
        self.write(self.model, b"developer model")
        with self.assertRaisesRegex(ValueError, "Locally edited obsolete"):
            self.install(archive)
        self.assertEqual((self.root / self.source).read_bytes(), self.initial[self.source])
        self.assertEqual((self.root / self.model).read_bytes(), b"developer model")

    def test_rollback_restores_old_release_without_regenerating_guids(self):
        archive = self.upgrade(remove=True)
        self.install(archive)
        self.install(self.zip1, lock=self.lock1, rollback=True)
        for name, data in self.initial.items():
            self.assertEqual((self.root / name).read_bytes(), data)
        self.assertFalse((self.root / "source-art/maps/test/b.blend").exists())

    def test_rollback_rejects_local_edits(self):
        self.install(self.upgrade())
        self.write(self.source, b"new local work")
        with self.assertRaisesRegex(ValueError, "Local file differs"):
            self.install(self.zip1, lock=self.lock1, rollback=True)
        self.assertEqual((self.root / self.source).read_bytes(), b"new local work")

    def test_full_snapshot_can_skip_an_intermediate_upgrade(self):
        self.upgrade()
        self.write(self.source, b"source version three")
        assets.snapshot(argparse.Namespace(profile=self.profile, version="0.3.0", lock=self.lock))
        archive = self.root / "v3.zip"
        self.export(archive)
        self.restore_initial()
        self.install(archive)
        self.assertEqual((self.root / self.source).read_bytes(), b"source version three")
        self.assertEqual(assets.installed(assets.load_lock(self.lock))["version"], "0.3.0")

    def test_downgrade_requires_explicit_rollback(self):
        self.install(self.upgrade())
        with self.assertRaisesRegex(ValueError, "needs --rollback"):
            self.install(self.zip1, lock=self.lock1)

    def test_directory_reparse_target_is_rejected(self):
        actual = assets.reparse_path
        redirected = self.root / "source-art/maps"
        def simulated(path):
            return path == redirected or actual(path)
        with patch.object(assets, "reparse_path", side_effect=simulated):
            with self.assertRaisesRegex(ValueError, "symbolic-link"):
                self.install(self.zip1)

    def test_failed_write_rolls_back_earlier_mutations_and_keeps_receipt(self):
        archive = self.upgrade()
        replace = assets.os.replace
        def fail_second(source, destination):
            if "payload" in Path(source).parts and Path(destination).name == "b.blend":
                raise OSError("synthetic write failure")
            return replace(source, destination)
        with patch.object(assets.os, "replace", side_effect=fail_second):
            with self.assertRaisesRegex(OSError, "synthetic"):
                self.install(archive)
        self.assertEqual((self.root / self.source).read_bytes(), self.initial[self.source])
        self.assertFalse((self.root / "source-art/maps/test/b.blend").exists())
        self.assertEqual(assets.installed(assets.load_lock(self.lock))["version"], "0.1.0")

    def test_edit_during_archive_verification_is_not_overwritten(self):
        archive = self.upgrade()
        stream_hash = assets.hash_stream
        def edit_after_zip_read(stream, destination=None):
            result = stream_hash(stream, destination)
            if isinstance(stream, zipfile.ZipExtFile) and stream.name == self.model + ".meta":
                self.write(self.source, b"work saved while import was verifying")
            return result
        with patch.object(assets, "hash_stream", side_effect=edit_after_zip_read):
            with self.assertRaisesRegex(ValueError, "changed while verifying"):
                self.install(archive)
        self.assertEqual((self.root / self.source).read_bytes(), b"work saved while import was verifying")
        self.assertFalse((self.root / "source-art/maps/test/b.blend").exists())

    def test_corrupt_zip_is_rejected_before_any_update(self):
        original = self.upgrade()
        corrupt = self.root / "corrupt.zip"
        with zipfile.ZipFile(original) as source, zipfile.ZipFile(corrupt, "w") as destination:
            for info in source.infolist():
                data = source.read(info)
                destination.writestr(info.filename, b"bad asset" if info.filename.endswith("b.blend") else data)
        with self.assertRaisesRegex(ValueError, "Corrupt or modified"):
            self.install(corrupt)
        self.assertEqual((self.root / self.source).read_bytes(), self.initial[self.source])

    def test_wrong_lock_and_unexpected_zip_entries_are_rejected(self):
        archive = self.upgrade()
        with self.assertRaisesRegex(ValueError, "different pinned release"):
            self.install(archive, lock=self.lock1)
        with zipfile.ZipFile(archive, "a") as destination:
            destination.writestr("../outside", b"bad")
        with self.assertRaisesRegex(ValueError, "exactly the pinned"):
            self.install(archive)
        self.assertFalse((self.root.parent / "outside").exists())

    def test_snapshot_version_is_immutable_and_export_rejects_changes(self):
        with self.assertRaisesRegex(ValueError, "strictly newer"):
            assets.snapshot(argparse.Namespace(profile=self.profile, version="0.1.0", lock=self.lock))
        self.write(self.source, b"changed after snapshot")
        output = self.root / "modified.zip"
        with self.assertRaisesRegex(ValueError, "changed since snapshot"):
            self.export(output)
        self.assertFalse(output.exists())
        self.assertFalse(output.with_name(output.name + ".partial").exists())

    def test_missing_sidecars_and_duplicate_guids_block_snapshot(self):
        sidecar = self.root / (self.model + ".meta")
        sidecar.unlink()
        with self.assertRaisesRegex(ValueError, "Missing Unity metadata"):
            assets.snapshot(argparse.Namespace(profile=self.profile, version="0.2.0", lock=self.lock))
        sidecar.write_bytes(self.initial["unity/Assets/Alabama/Art/Test.meta"])
        with self.assertRaisesRegex(ValueError, "Duplicate Unity GUID"):
            assets.snapshot(argparse.Namespace(profile=self.profile, version="0.2.0", lock=self.lock))

    def test_dependency_requires_the_pinned_base_without_changing_it(self):
        base_file = {"path": "source-art/base.txt", "size": 4, "sha256": assets.hashlib.sha256(b"base").hexdigest()}
        self.write(base_file["path"], b"base")
        base_manifest = {"schemaVersion": 1, "packId": assets.identity([base_file]), "files": [base_file]}
        base_path = self.root / "docs/base.json"
        assets.save_json(base_path, base_manifest)
        base_lock = self.root / "docs/base.lock.json"
        assets.pin(argparse.Namespace(manifest=base_path, name="base", version="1.0.0", lock=base_lock))
        profile = assets.read_json(self.profile)
        profile["dependencies"] = ["docs/base.lock.json"]
        assets.save_json(self.profile, profile)
        assets.snapshot(argparse.Namespace(profile=self.profile, version="0.2.0", lock=self.lock))
        archive = self.root / "dependent.zip"
        self.export(archive)
        (self.root / base_file["path"]).unlink()
        with self.assertRaisesRegex(ValueError, "Restore the pinned base"):
            self.install(archive)

    def test_legacy_manifest_can_be_pinned_without_changing_zip_contents(self):
        files = assets.read_json(assets.local(assets.load_lock(self.lock)["manifestPath"]))["files"]
        legacy = {"schemaVersion": 1, "packId": assets.identity(files), "files": files}
        path = self.root / "docs/legacy.json"
        assets.save_json(path, legacy)
        locked = self.root / "docs/legacy.lock.json"
        assets.pin(argparse.Namespace(manifest=path, name="legacy", version="1.0.0", lock=locked))
        archive = self.root / "legacy.zip"
        with zipfile.ZipFile(archive, "w") as destination:
            destination.writestr(assets.MANIFEST_NAME, json.dumps(legacy))
            for name, data in self.initial.items():
                destination.writestr(name, data)
        self.install(archive, lock=locked)
        self.assertEqual(assets.read_json(path), legacy)

    def test_invalid_and_case_alias_paths_are_rejected(self):
        for path in ("../escape", "source-art/../outside", "source-art/CON.blend", "source-art/a:stream", "source-art/a\\b", "source-art/./a"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                assets.safe_relative(path, True)
        manifest = assets.read_json(assets.local(assets.load_lock(self.lock)["manifestPath"]))
        manifest["files"].append(dict(manifest["files"][0], path=manifest["files"][0]["path"].replace("a.blend", "A.blend")))
        with self.assertRaisesRegex(ValueError, "Duplicate asset path"):
            assets.validate_manifest(manifest)


if __name__ == "__main__":
    unittest.main()

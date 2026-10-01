"""Exercise the ZIP trust boundary without requiring Unity or third-party art."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]


class AssetPackTests(unittest.TestCase):
    def setUp(self):
        (ROOT / "artifacts").mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=ROOT / "artifacts")
        self.root = Path(self.temp.name)
        (self.root / "tools").mkdir()
        (self.root / "docs").mkdir()
        shutil.copy2(ROOT / "tools/asset_pack.py", self.root / "tools/asset_pack.py")
        self.content = {"source-art/example.blend": b"reviewed source", "unity/Assets/Alabama/Art.meta": b"stable guid"}
        files = [{"path": name, "size": len(data), "sha256": hashlib.sha256(data).hexdigest()}
                 for name, data in sorted(self.content.items())]
        identity = hashlib.sha256(json.dumps(files, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
        self.manifest = {"schemaVersion": 1, "packId": identity, "files": files}
        (self.root / "docs/asset-pack.json").write_bytes(json.dumps(self.manifest).encode())
        self.zip = self.root / "pack.zip"

    def tearDown(self):
        self.temp.cleanup()

    def make_zip(self, content=None, manifest=None):
        with zipfile.ZipFile(self.zip, "w") as archive:
            archive.writestr("asset-pack-manifest.json", json.dumps(manifest or self.manifest))
            for name, data in (content or self.content).items():
                archive.writestr(name, data)

    def run_import(self):
        return subprocess.run([sys.executable, str(self.root / "tools/asset_pack.py"), "import", str(self.zip)],
                              capture_output=True, text=True)

    def test_restore_preserves_bytes_and_is_idempotent(self):
        self.make_zip()
        self.assertEqual(self.run_import().returncode, 0)
        for name, data in self.content.items():
            self.assertEqual((self.root / name).read_bytes(), data)
        repeated = self.run_import()
        self.assertEqual(repeated.returncode, 0)
        self.assertIn("restored 0", repeated.stdout)

    def test_tampered_bytes_fail_before_restoring_any_files(self):
        altered = dict(self.content)
        altered["source-art/example.blend"] = b"corrupt contents"
        self.make_zip(altered)
        self.assertNotEqual(self.run_import().returncode, 0)
        self.assertFalse((self.root / "source-art/example.blend").exists())
        self.assertFalse((self.root / "unity/Assets/Alabama/Art.meta").exists())

    def test_local_changes_are_preserved(self):
        self.make_zip()
        target = self.root / "source-art/example.blend"
        target.parent.mkdir()
        target.write_bytes(b"colleague edits")
        self.assertNotEqual(self.run_import().returncode, 0)
        self.assertEqual(target.read_bytes(), b"colleague edits")

    def test_unexpected_traversal_entry_is_rejected(self):
        altered = dict(self.content)
        altered["../outside"] = b"unwanted"
        self.make_zip(altered)
        self.assertNotEqual(self.run_import().returncode, 0)
        self.assertFalse((self.root / "source-art/example.blend").exists())

    def test_mismatched_pack_version_is_rejected(self):
        manifest = dict(self.manifest, packId="different")
        self.make_zip(manifest=manifest)
        self.assertNotEqual(self.run_import().returncode, 0)


if __name__ == "__main__":
    unittest.main()

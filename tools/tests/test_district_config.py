"""District isolation and the shared frame are import safety requirements."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "nfs_world"))
from district_config import DEFAULT, SHARED_ORIGIN, load


class DistrictConfigTests(unittest.TestCase):
    def test_rosewood_retains_downtown_frame_and_isolates_outputs(self):
        root = Path(__file__).resolve().parents[2]
        config = load(root, Path("docs/districts/rosewood.json"))
        self.assertEqual(config["sharedOrigin"], list(SHARED_ORIGIN))
        for key in ("outputRoot", "editableRoot", "rawTextureRoot", "artifactRoot"):
            self.assertNotEqual(config[key], DEFAULT[key])

    def test_rejects_recentring_and_writes_into_downtown(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            recipe = root / "recipe.json"
            config = DEFAULT.copy()
            config["outputRoot"] += "/Rosewood"
            config["sharedOrigin"] = [0, 0, 0]
            recipe.write_text(json.dumps(config))
            with self.assertRaisesRegex(ValueError, "origin"):
                load(root, Path("recipe.json"))
            config["sharedOrigin"] = list(SHARED_ORIGIN)
            config["outputRoot"] = DEFAULT["outputRoot"]
            recipe.write_text(json.dumps(config))
            with self.assertRaisesRegex(ValueError, "output root"):
                load(root, Path("recipe.json"))

    def test_rejects_path_escape(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config = DEFAULT.copy()
            config["outputRoot"] += "/Rosewood"
            config["editableRoot"] = "../outside"
            (root / "recipe.json").write_text(json.dumps(config))
            with self.assertRaisesRegex(ValueError, "Unsafe"):
                load(root, Path("recipe.json"))

"""Format fixtures verify hierarchy, mirror handling, and corrupt input rejection."""

import struct
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "nfs_world"))
from kn5 import InvalidKn5, Reader


def string(value):
    value = value.encode()
    return struct.pack("<i", len(value)) + value


def fixture(mirror=False, bad_index=False):
    result = b"sc6969" + struct.pack("<iii", 6, 0, 0)
    result += struct.pack("<i", 1) + string("Surface") + string("ksPerPixel")
    result += struct.pack("<BBiii", 0, 0, 0, 0, 0)
    matrix = np.eye(4, dtype=np.float32)
    matrix[0, 0] = -2 if mirror else 2
    matrix[3, :3] = (100, 5, -50)
    result += struct.pack("<i", 1) + string("Root") + struct.pack("<iB", 1, 1) + matrix.astype("<f4").tobytes()
    result += struct.pack("<i", 2) + string("Road") + struct.pack("<iBBBBi", 0, 1, 1, 1, 0, 3)
    vertices = np.zeros((3, 11), dtype="<f4")
    vertices[:, :3] = ((0, 0, 0), (1, 0, 0), (0, 0, 1))
    vertices[:, 4] = 1
    vertices[:, 6:8] = ((0, 0), (1, 0), (0, 1))
    result += vertices.tobytes()
    result += struct.pack("<i3Hi", 3, 0, 1, 5 if bad_index else 2, 0)
    result += struct.pack("<iff4fB", 0, 0, 500, 0, 0, 0, 1, 1)
    return result


class Kn5Tests(unittest.TestCase):
    def parse(self, data):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "fixture.kn5"
            path.write_bytes(data)
            with Reader(path) as reader:
                return list(reader.meshes())

    def test_hierarchy_transforms_geometry_and_preserves_uv(self):
        mesh, = self.parse(fixture())
        np.testing.assert_allclose(mesh["positions"], ((100, 5, -50), (102, 5, -50), (100, 5, -49)))
        np.testing.assert_allclose(mesh["normals"], ((0, 1, 0),) * 3)
        np.testing.assert_allclose(mesh["uv"], ((0, 0), (1, 0), (0, 1)))

    def test_mirror_reverses_winding(self):
        mesh, = self.parse(fixture(mirror=True))
        np.testing.assert_array_equal(mesh["triangles"], ((0, 2, 1),))
        self.assertEqual(mesh["positions"][1, 0], 98)

    def test_truncation_is_rejected(self):
        with self.assertRaises(InvalidKn5):
            self.parse(fixture()[:-4])

    def test_bad_indices_are_rejected(self):
        with self.assertRaises(InvalidKn5):
            self.parse(fixture(bad_index=True))

    def test_protected_or_foreign_format_is_rejected(self):
        with self.assertRaises(InvalidKn5):
            self.parse(b"protected content")

    def test_negative_count_is_rejected(self):
        with self.assertRaises(InvalidKn5):
            self.parse(b"sc6969" + struct.pack("<iii", 6, 0, -1))

    def test_trailing_data_is_rejected(self):
        with self.assertRaises(InvalidKn5):
            self.parse(fixture() + b"unexpected")


if __name__ == "__main__":
    unittest.main()

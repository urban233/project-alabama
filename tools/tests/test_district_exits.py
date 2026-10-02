import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'nfs_world'))
from audit_district_exits import surface_matches


class RoadSurfaceProbeTests(unittest.TestCase):
    def test_stacked_roads_do_not_match_the_wrong_elevation(self):
        road = np.array([[[0, 10, 0], [10, 10, 0], [0, 10, 10]]], dtype=float)
        probes = np.array([[2, 10, 2], [2, 0, 2], [9, 10, 9]])
        np.testing.assert_array_equal(surface_matches(probes, [road]), [True, False, False])

    def test_sloped_triangle_matches_its_surface_height(self):
        road = np.array([[[0, 0, 0], [10, 5, 0], [0, 0, 10]]], dtype=float)
        probes = np.array([[2, 1, 2], [2, 2, 2]])
        np.testing.assert_array_equal(surface_matches(probes, [road]), [True, False])


if __name__ == '__main__':
    unittest.main()

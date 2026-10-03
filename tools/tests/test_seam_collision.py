import sys
import unittest
from pathlib import Path
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"nfs_world"))
from seam_collision import area, subtract


class SeamCollisionTests(unittest.TestCase):
    def test_overlap_ownership_preserves_area_and_road_grade(self):
        # y=x/4: all new border points must retain the source road grade.
        source=np.array([[0,0,0],[4,1,0],[0,0,4]],dtype=float)
        owner=np.array([[0,0,0],[2,.5,0],[0,0,2]],dtype=float)
        retained,removed=subtract(source,owner)
        self.assertAlmostEqual(removed,2)
        self.assertAlmostEqual(sum(area(p) for p in retained)+removed,area(source))
        for polygon in retained:
            for point in polygon:
                self.assertAlmostEqual(point[1],point[0]/4)

    def test_shared_edge_removes_no_surface(self):
        source=np.array([[0,0,0],[4,0,0],[0,0,4]],dtype=float)
        owner=np.array([[0,0,0],[4,0,0],[4,0,-4]],dtype=float)
        retained,removed=subtract(source,owner)
        self.assertEqual(removed,0)
        self.assertAlmostEqual(sum(area(p) for p in retained),area(source))

    def test_complete_overlap_removes_only_neighbour_copy(self):
        source=np.array([[0,0,0],[4,0,0],[0,0,4]],dtype=float)
        retained,removed=subtract(source,source)
        self.assertEqual(retained,[])
        self.assertAlmostEqual(removed,8)

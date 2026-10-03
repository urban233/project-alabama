"""Regression checks for UV-edge, hue and alpha preservation in the material bake."""
import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'nfs_world'))
from art_direction_recipe import RECIPES, filter_albedo


class ArtDirectionRecipeTests(unittest.TestCase):
    def test_edges_and_moderate_surface_variation_survive(self):
        rng = np.random.default_rng(37)
        image = np.ones((64, 64, 4), dtype=np.float32)
        image[:, :32, :3] = .2
        image[:, 32:, :3] = .7
        image[..., :3] += rng.uniform(-.025, .025, (64, 64, 1))
        for family in RECIPES:
            with self.subTest(family=family):
                result = filter_albedo(image, family, False)
                self.assertLess(result[:, 31, :3].mean(), .26)
                self.assertGreater(result[:, 32, :3].mean(), .64)
                before = image[8:56, 8:24, :3].std()
                after = result[8:56, 8:24, :3].std()
                self.assertGreater(after, before * .1)
                self.assertLess(after, before * .9)
                self.assertLess(abs(float(result[..., :3].mean() - image[..., :3].mean())), .01)
                np.testing.assert_array_equal(result[..., 0], result[..., 1])
                np.testing.assert_array_equal(result[..., 1], result[..., 2])

    def test_invisible_colours_do_not_bleed_into_cutout(self):
        image = np.ones((32, 32, 4), dtype=np.float32)
        image[..., :3] = .4
        image[:, :16, :3] = [1, 0, 1]
        image[:, :16, 3] = 0
        for family in RECIPES:
            result = filter_albedo(image, family, True)
            np.testing.assert_array_equal(result[..., 3], image[..., 3])
            self.assertLess(np.max(np.abs(result[:, 16:, :3] - .4)), .002)

    def test_opaque_input_alpha_is_not_used_as_a_mask(self):
        image = np.full((8, 8, 4), .35, dtype=np.float32)
        image[..., 3] = 0
        result = filter_albedo(image, 'road', False)
        self.assertTrue(np.all(result[..., 3] == 1))
        self.assertTrue(np.isfinite(result).all())

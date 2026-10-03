"""Middle-detail albedo filtering; no UV changes, hue quantization or new lighting."""
import numpy as np

RECIPES = {
    'concrete': (.10, (1, 2, 4), .12, 96),
    'masonry': (.09, (1, 2), .18, 64),
    'road': (.08, (1, 2), .24, 96),
    'props': (.10, (1, 2, 4), .16, 64),
    'facade': (.13, (1, 2, 4, 8), .12, 80),
    'foliage': (.14, (1, 2, 4, 8), .16, 32),
    'ground': (.12, (1, 2, 4), .20, 64),
    'water': (.15, (2, 4, 8), .18, 80),
    'surface': (.11, (1, 2, 4), .18, 80),
}


def filter_albedo(rgba, family, alpha_clip):
    threshold, steps, residual, levels = RECIPES[family]
    source = np.asarray(rgba, dtype=np.float32)
    rgb = source[..., :3].copy()
    alpha = source[..., 3] if alpha_clip else np.ones(source.shape[:2], dtype=np.float32)
    # A fixed source guide prevents successive iterations from erasing a seam.
    guide = rgb.copy()
    for step in steps:
        total = rgb.copy()
        weights = np.ones(alpha.shape, dtype=np.float32)
        for axis, amount in ((0, step), (0, -step), (1, step), (1, -step)):
            distance = np.sum((np.roll(guide, amount, axis=axis) - guide) ** 2, axis=2)
            weight = np.exp(-distance / threshold ** 2) * np.roll(alpha, amount, axis=axis)
            total += np.roll(rgb, amount, axis=axis) * weight[..., None]
            weights += weight
        rgb = total / weights[..., None]
    # Retain faint original grain instead of turning broad planes into flat tiles.
    rgb = rgb * (1 - residual) + guide * residual
    luminance = np.sum(rgb * np.array([.2126, .7152, .0722], dtype=np.float32), axis=2)
    rgb += ((np.rint(luminance * levels) / levels - luminance) * .15)[..., None]
    result = source.copy()
    result[..., :3] = np.clip(rgb, 0, 1)
    result[..., 3] = source[..., 3] if alpha_clip else 1
    return result

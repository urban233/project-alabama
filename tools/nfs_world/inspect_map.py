"""Inventory the selected district and draw a source-space road overview."""

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from kn5 import Reader, file_hash
from district_config import load


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--config", type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    config = load(root, args.config)
    source = root / config["sourceRoot"]
    output = root / config["artifactRoot"]
    output.mkdir(parents=True, exist_ok=True)
    report = {"district": config["sourceDistrict"], "models": []}
    road_triangles = []
    for path in sorted(source.glob(f"*{config['sourceDistrict']}-*.kn5")):
        with Reader(path, root / config["rawTextureRoot"]) as reader:
            for mesh in reader.meshes():
                if path.stem.endswith("-Roads") and mesh["record"]["active"]:
                    road_triangles.append(mesh["positions"][mesh["triangles"]])
            model = dict(file=path.name, sha256=file_hash(path), bytes=path.stat().st_size,
                         version=reader.version, textures=reader.textures,
                         materials=reader.materials, nodes=reader.nodes)
            meshes = [n for n in reader.nodes if n["type"] == 2]
            model["triangles"] = sum(m["triangles"] for m in meshes)
            model["mesh_count"] = len(meshes)
            if meshes:
                model["bounds"] = [np.min([m["bounds"][0] for m in meshes], axis=0).tolist(),
                                   np.max([m["bounds"][1] for m in meshes], axis=0).tolist()]
            report["models"].append(model)
            print(json.dumps({k: model[k] for k in ("file", "triangles", "mesh_count")}), flush=True)
    if len(report["models"]) != 9:
        raise RuntimeError("Expected all nine district source models")
    (output / "inventory.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    all_roads = np.concatenate(road_triangles)
    low = all_roads.min(axis=(0, 1)); high = all_roads.max(axis=(0, 1))
    image = Image.new("RGB", (1800, 1800), "#17222c")
    draw = ImageDraw.Draw(image)
    scale = 1640 / max(high[0] - low[0], high[2] - low[2])
    for triangle in all_roads:
        points = [(80 + (float(p[0]) - low[0]) * scale, 1720 - (float(p[2]) - low[2]) * scale) for p in triangle]
        elevation = np.clip((triangle[:, 1].mean() - low[1]) / max(1, high[1] - low[1]), 0, 1)
        colour = (int(100 + 110 * elevation), int(120 + 65 * elevation), int(150 - 65 * elevation))
        draw.polygon(points, fill=colour)
    for model in report["models"]:
        if not model["file"].endswith("-Pits.kn5"):
            continue
        for node in model["nodes"]:
            if "AC_" in node["name"] and "matrix" in node:
                x, y, z = node["matrix"][3][:3]
                u = 80 + (x - low[0]) * scale; v = 1720 - (z - low[2]) * scale
                draw.ellipse((u - 4, v - 4, u + 4, v + 4), fill="#ffd26d")
                if node["name"] in ("AC_PIT_0", "AC_START_0"):
                    draw.text((u + 8, v), node["name"], fill="white")
    draw.text((40, 20), f"{config['sourceDistrict']} - source road geometry | X {low[0]:.0f}..{high[0]:.0f} m | Z {low[2]:.0f}..{high[2]:.0f} m", fill="white")
    image.save(output / "source-overview.png")
    print(f"Inventory and overview: {output}", flush=True)


if __name__ == "__main__":
    main()

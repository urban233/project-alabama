"""Decode verified embedded textures to PNG with their alpha channel retained."""

import argparse
import json
from pathlib import Path

from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--variant", choices=("Sample", "District"), required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    base = root / "unity/Assets/Alabama/Art/Maps/NfsWorld"
    contract = json.loads((base / args.variant / "contract.json").read_text())
    output = base / "Textures"
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for material in contract["materials"]:
        if not material["texture"]:
            continue
        target = output / material["texture"]
        with Image.open(root / "source-art/maps/nfs-world/textures" / material["stored"]) as image:
            image.load()
            rgba = image.convert("RGBA")
            if not target.exists():
                rgba.save(target)
            records.append(dict(file=target.name, width=image.width, height=image.height,
                                alphaRange=rgba.getchannel("A").getextrema()))
    (root / f"artifacts/NfsWorld/textures-{args.variant}.json").write_text(json.dumps(records, indent=2))
    print(f"Decoded {len(records)} material textures", flush=True)


if __name__ == "__main__":
    main()

"""Refresh provenance hashes for the car and industrial visual review assets."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def records(paths):
    return [{"path": path.relative_to(ROOT).as_posix(),
             "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
            for path in sorted(set(paths)) if path.is_file() and not path.name.endswith(".meta")]


def main():
    manifest = ROOT / "docs/assets.json"
    data = json.loads(manifest.read_text())
    data["assets"] = [asset for asset in data["assets"]
                      if asset["id"] not in {"e46-race-study", "polyhaven-vehicle-review", "industrial-street-kit", "district-loop-road"}]
    vehicle = ROOT / "unity/Assets/Alabama/Art/Vehicles/E46"
    source = ROOT / "source-art/vehicles/e46"
    data["assets"].append({
        "id": "e46-race-study", "title": "E46 race study",
        "creator": "BlenderCentral; adaptation by Project Alabama contributors",
        "originalUrl": "https://blendswap.com/blend/10588",
        "license": "CC-BY-SA-3.0",
        "licenseEvidence": "source-art/third-party/blendswap/e46/BLENDSWAP_LICENSE.txt",
        "modifications": ["Selective mesh reduction", "Race details and widened arches",
                          "Replacement materials and generated texture maps", "Metre scale and four wheel pivots"],
        "generator": "tools/blender/build_e46.py", "updatedOn": "2026-10-01",
        "sourceArchiveSha256": "8bfff8febbb2ba3afe8be684e686df8f4848ec03df8af4c8f9623d6f6ffa23b5",
        "sourceArchiveRedistributed": False,
        "files": records([*vehicle.glob("*"), source / "E46_Race.blend", source / "E46_Race.report.json", source / "LICENSE.md"]),
        "triangles": 60311, "sourceEvaluatedTriangles": 816368,
        "purpose": "Static visual review; detailed GTR match and driving remain pending"
    })
    data["assets"].append({
        "id": "polyhaven-vehicle-review", "license": "CC0-1.0",
        "title": "Asphalt 04 and Industrial Sunset Pure Sky",
        "creator": "Jenelle van Heerden, Sergej Majboroda, Jarod Guest",
        "originalUrls": ["https://polyhaven.com/a/asphalt_04", "https://polyhaven.com/a/industrial_sunset_puresky"],
        "licenseEvidence": "source-art/third-party/polyhaven/LICENSE.md",
        "generator": "tools/fetch-environment.ps1", "updatedOn": "2026-10-01",
        "files": records([*(ROOT / "source-art/third-party/polyhaven").rglob("*"),
                          *(ROOT / "unity/Assets/Alabama/Art/Environment/PolyHaven").glob("*")])
    })
    kit = ROOT / "unity/Assets/Alabama/Art/Environment/IndustrialKit"
    scene_art = ROOT / "unity/Assets/Alabama/Art/IndustrialStreet"
    kit_source = ROOT / "source-art/environment/industrial-kit"
    data["assets"].append({
        "id": "industrial-street-kit", "title": "Original industrial street kit",
        "creator": "Project Alabama contributors", "origin": "Original seeded geometry created in this repository",
        "license": "CC0-1.0", "licenseEvidence": "source-art/environment/industrial-kit/LICENSE.md",
        "generator": "tools/blender/build_industrial_kit.py", "updatedOn": "2026-10-01",
        "modules": 13, "libraryTriangles": 17536,
        "files": records([*kit.glob("*"), *scene_art.glob("*"),
                          kit_source / "IndustrialKit.blend", kit_source / "IndustrialKit.report.json",
                          kit_source / "LICENSE.md", ROOT / "unity/Assets/Alabama/Scenes/IndustrialStreet.unity"]),
        "purpose": "Single-street visual review in the pre-alpha demo"
    })
    loop_art = ROOT / "unity/Assets/Alabama/Art/DistrictLoop"
    data["assets"].append({
        "id": "district-loop-road", "title": "Closed district road and marking meshes",
        "creator": "Project Alabama contributors", "origin": "Original geometry generated in Unity",
        "license": "CC0-1.0", "licenseEvidence": "source-art/district-loop/LICENSE.md",
        "generator": "unity/Assets/Alabama/Editor/DistrictLoopScene.cs", "updatedOn": "2026-10-01",
        "files": records([*loop_art.glob("*"), ROOT / "source-art/district-loop/LICENSE.md"]),
        "compositeScenePath": "unity/Assets/Alabama/Scenes/DistrictLoop.unity",
        "purpose": "Continuous physics road and markings for the 917 metre playable route"
    })
    manifest.write_bytes((json.dumps(data, indent=2) + "\n").encode("utf-8"))


if __name__ == "__main__":
    main()

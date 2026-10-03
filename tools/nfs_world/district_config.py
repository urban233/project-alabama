"""Resolve explicit district recipes without changing the accepted Downtown defaults."""

import json
from pathlib import Path

SHARED_ORIGIN = (1978.0977783203125, 99.6069107055664, 1165.766845703125)
DEFAULT = dict(
    districtId="downtown", sourceDistrict="DowntownRockport",
    sourceRoot="source-art/maps/nfs-world/original/mauleous_nfs_world",
    outputRoot="unity/Assets/Alabama/Art/Maps/NfsWorld",
    editableRoot="source-art/maps/nfs-world/blender",
    rawTextureRoot="source-art/maps/nfs-world/textures",
    artifactRoot="artifacts/NfsWorld",
    sourceConfig="source-art/maps/nfs-world/original/mauleous_nfs_world/extension/ext_config.ini",
    sharedOrigin=list(SHARED_ORIGIN),
    spawnNode="AC_PIT_0",
)


def load(root, recipe=None):
    config = DEFAULT.copy() if recipe is None else json.loads((root / recipe).read_text(encoding="utf-8"))
    for key in DEFAULT:
        if key not in config:
            raise ValueError(f"District recipe lacks {key}")
    if config["sharedOrigin"] != list(SHARED_ORIGIN):
        raise ValueError("Districts must share Downtown's accepted source origin")
    for key in ("sourceRoot", "outputRoot", "editableRoot", "rawTextureRoot", "artifactRoot", "sourceConfig"):
        path = Path(config[key])
        if path.is_absolute() or ".." in path.parts or not path.parts:
            raise ValueError(f"Unsafe district path: {key}")
        resolved = (root / path).resolve()
        if not resolved.is_relative_to(root.resolve()):
            raise ValueError(f"District path escapes checkout: {key}")
    if recipe is not None and str((root / config["outputRoot"]).resolve()).casefold() == str((root / DEFAULT["outputRoot"]).resolve()).casefold():
        raise ValueError("A neighbouring district requires its own output root")
    return config

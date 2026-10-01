"""Check that redistributable asset files still match the reviewed manifest."""

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
manifest = json.loads((ROOT / "docs" / "assets.json").read_text(encoding="utf-8"))
failures = []
checked = 0
for asset in manifest["assets"]:
    evidence = asset.get("licenseEvidence")
    if not evidence or not (ROOT / evidence).is_file():
        failures.append(f"{asset['id']}: missing license evidence {evidence}")
    for record in asset["files"]:
        relative = Path(record["path"])
        candidate = (ROOT / relative).resolve()
        if not candidate.is_relative_to(ROOT) or not candidate.is_file():
            failures.append(f"{asset['id']}: missing file {relative}")
            continue
        with candidate.open("rb") as source:
            digest = hashlib.file_digest(source, "sha256").hexdigest()
        if digest != record["sha256"]:
            failures.append(f"{asset['id']}: checksum mismatch {relative}")
        else:
            checked += 1

unity_assets = ROOT / "unity" / "Assets"
for asset_path in unity_assets.rglob("*"):
    if asset_path.suffix == ".meta":
        continue
    meta_path = asset_path.with_name(asset_path.name + ".meta")
    if not meta_path.is_file():
        failures.append(f"Missing Unity metadata: {asset_path.relative_to(ROOT)}")

if failures:
    raise SystemExit("\n".join(failures))
print(f"Verified {checked} files across {len(manifest['assets'])} assets, license evidence, and Unity metadata.")

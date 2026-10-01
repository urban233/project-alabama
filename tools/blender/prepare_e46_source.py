"""Extract the pinned E46 source archive without running any downloaded code."""

import hashlib
from pathlib import Path
import zipfile


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "source-art/third-party/blendswap/e46/original"
ARCHIVE = SOURCE / "BMW M3 E46.zip"
ARCHIVE_SHA256 = "8bfff8febbb2ba3afe8be684e686df8f4848ec03df8af4c8f9623d6f6ffa23b5"
EXPECTED_FILES = {"BMW M3 E46.blend", "BLENDSWAP_LICENSE.txt"}


def main():
    checksum = hashlib.sha256(ARCHIVE.read_bytes()).hexdigest()
    if checksum != ARCHIVE_SHA256:
        raise ValueError("The archive differs from the inspected E46 source download.")
    with zipfile.ZipFile(ARCHIVE) as archive:
        if set(archive.namelist()) != EXPECTED_FILES:
            raise ValueError("Unexpected archive contents; inspect before extracting.")
        for name in sorted(EXPECTED_FILES):
            if archive.getinfo(name).file_size > 40_000_000:
                raise ValueError(f"Unexpected source size: {name}")
            content = archive.read(name)
            destination = SOURCE / name
            if destination.exists() and destination.read_bytes() != content:
                raise ValueError(f"Preserved source has changed: {name}")
            if not destination.exists():
                destination.write_bytes(content)
            print(f"Preserved {name}: {len(content)} bytes; SHA-256 "
                  f"{hashlib.sha256(content).hexdigest()}")


if __name__ == "__main__":
    main()

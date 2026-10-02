# Asset release record

| Release | Content ID | Payload | Notes |
| --- | --- | --- | --- |
| base 1.0.0 | `5e1d2765406a2dbda4820d5620b854f463798609ac4e7f85636f464ea31102e0` | 201 files / 38,379,439 bytes | Pins the existing reviewed ZIP unchanged |
| nfs-world 0.1.0 | `ca86c3dbca728a3312c17c2c965df0bc3c5c64444d8200e56c2cc0765ac8d52d` | 86,417 files / 7,047,035,106 bytes | Downtown prototype/art scene, authoring sources and local map inputs; requires base 1.0.0 |

The initial private map ZIP is generated locally at
`artifacts/share/project-alabama-nfs-world-0.1.0.zip`, with a `.zip.sha256` sidecar.
Its SHA-256 is `db8fdd53a814573df1066f862980ef375c2964509d53826df76a2c6fd8ee1076`.
Another export may have different ZIP timestamps/compression and therefore a
different archive hash, while restoring exactly the same locked asset contents.
The lock verifies the embedded manifest and each file, independently of ZIP naming.

The original base ZIP passed locked import without replacing any file. The map
ZIP passed locked import for all 86,417 files with zero asset replacements/removals;
the installed receipt is registered locally. The snapshot validated sidecars and
unique GUIDs. All 32 Python tooling tests pass; synthetic releases
to verify corruption handling, backups, direct upgrades, rollback and protection
of local changes, including an edit saved during archive verification.

Artwork and detailed map manifests remain ignored. This record, profiles and
locks contain metadata only; they do not grant redistribution rights or publish
the archive. See [the workflow](../asset-versioning.md) and
[the developer plan](../two-developer-plan.md).

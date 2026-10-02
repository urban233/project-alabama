"""Bounded static KN5 reader. File-layout references are recorded in README.md.

Matrices use the source's row-vector convention. Geometry is returned in source
world coordinates, before any Blender/Unity axis conversion. No source code or
third-party converter is embedded or executed.
"""

from __future__ import annotations

import hashlib
import struct
from pathlib import Path

import numpy as np


class InvalidKn5(ValueError):
    pass


class Reader:
    def __init__(self, path, texture_dir=None):
        self.path = Path(path)
        self.file = self.path.open("rb")
        self.size = self.path.stat().st_size
        self.nodes = []
        self.textures = []
        self.materials = []
        self.texture_dir = Path(texture_dir) if texture_dir else None
        try:
            self.header()
        except Exception:
            self.file.close()
            raise

    def __enter__(self):
        return self

    def __exit__(self, *args):
        self.file.close()

    def read(self, count):
        if count < 0 or count > self.size - self.file.tell():
            raise InvalidKn5(f"{self.path.name}: invalid byte count {count} at {self.file.tell()}")
        result = self.file.read(count)
        if len(result) != count:
            raise InvalidKn5("Truncated file")
        return result

    def unpack(self, format):
        return struct.unpack(format, self.read(struct.calcsize(format)))

    def integer(self, maximum=100_000_000):
        result = self.unpack("<i")[0]
        if not 0 <= result <= maximum:
            raise InvalidKn5(f"Invalid count {result} at {self.file.tell() - 4}")
        return result

    def string(self):
        return self.read(self.integer(65536)).decode("utf-8")

    def header(self):
        if self.read(6) != b"sc6969":
            raise InvalidKn5("Not an unprotected KN5 file (sc6969 magic missing)")
        self.version = self.integer(6)
        if self.version not in (5, 6):
            raise InvalidKn5(f"Only static KN5 versions 5 and 6 are supported: {self.version}")
        if self.version == 6:
            self.extra = self.unpack("<i")[0]
        for _ in range(self.integer(100000)):
            active = self.integer(1)
            name = self.string()
            size = self.integer()
            digest = hashlib.sha256()
            header = bytearray()
            # Spool to a temporary content-addressed location only when requested.
            temporary = None
            if self.texture_dir and size:
                self.texture_dir.mkdir(parents=True, exist_ok=True)
                temporary = self.texture_dir / "pending-texture.bin"
            output = temporary.open("wb") if temporary else None
            try:
                remaining = size
                while remaining:
                    block = self.read(min(remaining, 1024 * 1024))
                    digest.update(block)
                    if len(header) < 256:
                        header.extend(block[:256 - len(header)])
                    if output:
                        output.write(block)
                    remaining -= len(block)
            finally:
                if output:
                    output.close()
            sha256 = digest.hexdigest()
            suffix = ".dds" if header[:4] == b"DDS " else Path(name).suffix.lower()
            if suffix not in (".dds", ".png", ".jpg", ".jpeg", ".tga", ".bmp"):
                suffix = ".bin"
            stored = sha256 + suffix if size else ""
            if temporary:
                target = self.texture_dir / stored
                if target.exists():
                    temporary.unlink()
                else:
                    temporary.replace(target)
            description = dict(name=name, active=bool(active), bytes=size, sha256=sha256, stored=stored)
            if header[:4] == b"DDS " and len(header) >= 128:
                description.update(height=struct.unpack_from("<I", header, 12)[0],
                                   width=struct.unpack_from("<I", header, 16)[0],
                                   compression=bytes(header[84:88]).decode("ascii", errors="replace"))
            self.textures.append(description)
        for index in range(self.integer(100000)):
            name, shader = self.string(), self.string()
            blend, alpha = self.unpack("<BB")
            depth = self.unpack("<i")[0]
            properties = {}
            for _ in range(self.integer(10000)):
                key = self.string()
                properties[key] = list(self.unpack("<10f"))
            samplers = {}
            for _ in range(self.integer(10000)):
                key = self.string()
                slot = self.unpack("<i")[0]
                samplers[key] = dict(slot=slot, texture=self.string())
            self.materials.append(dict(index=index, name=name, shader=shader, blend=blend,
                                       alpha_test=bool(alpha), depth=depth,
                                       properties=properties, samplers=samplers))

    def meshes(self):
        yield from self.node(np.eye(4, dtype=np.float32), True, -1, 0)
        if self.file.tell() != self.size:
            raise InvalidKn5(f"Unconsumed trailing bytes: {self.size - self.file.tell()}")

    def node(self, parent_matrix, parent_active, parent_id, depth):
        if depth > 256:
            raise InvalidKn5("Node hierarchy too deep")
        kind = self.integer(3)
        name = self.string()
        children = self.integer(1000000)
        active = bool(self.unpack("<B")[0]) and parent_active
        index = len(self.nodes)
        record = dict(index=index, name=name, type=kind, parent=parent_id, active=active)
        self.nodes.append(record)
        matrix = parent_matrix
        if kind == 1:
            local = np.array(self.unpack("<16f"), dtype=np.float32).reshape(4, 4)
            matrix = local @ parent_matrix
            record["matrix"] = matrix.tolist()
        elif kind == 2:
            casts_shadows, visible, transparent = self.unpack("<BBB")
            vertex_count = self.integer()
            vertices = np.frombuffer(self.read(vertex_count * 44), dtype="<f4").reshape(-1, 11)
            index_count = self.integer()
            if index_count % 3:
                raise InvalidKn5("Mesh indices are not triangles")
            indices = np.frombuffer(self.read(index_count * 2), dtype="<u2").reshape(-1, 3)
            material = self.unpack("<i")[0]
            layer, lod_in, lod_out, cx, cy, cz, radius, renderable = self.unpack("<iff4fB")
            if material < 0 or material >= len(self.materials):
                raise InvalidKn5(f"Mesh {name} has invalid material index {material}")
            if indices.size and int(indices.max()) >= vertex_count:
                raise InvalidKn5(f"Mesh {name} has out-of-range indices")
            positions = vertices[:, :3] @ matrix[:3, :3] + matrix[3, :3]
            if not np.isfinite(positions).all():
                raise InvalidKn5(f"Mesh {name} contains non-finite positions")
            normal_matrix = np.linalg.inv(matrix[:3, :3]).T
            normals = vertices[:, 3:6] @ normal_matrix
            normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-12)
            if np.linalg.det(matrix[:3, :3]) < 0:
                indices = indices[:, [0, 2, 1]]
            record.update(vertices=vertex_count, triangles=index_count // 3, material=material,
                          casts_shadows=bool(casts_shadows), visible=bool(visible),
                          transparent=bool(transparent), renderable=bool(renderable),
                          layer=layer, lod_in=lod_in, lod_out=lod_out,
                          bounds=[positions.min(axis=0).tolist(), positions.max(axis=0).tolist()] if vertex_count else None)
            yield dict(record=record, positions=positions, normals=normals,
                       uv=vertices[:, 6:8], triangles=indices,
                       material=self.materials[material])
        else:
            raise InvalidKn5(f"Unsupported node type {kind} ({name}); static map reader does not infer skinning")
        for _ in range(children):
            yield from self.node(matrix, active, index, depth + 1)


def file_hash(path):
    with Path(path).open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()

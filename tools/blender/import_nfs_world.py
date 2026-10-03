"""Explicit KN5 -> editable Blender -> binary FBX static map handoff."""

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

import bpy
import numpy as np


def blender_vectors(values):
    # Source Y-up/Z-forward to Blender Z-up/-Y-forward: rotation, determinant +1.
    return values[:, [0, 2, 1]] * np.array([1, -1, 1], dtype=np.float32)


def material_alpha_overrides(config):
    rules = []
    for section in re.split(r"(?m)^\[", config):
        if not section.startswith("SHADER_REPLACEMENT_"):
            continue
        fields = {}
        for line in section.splitlines()[1:]:
            line = line.split(";", 1)[0]
            if "=" in line:
                key, value = line.split("=", 1)
                fields[key.strip()] = value.strip()
        if "MATERIALS" in fields and fields.get("BLEND_MODE") in ("ALPHA_TEST", "OPAQUE"):
            patterns = [re.compile("^" + re.escape(p.strip()).replace(r"\?", ".*").replace(r"\*", ".*") + "$")
                        for p in fields["MATERIALS"].split(",")]
            rules.append((patterns, fields["BLEND_MODE"] == "ALPHA_TEST"))
    return rules


def add_mesh(name, positions, normals, uv, triangles, material):
    mesh = bpy.data.meshes.new(name)
    mesh.vertices.add(len(positions))
    mesh.vertices.foreach_set("co", blender_vectors(positions).ravel())
    mesh.loops.add(triangles.size)
    mesh.loops.foreach_set("vertex_index", triangles.astype(np.int32).ravel())
    mesh.polygons.add(len(triangles))
    mesh.polygons.foreach_set("loop_start", np.arange(len(triangles), dtype=np.int32) * 3)
    mesh.polygons.foreach_set("loop_total", np.full(len(triangles), 3, dtype=np.int32))
    mesh.polygons.foreach_set("use_smooth", np.ones(len(triangles), dtype=bool))
    layer = mesh.uv_layers.new(name="UVMap")
    values = uv[triangles.ravel()].copy()
    values[:, 1] = 1 - values[:, 1]
    layer.data.foreach_set("uv", values.ravel())
    mesh.update()
    mesh.normals_split_custom_set_from_vertices(blender_vectors(normals))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(material)
    return obj


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--config", type=Path)
    parser.add_argument("--sample", action="store_true")
    parser.add_argument("--category", default="all")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    root = args.root.resolve()
    sys.path.insert(0, str(root / "tools/nfs_world"))
    from kn5 import Reader
    from district_config import load

    config = load(root, args.config)
    inventory = json.loads((root / config["artifactRoot"] / "inventory.json").read_text())
    alpha_rules = material_alpha_overrides((root / config["sourceConfig"]).read_text())
    pit_model = next(m for m in inventory["models"] if m["file"].endswith("-Pits.kn5"))
    pit = next(n for n in pit_model["nodes"] if n["name"] == config["spawnNode"] and n["type"] == 1)
    origin = np.array(config["sharedOrigin"], dtype=np.float32)
    variant = "Sample" if args.sample else "District"
    export = root / config["outputRoot"] / variant
    export.mkdir(parents=True, exist_ok=True)
    sources = root / config["editableRoot"] / variant
    sources.mkdir(parents=True, exist_ok=True)
    contract = dict(variant=variant, sourceOrigin=origin.tolist(),
                    districtId=config["districtId"], sourceSpawn=pit["matrix"][3][:3],
                    spawnForward=pit["matrix"][2][:3], parts=[], materials=[],
                    sourceSpace="KN5 X,Y,Z -> Blender X,-Z,Y -> Unity -X,Y,Z; one shared source origin")
    material_definitions = {}
    for model in inventory["models"]:
        category = model["file"].removesuffix(".kn5").split("-")[-1]
        if category == "Pits" or (args.category != "all" and category != args.category):
            continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.unit_settings.system = "METRIC"
        bpy.context.scene.unit_settings.scale_length = 1
        materials = {}
        texture_map = {t["name"]: t for t in model["textures"]}
        records = []
        print(f"Importing {category}", flush=True)
        path = root / config["sourceRoot"] / model["file"]
        with Reader(path) as reader:
            for item in reader.meshes():
                record = item["record"]
                if not record["active"] or not record["vertices"] or not record["triangles"]:
                    continue
                positions = item["positions"] - origin
                triangles = item["triangles"]
                if args.sample:
                    # Keep whole triangles intersecting the sample's horizontal square.
                    # This retains shared border vertices exactly, without capping surfaces.
                    centre = np.array(pit["matrix"][3][:3], dtype=np.float32) - origin if args.config else np.zeros(3)
                    points = (positions[triangles] - centre)[:, :, [0, 2]]
                    keep = (points.min(axis=1) <= 170).all(axis=1) & (points.max(axis=1) >= -170).all(axis=1)
                    triangles = triangles[keep]
                    if not len(triangles):
                        continue
                    used, inverse = np.unique(triangles, return_inverse=True)
                    positions = positions[used]
                    normals = item["normals"][used]
                    uv = item["uv"][used]
                    triangles = inverse.reshape(-1, 3)
                else:
                    normals, uv = item["normals"], item["uv"]
                points = positions[triangles]
                cross = np.cross(points[:, 1] - points[:, 0], points[:, 2] - points[:, 0])
                valid = np.einsum("ij,ij->i", cross, cross) > 1e-16
                removed_degenerates = int((~valid).sum())
                triangles = triangles[valid]
                if not len(triangles):
                    continue
                used, inverse = np.unique(triangles, return_inverse=True)
                positions, normals, uv = positions[used], normals[used], uv[used]
                triangles = inverse.reshape(-1, 3)
                physical = record["name"].startswith(("1ROAD", "1WALL", "1GRASS_TRN_CT_Road_GolfCourse"))
                collision = physical or record["name"].startswith(("1DIRT", "1GRASS"))
                source_material = item["material"]
                if source_material["shader"] not in ("ksPerPixel", "ksPerPixelAT", "ksTree"):
                    raise RuntimeError(f"Unmapped source shader {source_material['shader']}")
                sampler = source_material["samplers"].get("txDiffuse")
                texture = texture_map.get(sampler["texture"]) if sampler else None
                if sampler and not texture:
                    raise RuntimeError(f"Missing embedded diffuse texture: {sampler['texture']}")
                alpha = source_material["alpha_test"] or source_material["shader"] in ("ksPerPixelAT", "ksTree")
                for patterns, override in alpha_rules:
                    if any(pattern.fullmatch(source_material["name"]) for pattern in patterns):
                        alpha = override
                # AC extension explicitly requests double-sided building/tree/wall rendering.
                definition = dict(texture=texture["sha256"] + ".png" if texture and texture["bytes"] else "",
                                  stored=texture["stored"] if texture else "", alphaClip=alpha,
                                  cutoff=max(.1, source_material["properties"].get("ksAlphaRef", [.5])[0]),
                                  doubleSided=True, family="foliage" if category == "Trees" else category.lower())
                signature = {k: v for k, v in definition.items() if k not in ("family", "stored")}
                key = "NFW_" + hashlib.sha256(json.dumps(signature, sort_keys=True).encode()).hexdigest()[:20]
                definition["name"] = key
                material_definitions[key] = definition
                if key not in materials:
                    material = bpy.data.materials.new(key)
                    material.use_nodes = True
                    material.diffuse_color = (.45, .45, .45, 1)
                    if texture and texture["bytes"]:
                        image = bpy.data.images.load(str(root / config["rawTextureRoot"] / texture["stored"]), check_existing=True)
                        node = material.node_tree.nodes.new("ShaderNodeTexImage")
                        node.image = image
                        bsdf = material.node_tree.nodes.get("Principled BSDF")
                        material.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
                        if alpha:
                            material.node_tree.links.new(node.outputs["Alpha"], bsdf.inputs["Alpha"])
                        bsdf.inputs["Roughness"].default_value = .88
                    materials[key] = material
                name = f"{category}_{record['index']:05d}"
                obj = add_mesh(name, positions, normals, uv, triangles, materials[key])
                obj["source_name"] = record["name"]
                obj["collision"] = collision
                obj["visible"] = not physical and record["visible"] and record["renderable"]
                records.append(dict(name=name, sourceName=record["name"], collision=collision,
                                    visible=bool(obj["visible"]), triangles=len(triangles),
                                    bounds=[positions.min(axis=0).tolist(), positions.max(axis=0).tolist()],
                                    boundsMin=positions.min(axis=0).tolist(), boundsMax=positions.max(axis=0).tolist(),
                                    removedDegenerates=removed_degenerates,
                                    castsShadows=record["casts_shadows"], material=key))
        if not records:
            continue
        # Axis markers verify the FBX importer using transformed object positions.
        for name, position in (("OriginMarker", (0, 0, 0)), ("XMarker", (10, 0, 0)),
                               ("YMarker", (0, 10, 0)), ("ZMarker", (0, 0, 10))):
            empty = bpy.data.objects.new(name, None)
            bpy.context.collection.objects.link(empty)
            empty.location = blender_vectors(np.array([position], dtype=np.float32))[0]
        bpy.ops.wm.save_as_mainfile(filepath=str(sources / f"{category}.blend"))
        bpy.ops.export_scene.fbx(filepath=str(export / f"{category}.fbx"),
                                 object_types={"MESH", "EMPTY"}, use_selection=False,
                                 global_scale=1, apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                                 use_mesh_modifiers=False, mesh_smooth_type="OFF", bake_anim=False,
                                 add_leaf_bones=False, path_mode="AUTO")
        part = dict(category=category, file=category + ".fbx", meshes=records,
                    triangles=sum(r["triangles"] for r in records))
        contract["parts"].append(part)
        print(f"Exported {category}: {len(records)} meshes, {part['triangles']} triangles", flush=True)
    contract["materials"] = list(material_definitions.values())
    (export / "contract.json").write_text(json.dumps(contract, indent=2), encoding="utf-8")
    print(f"Export contract: {export / 'contract.json'}", flush=True)


if __name__ == "__main__":
    main()

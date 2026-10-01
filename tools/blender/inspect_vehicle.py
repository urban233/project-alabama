"""Read-only inventory of a third-party vehicle source, with auto-execution off.

Example:
  blender --background --factory-startup --disable-autoexec --python
  tools/blender/inspect_vehicle.py -- --input path/to/source.blend
  --report artifacts/VehicleSource/inventory.json

The source is never saved or changed. Names are inventory data, not instructions.
"""

import argparse
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(argv)


def vector(value):
    return [round(float(axis), 6) for axis in value]


def mesh_counts(mesh):
    mesh.calc_loop_triangles()
    return {"vertices": len(mesh.vertices), "polygons": len(mesh.polygons),
            "triangles": len(mesh.loop_triangles)}


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(point) for point in obj.bound_box]
    minimum = Vector(tuple(min(point[axis] for point in corners) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in corners) for axis in range(3)))
    return {"minimum": vector(minimum), "maximum": vector(maximum),
            "dimensions": vector(maximum - minimum)}


def describe_modifier(modifier):
    result = {"name": modifier.name, "type": modifier.type,
              "viewport": modifier.show_viewport, "render": modifier.show_render}
    for property_name in ("levels", "render_levels", "ratio", "merge_threshold",
                          "width", "segments", "use_axis", "use_clip"):
        if hasattr(modifier, property_name):
            value = getattr(modifier, property_name)
            if isinstance(value, (bool, int, float, str)):
                result[property_name] = value
            else:
                result[property_name] = list(value)
    return result


def main():
    args = arguments()
    source = args.input.resolve(strict=True)
    if source.suffix.lower() != ".blend":
        raise ValueError("Expected a .blend source file.")
    bpy.context.preferences.filepaths.use_scripts_auto_execute = False
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    report = {"source": str(source), "blenderVersion": bpy.app.version_string,
              "autoExecute": False, "scene": bpy.context.scene.name,
              "units": {"system": bpy.context.scene.unit_settings.system,
                        "scale": bpy.context.scene.unit_settings.scale_length},
              "objects": [], "materials": [], "images": [], "texts": []}
    for obj in sorted(bpy.data.objects, key=lambda item: item.name):
        item = {"name": obj.name, "type": obj.type,
                "parent": obj.parent.name if obj.parent else None,
                "collections": sorted(collection.name for collection in obj.users_collection),
                "location": vector(obj.location), "scale": vector(obj.scale),
                "rotationRadians": vector(obj.rotation_euler),
                "hideRender": obj.hide_render, "hideViewport": obj.hide_viewport,
                "modifiers": [describe_modifier(modifier) for modifier in obj.modifiers],
                "materials": [slot.material.name if slot.material else None
                              for slot in obj.material_slots]}
        if obj.type == "MESH":
            item["sourceMesh"] = mesh_counts(obj.data)
            item["bounds"] = world_bounds(obj)
            if obj.name in bpy.context.scene.objects:
                evaluated = obj.evaluated_get(depsgraph)
                mesh = evaluated.to_mesh()
                try:
                    item["evaluatedMesh"] = mesh_counts(mesh)
                    item["evaluatedBounds"] = world_bounds(evaluated)
                finally:
                    evaluated.to_mesh_clear()
        report["objects"].append(item)
    for material in sorted(bpy.data.materials, key=lambda item: item.name):
        report["materials"].append({"name": material.name,
                                    "diffuseColor": vector(material.diffuse_color),
                                    "usesNodes": material.use_nodes,
                                    "nodeTypes": sorted({node.bl_idname for node in material.node_tree.nodes})
                                    if material.node_tree else []})
    for image in sorted(bpy.data.images, key=lambda item: item.name):
        report["images"].append({"name": image.name, "filepath": image.filepath,
                                 "packed": bool(image.packed_file),
                                 "size": list(image.size), "source": image.source})
    for text in sorted(bpy.data.texts, key=lambda item: item.name):
        report["texts"].append({"name": text.name, "filepath": text.filepath,
                                "useModule": text.use_module,
                                "lineCount": len(text.lines)})
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_bytes((json.dumps(report, indent=2) + "\n").encode("utf-8"))
    print(f"Inventory saved: {args.report.resolve()}")
    print(f"Objects: {len(report['objects'])}; embedded texts: {len(report['texts'])}")


if __name__ == "__main__":
    main()

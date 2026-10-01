"""Adapt the pinned BlenderCentral E46 into a lower polygon racing-car study.

Run with Blender 4.4: --background --factory-startup --disable-autoexec
--python tools/blender/build_e46.py -- [--render]
Original download is preserved. Generated geometry is CC-BY-SA 3.0.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "source-art/third-party/blendswap/e46/original/BMW M3 E46.blend"
SOURCE_HASH = "2d4f03e49e636292aed26417446e0767f4077952561193ff98759d08582f17fa"
OUTPUT = ROOT / "source-art/vehicles/e46"
EXPORT = ROOT / "unity/Assets/Alabama/Art/Vehicles/E46"
EVIDENCE = ROOT / "artifacts/CarAdaptation"
BODY_COLLECTIONS = {f"Collection {i}" for i in (1, 2, 3, 4, 5, 13, 14, 15)}
OMIT = {"BMW Hood Sign", "M3 Side Logo", "Front Lower Grill", "Taillight Bulbs",
        "Cylinder.001", "Back Seat", "Headrest LR", "Headrest RR"}
WHEEL_PARTS = {"LB Tire": .08, "Rim LR": .30, "Rim Cap LR": .12,
               "Rim Nuts LR": .25, "LF Brake.001": .045, "Brake Clamp FL.001": .10}
SCALE = 4.617 / (4.599652 + 4.619929)
GROUND = -1.479949
REAR_PIVOT = Vector((1.549801, 2.601484, -.83264))
FRONT_Y = -3.042997

# Linear-light colors; these also form the explicit Unity material contract.
MATERIALS = {
    "E46_Silver": ((.48, .51, .55, 1), .35, .42),
    "E46_Blue": ((.012, .047, .16, 1), .35, .42),
    "E46_Paint": ((1, 1, 1, 1), .35, .42),
    "E46_TailLens": ((1, 1, 1, 1), .12, .24),
    "E46_Trim": ((.012, .014, .016, 1), .05, .55),
    "E46_Rubber": ((.009, .01, .012, 1), 0, .84),
    "E46_Alloy": ((.38, .40, .43, 1), .85, .25),
    "E46_Brake": ((.18, .19, .20, 1), .75, .55),
    "E46_Caliper": ((.36, .025, .013, 1), .25, .42),
    "E46_Glass": ((.025, .043, .06, .60), .18, .13),
    "E46_Lens": ((.25, .30, .35, .16), .05, .10),
    "E46_RedLight": ((.20, .004, .002, 1), .12, .20),
    "E46_WhiteLight": ((.65, .69, .72, 1), .25, .23),
    "E46_Amber": ((.65, .15, .006, 1), .05, .25),
}


def material_family(name):
    if name == "Car Paint":
        return "E46_Paint"
    if name == "Window Glass":
        return "E46_Glass"
    if name == "Headlight Glass":
        return "E46_Lens"
    if "Red" in name:
        return "E46_RedLight"
    if "White" in name or name == "Headlight Lamps":
        return "E46_WhiteLight"
    if "Blicker" in name:
        return "E46_Amber"
    if name == "Tires":
        return "E46_Rubber"
    if name == "Brake Caliper":
        return "E46_Caliper"
    if name == "Disk Brake":
        return "E46_Brake"
    if any(part in name for part in ("Rim", "Chrome", "Reflective")):
        return "E46_Alloy"
    return "E46_Trim"


def create_material(name, color, metallic, roughness):
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Alpha"].default_value = color[3]
    if color[3] < 1:
        material.surface_render_method = "DITHERED"
    if name == "E46_RedLight":
        shader.inputs["Emission Color"].default_value = color
        shader.inputs["Emission Strength"].default_value = .02
    return material


def color_texture(name, pixel_color, size=512):
    """Write explicit sRGB pixels, then load them with sRGB interpretation."""
    def srgb(value):
        return 12.92 * value if value <= .0031308 else 1.055 * value ** (1 / 2.4) - .055
    image = bpy.data.images.new(name, width=size, height=size, alpha=False, is_data=True)
    pixels = []
    for y in range(size):
        for x in range(size):
            pixels.extend([*(srgb(channel) for channel in pixel_color(x / size, y / size)), 1])
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(EXPORT / f"{name}.png")
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)
    return bpy.data.images.load(str(EXPORT / f"{name}.png"))


def texture_material(material, image):
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    node.extension = "EXTEND"
    material.node_tree.links.new(node.outputs["Color"], material.node_tree.nodes.get("Principled BSDF").inputs["Base Color"])


def create_paint_maps(materials):
    texture_material(materials["E46_Paint"], color_texture("E46_Paint", lambda u, v:
                     MATERIALS["E46_Silver" if v * 1.6 > .945 else "E46_Blue"][0][:3]))
    def tail_color(u, v):
        base = (.56, .58, .59) if .66 < v < .88 else (.25, .005, .003)
        rib = .66 if int(u * 160) % 8 == 0 else 1
        edge = .65 if v < .035 or v > .97 else 1
        return tuple(channel * rib * edge for channel in base)
    texture_material(materials["E46_TailLens"], color_texture("E46_TailLens", tail_color))


def triangle_count(mesh):
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def enable_collections(layer):
    layer.exclude = False
    layer.hide_viewport = False
    for child in layer.children:
        enable_collections(child)


def empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.empty_display_size = .15
    return obj


def apply_modifier(obj, modifier):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def copy_part(source, materials, parent, wheel=False, side=1, pivot=None):
    evaluated = source.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = bpy.data.meshes.new_from_object(evaluated, depsgraph=bpy.context.evaluated_depsgraph_get())
    mesh.transform(evaluated.matrix_world)
    families = [material_family(mat.name) if mat else "E46_Trim" for mat in mesh.materials]
    if not families:
        families = ["E46_Trim"]
    material_indices = [polygon.material_index for polygon in mesh.polygons]
    mesh.materials.clear()
    for family in families:
        mesh.materials.append(materials[family])
    for polygon, material_index in zip(mesh.polygons, material_indices):
        polygon.material_index = min(material_index, len(families) - 1)
    if source.name in {"Hood", "Trunk", "Mirror"}:
        for index, family in enumerate(families):
            if family == "E46_Paint":
                mesh.materials[index] = materials["E46_Silver"]
    if source.name == "Tailight Glass":
        for index in range(len(mesh.materials)):
            mesh.materials[index] = materials["E46_TailLens"]
    for vertex in mesh.vertices:
        original = vertex.co.copy()
        if wheel:
            local = (original - REAR_PIVOT) * SCALE
            local.x *= side
            vertex.co = local
        else:
            vertex.co = Vector((original.x * SCALE, (original.y + .0101385) * SCALE,
                                (original.z - GROUND) * SCALE))
            # The source's quarter panels and front fenders share a continuous widening field.
            x, y, z = vertex.co
            edge_weight = min(1, max(0, (abs(x) - .66) / .22))
            edge_weight = edge_weight * edge_weight * (3 - 2 * edge_weight)
            arch_weight = max(math.exp(-((y + 1.52) / .64) ** 2),
                              math.exp(-((y - 1.30) / .69) ** 2))
            height_weight = min(1, max(0, (1.07 - z) / .23))
            height_weight = height_weight * height_weight * (3 - 2 * height_weight)
            vertex.co.x += math.copysign(.095 * edge_weight * arch_weight * height_weight, x)
    if wheel and side < 0:
        mesh.flip_normals()
    mesh.update()
    if not wheel:
        for uv in list(mesh.uv_layers):
            mesh.uv_layers.remove(uv)
        uv = mesh.uv_layers.new(name="GameUV")
        for loop in mesh.loops:
            point = mesh.vertices[loop.vertex_index].co
            uv.data[loop.index].uv = ((point.x + 1) / 2, (point.z - .70) / .23) if source.name == "Tailight Glass" else ((point.y + 2.5) / 5, point.z / 1.6)
    if mesh.has_custom_normals:
        mesh.normals_split_custom_set([(0, 0, 0)] * len(mesh.loops))
    obj = bpy.data.objects.new(source.name.replace(" ", "_") + (f"_{parent.name}" if wheel else ""), mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj["source_object"] = source.name
    before = triangle_count(mesh)
    ratio = WHEEL_PARTS[source.name] if wheel else (.52 if before > 1000 else 1)
    if source.name in {"Seat LF", "SeatRF", "Steering Wheel", "Interior Frame"}:
        ratio = .32
    if ratio < 1:
        modifier = obj.modifiers.new("Game mesh reduction", "DECIMATE")
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        apply_modifier(obj, modifier)
    return obj, {"sourceObject": source.name, "part": obj.name, "beforeReduction": before,
                 "triangles": triangle_count(obj.data)}


def box(name, location, dimensions, material, parent, bevel=.01):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    obj.parent = parent
    if bevel:
        modifier = obj.modifiers.new("Edge highlights", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        apply_modifier(obj, modifier)
        modifier = obj.modifiers.new("Panel normals", "WEIGHTED_NORMAL")
        apply_modifier(obj, modifier)
    return obj


def race_details(body, materials):
    trim, blue, alloy = (materials[name] for name in ("E46_Trim", "E46_Blue", "E46_Alloy"))
    outline = [(-.90, -1.95), (-.98, -2.10), (-.75, -2.28), (-.40, -2.33),
               (0, -2.34), (.40, -2.33), (.75, -2.28), (.98, -2.10), (.90, -1.95)]
    count = len(outline)
    vertices = [(x, y, z) for z in (.19, .22) for x, y in outline]
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)]
    mesh = bpy.data.meshes.new("Front_splitter")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(trim)
    obj = bpy.data.objects.new("Front_splitter", mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = body
    box("Lower_grille", (0, -2.13, .34), (.77, .035, .19), trim, body, .006)
    for x in (-.29, -.19, -.09, .01, .11, .21, .31):
        box("Grille_rib", (x, -2.157, .34), (.009, .012, .17), alloy, body, .001)
    box("Rear_diffuser", (0, 2.14, .265), (1.48, .12, .06), trim, body, .012)
    for x in (-.60, -.30, 0, .30, .60):
        box("Diffuser_fin", (x, 2.14, .24), (.016, .16, .08), trim, body, .002)
    box("Wing_airfoil", (0, 1.96, 1.345), (1.95, .24, .038), trim, body, .015)
    for x in (-.58, .58):
        box("Wing_mount", (x, 1.97, 1.185), (.030, .12, .29), trim, body, .008)
    for x in (-.978, .978):
        box("Wing_endplate", (x, 1.96, 1.345), (.018, .29, .105), trim, body, .006)
    for x in (-.63, .63):
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.064, depth=.14,
                                           location=(x, 2.18, .30), rotation=(math.pi / 2, 0, 0))
        obj = bpy.context.object
        obj.name = "Exhaust_tip"
        obj.parent = body
        obj.data.materials.append(alloy)
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.052, depth=.003,
                                           location=(x, 2.252, .30), rotation=(math.pi / 2, 0, 0))
        obj = bpy.context.object
        obj.name = "Exhaust_inner"
        obj.parent = body
        obj.data.materials.append(trim)


def setup_preview(materials):
    scene = bpy.context.scene
    scene.frame_set(1)
    scene.use_nodes = False
    scene.render.use_compositing = False
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.world = bpy.data.worlds.new("Review World")
    scene.world.use_nodes = True
    nodes = scene.world.node_tree.nodes
    environment = nodes.new("ShaderNodeTexEnvironment")
    environment.image = bpy.data.images.load(str(ROOT / "source-art/third-party/polyhaven/industrial_sunset_puresky/industrial_sunset_puresky_2k.hdr"))
    scene.world.node_tree.links.new(environment.outputs["Color"], nodes.get("Background").inputs["Color"])
    nodes.get("Background").inputs["Strength"].default_value = .45
    floor_material = create_material("Review_floor", (.12, .13, .145, 1), 0, .80)
    box("Review_floor", (0, 0, -.045), (200, 200, .08), floor_material, None, 0)
    for name, position, power, size in (("Key", (-3, -4, 7), 1500, 5),
                                       ("Rim", (4, 2, 5), 1800, 4),
                                       ("Rear_fill", (-1, 5, 4), 900, 3)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = power, "DISK", size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = position
        obj.rotation_euler = (Vector((0, 0, .6)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    cameras = {}
    for name, position in {"rear": (-4.7, 6.8, 2.6), "front": (4.7, -6.8, 2.6),
                            "side": (7.8, .1, 1.8)}.items():
        data = bpy.data.cameras.new(name)
        data.lens = 52
        obj = bpy.data.objects.new("Review_" + name, data)
        scene.collection.objects.link(obj)
        obj.location = position
        obj.rotation_euler = (Vector((0, 0, .65)) - obj.location).to_track_quat("-Z", "Y").to_euler()
        cameras[name] = obj
    scene.camera = cameras["rear"]
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.region_3d.view_perspective = "CAMERA"
                area.spaces.active.shading.color_type = "MATERIAL"
    return cameras


def export_groups(root, groups):
    # Keep per-panel objects in the editable .blend; merge only the explicit FBX handoff.
    for group in groups:
        meshes = [obj for obj in group.children if obj.type == "MESH"]
        bpy.ops.object.select_all(action="DESELECT")
        for obj in meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()
        bpy.context.object.name = group.name + "_Mesh"
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root, *root.children_recursive]:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(EXPORT / "E46_Race.fbx"), use_selection=True,
                             object_types={"MESH", "EMPTY"}, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_UNITS", add_leaf_bones=False,
                             bake_anim=False, path_mode="STRIP", use_custom_props=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--render", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    if hashlib.sha256(SOURCE.read_bytes()).hexdigest() != SOURCE_HASH:
        raise ValueError("The original E46 source does not match the inspected download.")
    for directory in (OUTPUT, EXPORT, EVIDENCE):
        directory.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.use_scripts_auto_execute = False
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE), load_ui=False, use_scripts=False)
    source_objects = list(bpy.data.objects)
    enable_collections(bpy.context.view_layer.layer_collection)
    bpy.context.scene.render.use_simplify = False
    body_sources = [obj for obj in source_objects if obj.type == "MESH"
                    and any(collection.name in BODY_COLLECTIONS for collection in obj.users_collection)
                    and obj.name not in OMIT]
    wheel_sources = [bpy.data.objects[name] for name in WHEEL_PARTS]
    for obj in body_sources + wheel_sources:
        for modifier in obj.modifiers:
            if modifier.type == "SUBSURF":
                modifier.levels = modifier.render_levels = 1
            if modifier.type == "BOOLEAN" and obj.name == "Tailight Main":
                modifier.show_viewport = modifier.show_render = False
        if obj.name.startswith("Brake Clamp"):
            for modifier in list(obj.modifiers):
                if modifier.type == "SUBSURF":
                    obj.modifiers.remove(modifier)
    bpy.context.view_layer.update()
    materials = {name: create_material(name, *values) for name, values in MATERIALS.items()}
    root = empty("E46_Race")
    root["attribution"] = "Adapted from BMW M3 E46 by BlenderCentral, CC-BY-SA 3.0"
    root["source_url"] = "https://blendswap.com/blend/10588"
    body = empty("Body", root)
    report = {"sourceSha256": SOURCE_HASH, "license": "CC-BY-SA-3.0", "parts": [],
              "sourceEvaluatedTriangles": 816368, "sourceMeasurement": "Inspected source viewport evaluation",
              "blenderForward": "-Y", "blenderUp": "+Z", "metresPerSourceUnit": SCALE}
    for source in body_sources:
        _, part = copy_part(source, materials, body)
        report["parts"].append(part)
    wheels = []
    for axle, source_y in (("F", FRONT_Y), ("R", REAR_PIVOT.y)):
        for side_name, sign in (("L", 1), ("R", -1)):
            pivot = Vector((.86 * sign, (source_y + .0101385) * SCALE, (REAR_PIVOT.z - GROUND) * SCALE))
            wheel = empty(f"Wheel_{axle}{side_name}", root, pivot)
            wheels.append(wheel)
            for source in wheel_sources:
                _, part = copy_part(source, materials, wheel, wheel=True, side=sign)
                report["parts"].append(part)
    empty("FrontMarker", root, (0, -2.5, .4))
    for obj in source_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    for text in list(bpy.data.texts):
        bpy.data.texts.remove(text)
    for image in list(bpy.data.images):
        bpy.data.images.remove(image)
    for collection in list(bpy.data.collections):
        if not collection.objects and not collection.children:
            bpy.data.collections.remove(collection)
    for layer in list(bpy.context.scene.view_layers):
        if layer != bpy.context.view_layer:
            bpy.context.scene.view_layers.remove(layer)
    bpy.context.view_layer.name = "Vehicle Review"
    create_paint_maps(materials)
    race_details(body, materials)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    report["triangles"] = sum(triangle_count(obj.data) for obj in meshes)
    report["wheelPivotsBlender"] = {wheel.name: list(wheel.location) for wheel in wheels}
    report["materials"] = {name: {"linearColor": values[0], "metallic": values[1],
                                    "smoothness": 1 - values[2]} for name, values in MATERIALS.items()}
    cameras = setup_preview(materials)
    # Purge unused source materials, textures, scenes and studio mesh datablocks.
    bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
    attribution = bpy.data.texts.new("ASSET_ATTRIBUTION")
    attribution.write("BMW M3 E46 by BlenderCentral, adapted for Project Alabama.\n"
                      "Source: https://blendswap.com/blend/10588\n"
                      "License: CC-BY-SA 3.0 https://creativecommons.org/licenses/by-sa/3.0/\n"
                      "Changes: mesh reduction, material replacement, race details, wheel hierarchy.\n")
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT / "E46_Race.blend"))
    export_groups(root, [body, *wheels])
    report_path = OUTPUT / "E46_Race.report.json"
    report_path.write_bytes((json.dumps(report, indent=2) + "\n").encode("utf-8"))
    contract = {"materials": [dict(name=name, **values,
                texture=name + ".png" if name in {"E46_Paint", "E46_TailLens"} else "")
                for name, values in report["materials"].items()]}
    (EXPORT / "E46_Materials.json").write_bytes((json.dumps(contract, indent=2) + "\n").encode("utf-8"))
    print(f"E46 adaptation: {report['triangles']} triangles")
    if args.render:
        for name, camera in cameras.items():
            bpy.context.scene.camera = camera
            bpy.context.scene.render.filepath = str(EVIDENCE / f"{name}.png")
            bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()

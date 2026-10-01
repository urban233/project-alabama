"""Original CC0 industrial street kit, in metres, with explicit FBX exports.

Blender 4.4: --background --factory-startup --disable-autoexec --python
tools/blender/build_industrial_kit.py. No downloaded scripts or images are used.
"""
import json
import math
from pathlib import Path
import random

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "source-art/environment/industrial-kit"
EXPORT = ROOT / "unity/Assets/Alabama/Art/Environment/IndustrialKit"
PALETTE = {
    "Kit_Concrete": ((.27, .255, .22, 1), 0, .88),
    "Kit_Brick": ((.20, .115, .075, 1), 0, .88),
    "Kit_BrickDark": ((.115, .075, .055, 1), 0, .92),
    "Kit_Steel": ((.08, .09, .095, 1), .5, .58),
    "Kit_Rust": ((.16, .09, .035, 1), .3, .76),
    "Kit_Window": ((.035, .065, .085, 1), .25, .36),
    "Kit_Roof": ((.09, .095, .09, 1), .25, .8),
    "Kit_Bark": ((.075, .047, .025, 1), 0, .92),
    "Kit_LeafOchre": ((.48, .22, .025, 1), 0, .94),
    "Kit_LeafOrange": ((.55, .13, .017, 1), 0, .94),
    "Kit_LeafOlive": ((.19, .17, .055, 1), 0, .96),
    "Kit_SignGreen": ((.024, .085, .052, 1), 0, .75),
    "Kit_Marking": ((.62, .49, .21, 1), 0, .87),
    "Kit_Black": ((.009, .011, .012, 1), 0, .86),
    "Kit_Lamp": ((.7, .58, .32, 1), 0, .3),
}
MATERIALS = {}
MODULES = []


def material(name, values):
    color, metallic, roughness = values
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    result.use_nodes = True
    bsdf = result.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return result


def start(name):
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    root["license"] = "CC0-1.0; original Project Alabama geometry"
    MODULES.append(root)
    return root


def cube(root, name, position, size, family, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.parent = root
    obj.data.materials.append(MATERIALS[family])
    if bevel:
        modifier = obj.modifiers.new("Edge highlights", "BEVEL")
        modifier.width, modifier.segments = bevel, 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def beam(root, name, a, b, thickness, family, sides=6):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=thickness / 2,
                                      depth=(b - a).length, location=(a + b) / 2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = (b - a).to_track_quat("Z", "Y").to_euler()
    obj.parent = root
    obj.data.materials.append(MATERIALS[family])
    return obj


def warehouse():
    root = start("WarehouseBay")
    cube(root, "Masonry", (0, 5.9, 4), (12, 12, 8), "Kit_Brick", .03)
    cube(root, "Plinth", (0, -.12, .6), (12.1, .18, 1.2), "Kit_Concrete")
    cube(root, "Lintel", (0, -.16, 5.3), (12.1, .16, .25), "Kit_Concrete")
    cube(root, "Parapet", (0, 5.9, 8.15), (12.3, 12.3, .3), "Kit_Concrete")
    for x in (-5.65, -.2, 5.65):
        cube(root, "Pilaster", (x, -.14, 4), (.26, .3, 8), "Kit_BrickDark")
    for x in (-4.0, -2.2, 1.9, 3.7):
        cube(root, "Window recess", (x, -.17, 6.4), (1.5, .10, 1.6), "Kit_Black")
        cube(root, "Factory window", (x, -.23, 6.4), (1.36, .06, 1.45), "Kit_Window")
        cube(root, "Mullion", (x, -.29, 6.4), (.05, .04, 1.5), "Kit_Steel")
        cube(root, "Transom", (x, -.29, 6.4), (1.4, .04, .05), "Kit_Steel")
    cube(root, "Roller door", (-3.0, -.22, 2.5), (3.8, .16, 4.3), "Kit_Roof")
    for z in [i * .27 + .4 for i in range(16)]:
        cube(root, "Shutter rib", (-3, -.32, z), (3.7, .04, .025), "Kit_Steel")
    cube(root, "Loading platform", (-3, -.9, .22), (4.2, 1.6, .44), "Kit_Concrete")
    cube(root, "Personnel door", (2.0, -.22, 1.25), (1.1, .16, 2.5), "Kit_Steel")
    for z in (1.6, 3.2, 4.8, 6.4):
        cube(root, "Side course", (6.03, 5.9, z), (.045, 12, .07), "Kit_BrickDark")
    cube(root, "Roof vent", (1.8, 3.2, 8.65), (2.2, 1.8, 1.0), "Kit_Roof")
    beam(root, "Downpipe", (5.1, -.31, .2), (5.1, -.31, 8.0), .12, "Kit_Rust")


def street_lamp():
    root = start("StreetLamp")
    cube(root, "Foot", (0, 0, .15), (.55, .55, .3), "Kit_Concrete", .02)
    beam(root, "Pole", (0, 0, .1), (0, 0, 8.4), .16, "Kit_Steel", 8)
    beam(root, "Arm", (0, 0, 8.4), (0, -2.2, 8.6), .12, "Kit_Steel")
    cube(root, "Housing", (0, -2.2, 8.57), (.42, .85, .16), "Kit_Steel", .03)
    cube(root, "Lens", (0, -2.2, 8.475), (.32, .72, .025), "Kit_Lamp")


def barrier():
    root = start("RoadBarrier")
    # Trapezoid cross-section with a real ground-level pivot.
    profile = [(-.38, 0), (.38, 0), (.23, .42), (.15, 1.0), (-.15, 1.0), (-.23, .42)]
    vertices = [(x, y, z) for y in (-2.9, 2.9) for x, z in profile]
    faces = [tuple(reversed(range(6))), tuple(range(6, 12))]
    faces += [(i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6) for i in range(6)]
    mesh(root, "Concrete barrier", vertices, faces, ["Kit_Concrete"])


def mesh(root, name, vertices, faces, families, indices=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    for family in families:
        data.materials.append(MATERIALS[family])
    if indices:
        for polygon, index in zip(data.polygons, indices):
            polygon.material_index = index
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = root
    return obj


def fence():
    root = start("FencePanel")
    for x in (-2.5, 2.5):
        beam(root, "Post", (x, 0, 0), (x, 0, 2.7), .09, "Kit_Steel")
    for z in (.25, 2.55):
        beam(root, "Rail", (-2.5, 0, z), (2.5, 0, z), .045, "Kit_Steel")
    # Open diamond wire mesh. Geometry remains visible without alpha sorting.
    for slope in (-1, 1):
        for offset in [i * .22 for i in range(-24, 25)]:
            x0, x1 = max(-2.5, -offset / slope), min(2.5, (2.5 - offset) / slope) if slope > 0 else 2.5
            if slope < 0:
                x0, x1 = max(-2.5, offset - 2.5), min(2.5, offset)
            if x1 > x0:
                beam(root, "Wire", (x0, 0, slope * x0 + offset + .05),
                     (x1, 0, slope * x1 + offset + .05), .012, "Kit_Steel", 3)


def tree(name, seed, height, spread):
    rng = random.Random(seed)
    root = start(name)
    beam(root, "Trunk", (0, 0, 0), (.12, .10, height * .78), .40, "Kit_Bark", 7)
    clusters = []
    for branch in range(11):
        angle = branch * 2.399 + rng.uniform(-.2, .2)
        reach = spread * rng.uniform(.58, 1)
        start_z = height * rng.uniform(.28, .55)
        tip = Vector((math.cos(angle) * reach, math.sin(angle) * reach, height * rng.uniform(.60, .94)))
        beam(root, "Branch", (.08, .05, start_z), tip, .14, "Kit_Bark", 5)
        clusters.append(tip)
    vertices, faces, indices = [], [], []
    # Irregular individual leaf cards, rather than spherical cartoon canopy blobs.
    for centre in clusters:
        for _ in range(105):
            point = centre + Vector((rng.gauss(0, spread * .27), rng.gauss(0, spread * .27), rng.gauss(0, height * .075)))
            theta, tilt = rng.uniform(0, math.tau), rng.uniform(-.8, .8)
            length, width = rng.uniform(.25, .48), rng.uniform(.15, .28)
            long = Vector((math.cos(theta), math.sin(theta), tilt)).normalized() * length
            short = Vector((-math.sin(theta), math.cos(theta), rng.uniform(-.5, .5))).normalized() * width
            base = len(vertices)
            vertices += [tuple(point - long), tuple(point + short), tuple(point + long), tuple(point - short)]
            faces += [(base, base + 1, base + 2, base + 3), (base + 3, base + 2, base + 1, base)]
            family = rng.choices(range(3), weights=(5, 4, 2))[0]
            indices += [family, family]
    mesh(root, "Autumn leaves", vertices, faces, ["Kit_LeafOchre", "Kit_LeafOrange", "Kit_LeafOlive"], indices)


def bridge():
    root = start("BridgeDeck")
    cube(root, "Deck", (0, 0, -.3), (10, 16, .6), "Kit_Concrete")
    for x in (-5.0, 5.0):
        cube(root, "Parapet", (x, 0, .45), (.35, 16, .9), "Kit_Concrete")
        for z in (.25, .75):
            beam(root, "Rail", (x, -8, z), (x, 8, z), .06, "Kit_Steel")
    root = start("BridgeColumn")
    cube(root, "Pier", (0, 0, 4), (1.5, 2.0, 8), "Kit_Concrete", .04)
    cube(root, "Crosshead", (0, 0, 7.8), (7, 2.2, .7), "Kit_Concrete", .03)


def crane():
    root = start("PortCrane")
    for x in (-3, 3):
        for y in (-3, 3):
            beam(root, "Tower leg", (x, y, 0), (x * .5, y * .5, 16), .42, "Kit_Steel")
    for z in (3, 7, 11, 15):
        for y in (-2.5, 2.5):
            beam(root, "Lattice diagonal", (-3, y, z - 3), (3, y, z + 1), .16, "Kit_Steel")
            beam(root, "Lattice brace", (-3, y, z), (3, y, z), .20, "Kit_Steel")
    cube(root, "Cabin", (0, 0, 16), (4.2, 3.6, 2.4), "Kit_Roof")
    cube(root, "Cab window", (0, -1.84, 16.2), (3.3, .08, .9), "Kit_Window")
    for x in (-.75, .75):
        beam(root, "Boom", (x, 0, 17), (x, -20, 23), .28, "Kit_Steel")
        beam(root, "Boom", (x, 0, 18.8), (x, -20, 24), .22, "Kit_Steel")
        for i in range(10):
            y = -i * 2
            beam(root, "Boom web", (x, y, 17 - y * .3), (x, y - 2, 18.8 - (y - 2) * .26), .11, "Kit_Steel")
    beam(root, "Hoist cable", (0, -18, 23), (0, -18, 11), .045, "Kit_Steel")
    cube(root, "Hook block", (0, -18, 10.6), (.5, .4, .8), "Kit_Rust")


def gantry():
    root = start("SignGantry")
    for x in (-7.6, 7.6):
        beam(root, "Column", (x, 0, 0), (x, 0, 6.8), .23, "Kit_Steel", 8)
    for z in (6.2, 6.9):
        beam(root, "Crossbeam", (-7.6, 0, z), (7.6, 0, z), .12, "Kit_Steel")
    for x in range(-7, 8):
        beam(root, "Gantry web", (x, 0, 6.2), (x + .7, 0, 6.9), .075, "Kit_Steel")


def chevron():
    root = start("ChevronPanel")
    cube(root, "Backing", (0, 0, 1.6), (2.4, .13, 1.2), "Kit_Black")
    for x in (-.75, .15):
        vertices = [(x - .10, -.075, 1.05), (x + .17, -.075, 1.05),
                    (x + .62, -.075, 1.6), (x + .17, -.075, 2.15),
                    (x - .10, -.075, 2.15), (x + .35, -.075, 1.6)]
        mesh(root, "Chevron", vertices, [tuple(range(6)), tuple(reversed(range(6)))], ["Kit_Marking"])
    for x in (-.85, .85):
        beam(root, "Signpost", (x, 0, 0), (x, 0, 1.2), .075, "Kit_Steel")


def billboard():
    root = start("Billboard")
    cube(root, "Panel", (0, 0, 5), (8, .22, 3.2), "Kit_Black")
    for x in (-2.5, 2.5):
        beam(root, "Support", (x, 0, 0), (x, 0, 5), .19, "Kit_Steel")
    cube(root, "Top cap", (0, 0, 6.64), (8.15, .30, .08), "Kit_Steel")


def main():
    SOURCE.mkdir(parents=True, exist_ok=True)
    EXPORT.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    MATERIALS.update({name: material(name, values) for name, values in PALETTE.items()})
    warehouse(); street_lamp(); barrier(); fence(); bridge(); crane(); gantry(); chevron(); billboard()
    tree("AutumnTreeA", 173, 9.0, 2.5)
    tree("AutumnTreeB", 291, 11.0, 2.8)
    tree("AutumnTreeC", 719, 8.0, 2.3)
    report = {"license": "CC0-1.0", "seededGeneration": True, "modules": [], "materials": []}
    for root in MODULES:
        parts = [obj for obj in root.children if obj.type == "MESH"]
        bpy.ops.object.select_all(action="DESELECT")
        for obj in parts:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = parts[0]
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = root.name + "_Mesh"
        obj.data.calc_loop_triangles()
        report["modules"].append({"name": root.name, "triangles": len(obj.data.loop_triangles)})
        marker = bpy.data.objects.new("FrontMarker", None)
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = root
        marker.location = (0, -1, 0)
        bpy.ops.object.select_all(action="DESELECT")
        for selected in (root, obj, marker):
            selected.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(EXPORT / (root.name + ".fbx")),
                                 use_selection=True, object_types={"MESH", "EMPTY"},
                                 axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_UNITS",
                                 bake_anim=False, add_leaf_bones=False, path_mode="STRIP")
    for name, (color, metallic, roughness) in PALETTE.items():
        report["materials"].append({"name": name, "linearColor": color, "metallic": metallic, "smoothness": 1 - roughness})
    (EXPORT / "IndustrialKit.json").write_bytes((json.dumps(report, indent=2) + "\n").encode("utf-8"))
    (SOURCE / "IndustrialKit.report.json").write_bytes((json.dumps(report, indent=2) + "\n").encode("utf-8"))
    # Arrange the editable library for a useful overview; exports keep local zero pivots.
    for i, root in enumerate(MODULES):
        root.location = ((i % 4) * 26, (i // 4) * 30, 0)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "IndustrialKit.blend"))
    print("Industrial kit:", len(MODULES), "modules;", sum(item["triangles"] for item in report["modules"]), "library triangles")


if __name__ == "__main__":
    main()

"""Create an original 1 m calibration mesh and export it explicitly for Unity."""

import argparse
import sys
from pathlib import Path

import bpy


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root = args.root.resolve(strict=True)
    source_dir = root / 'source-art' / 'calibration'
    export_dir = root / 'unity' / 'Assets' / 'Alabama' / 'Art' / 'Calibration'
    source_dir.mkdir(parents=True, exist_ok=True)
    export_dir.mkdir(parents=True, exist_ok=True)

    # Factory startup prevents local add-ons and user startup files influencing exports.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.5))
    block = bpy.context.object
    block.name = 'CalibrationBlock'
    block.data.name = 'CalibrationBlockMesh'
    block['purpose'] = 'Unit-scale and pivot validation; every dimension is one meter.'
    bpy.ops.wm.save_as_mainfile(filepath=str(source_dir / 'calibration.blend'))
    bpy.ops.export_scene.fbx(
        filepath=str(export_dir / 'calibration.fbx'),
        use_selection=True,
        object_types={'MESH'},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        bake_anim=False,
        add_leaf_bones=False,
        path_mode='AUTO',
    )
    print(f'Exported calibration source and mesh to {root}')


if __name__ == '__main__':
    main()

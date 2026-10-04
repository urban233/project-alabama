"""Record the installed replacement without attributing the old Blender source to it."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'source-art/vehicles/supplied-e46'
RUNTIME = ROOT / 'unity/Assets/Alabama/Art/Vehicles/E46'


def main():
    report = json.loads((SOURCE / 'adaptation-report.json').read_text())
    contract = json.loads((RUNTIME / 'E46_Materials.json').read_text())
    paths = [p for p in SOURCE.rglob('*') if p.is_file()
             and 'original' not in p.relative_to(SOURCE).parts
             and p.suffix not in ('.blend1', '.blend2')]
    paths += [RUNTIME / name for name in ('E46_Race.fbx', 'E46_Race.prefab', 'E46_Drive.prefab', 'E46_Materials.json')]
    for material in contract['materials']:
        paths.append(RUNTIME / (material['name'] + '.mat'))
        if material['texture']:
            paths.append(RUNTIME / material['texture'])
    files = []
    for path in sorted(set(paths)):
        if not path.is_file():
            raise FileNotFoundError(path)
        files.append({'path': path.relative_to(ROOT).as_posix(),
                      'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    replaced = {record['path'] for record in files}
    manifest = ROOT / 'docs/assets.json'
    data = json.loads(manifest.read_text())
    data['assets'] = [asset for asset in data['assets'] if asset['id'] != 'supplied-e46-player']
    for asset in data['assets']:
        asset['files'] = [record for record in asset['files'] if record['path'] not in replaced]
        if asset['id'] == 'e46-race-study':
            asset['purpose'] = 'Preserved earlier BlenderCentral source and unused materials; superseded as player car by supplied-e46-player.'
    data['assets'].append({
        'id': 'supplied-e46-player', 'title': 'Supplied E46 player car, balanced art direction',
        'creator': 'memoov; adaptation by Project Alabama contributors',
        'originalUrl': 'https://sketchfab.com/3d-models/bmw-m3-need-for-speed-most-wanted-b6a04764e3ea42cc9021327fdccd45ab',
        'license': 'CC-BY-4.0 (declaration embedded in supplied GLB)',
        'licenseEvidence': 'source-art/vehicles/supplied-e46/LICENSE.md',
        'generator': 'tools/blender/build_supplied_car.py', 'updatedOn': '2026-10-04',
        'sourceArchiveSha256': report['sourceZipSha256'], 'sourceArchiveRedistributed': False,
        'sourceTriangles': report['sourceTriangles'], 'triangles': report['triangles'],
        'bodyTriangles': report['bodyTriangles'], 'paintTriangles': report['paintTriangles'],
        'geometryRevision': report['geometryRevision'],
        'modifications': ['Strong chassis reduction with broad flat-shaded panels; silhouette and UVs retained',
                          'Selective rim, tyre, grille, lamp, trim and brake reduction',
                          'Metre scale and independent wheel pivots',
                          'Repaired textures and restrained URP materials',
                          'AI-assisted paint-atlas cleanup; source and prompt retained privately'],
        'files': files,
        'purpose': 'Replaces the current player BMW in existing prefab-linked scenes; artwork remains private.'
    })
    manifest.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    print(f'Recorded {len(files)} replacement asset files.')


if __name__ == '__main__':
    main()

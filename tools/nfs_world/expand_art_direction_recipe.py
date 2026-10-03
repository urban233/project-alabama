"""Extend reviewed representatives across the imported map's existing generic albedo bake."""
import json
import re
from pathlib import Path


def main():
    root = Path(__file__).resolve().parents[2]
    base = root / 'unity/Assets/Alabama/Art/Maps/NfsWorld'
    profile_path = root / 'docs/art-direction/downtown-material-recipe.json'
    profile = json.loads(profile_path.read_text())
    names = {t['sha256']: t['name'] for m in json.loads((root / 'artifacts/NfsWorld/inventory.json').read_text())['models']
             for t in m['textures']}
    manifest = json.loads((base / 'ArtPass/textures.json').read_text())
    report = json.loads((root / 'artifacts/NfsWorld/texture-bake.json').read_text())
    categories = {e['source']: set(e['categories']) for e in report['textures']}
    representatives = profile.get('representativeFamilies', profile['families'])
    selected = {source: family for family, sources in representatives.items() for source in sources}
    protected = re.compile(r'(?:^|_)(?:SGN|ADS|SFX|HUD|UI|DECAL|SIGN)(?:_|\d)|GRAF|LOGO|CHEVRON|WARNING|ARROWDOWN|ARROWUP', re.I)
    data_map = re.compile(r'_(?:N|NRM|NORMAL|SPEC|S)\.dds$', re.I)
    for entry in manifest['textures']:
        source = entry['source']
        name = names.get(source.removesuffix('.png'), '').upper()
        # Authored atlases have deliberate layouts. Only the already-reviewed
        # representatives can override them; the wider bake uses generic inputs.
        if source in selected or not entry['file'].startswith('Baked/') or protected.search(name) or data_map.search(name):
            continue
        kinds = categories.get(source, set())
        if '_OBJ_' not in name and any(t in name for t in ('WATER', 'OCEAN', 'RIVER')):
            family = 'water'
        elif 'Trees' in kinds and not any(t in name for t in ('TRUNK', 'BARK')):
            family = 'foliage'
        elif any(t in name for t in ('WINDOW', 'GLASS', 'FACADE')):
            family = 'facade'
        elif any(t in name for t in ('BRICK', 'MASONRY')):
            family = 'masonry'
        elif any(t in name for t in ('CONCRETE', 'PLASTER', 'STONE', 'WALL')):
            family = 'concrete'
        elif re.search(r'_(?:GRASS|DIRTPATH|GROUND|CLIFF|OILROCK|ROCK(?!ET))', name):
            family = 'ground'
        elif 'Roads' in kinds or any(t in name for t in ('PAVEMENT', 'ASPHALT', 'TARMAC')):
            family = 'road'
        elif 'Props' in kinds or any(t in name for t in ('METAL', 'STEEL', 'TRUNK', 'BARK')):
            family = 'props'
        else:
            family = 'surface'
        selected[source] = family
    profile['schemaVersion'] = 2
    profile['recipe'] = 'balanced-downtown-v2'
    profile['intent'] = 'District-wide moderate albedo detail across material families; original UV canvases and alpha. Keep authored atlases and protected lettering except the explicitly reviewed source-aligned representatives.'
    profile['families'] = {family: sorted(source for source, chosen in selected.items() if chosen == family)
                           for family in ['concrete', 'masonry', 'road', 'props', 'facade', 'foliage', 'ground', 'water', 'surface']}
    profile_path.write_text(json.dumps(profile, indent=2) + '\n')
    print({family: len(sources) for family, sources in profile['families'].items()})


if __name__ == '__main__':
    main()

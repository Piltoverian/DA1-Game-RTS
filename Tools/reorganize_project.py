"""One-time, GUID-preserving project reorganization. Run from the project root."""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil
import uuid

ROOT = Path(__file__).resolve().parent.parent
ARCHIVE = 'Archive/ProjectCleanup-2026-10-06'
MOVES = [
    ('Assets/Scripts/Game(New Simulation Logic)/NewDataRefactor/Editor', 'Assets/_Project/Scripts/Editor/GameData'),
    ('Assets/Scripts/Game(New Simulation Logic)/NewDataRefactor', 'Assets/_Project/Scripts/Runtime/GameData'),
    ('Assets/Scripts/Game(New Simulation Logic)/CommandSystem', 'Assets/_Project/Scripts/Runtime/Simulation/Commands'),
    ('Assets/Scripts/Game(New Simulation Logic)/CoreECS/Authoring/Authoring', 'Assets/_Project/Scripts/Runtime/Simulation/Authoring/Shared'),
    ('Assets/Scripts/Game(New Simulation Logic)/CoreECS/Authoring', 'Assets/_Project/Scripts/Runtime/Simulation/Authoring'),
    ('Assets/Scripts/Game(New Simulation Logic)/CoreECS/Physics', 'Assets/_Project/Scripts/Runtime/Simulation/Physics'),
    ('Assets/Scripts/Game(New Simulation Logic)/MapGen', 'Assets/_Project/Scripts/Runtime/Simulation/MapGeneration'),
    ('Assets/Scripts/Game(New Simulation Logic)/Movement', 'Assets/_Project/Scripts/Runtime/Simulation/Movement/Overrides'),
    ('Assets/Scripts/Game(New Simulation Logic)/MovementAgent', 'Assets/_Project/Scripts/Runtime/Simulation/Movement'),
    ('Assets/Scripts/Game(New Simulation Logic)/Misc', 'Assets/_Project/Scripts/Runtime/Simulation/Shared'),
    *[(f'Assets/Scripts/Game(New Simulation Logic)/System/{old}', f'Assets/_Project/Scripts/Runtime/Simulation/{new}') for old, new in [
        ('Combat', 'Combat'), ('Construction', 'Construction'), ('Economy', 'Economy'), ('PlayerContext', 'Players'),
        ('ResetSystem', 'Lifecycle'), ('ResourceSystem', 'Resources'), ('SelectSystem', 'Selection')]],
    ('Assets/Scripts/Events/EventChannels', 'Assets/_Project/Data/Events'),
    ('Assets/Scripts/Events', 'Assets/_Project/Scripts/Runtime/Events'),
    ('Assets/Scripts/EditorTools', 'Assets/_Project/Scripts/Editor/Art'),
    ('Assets/Editor', 'Assets/_Project/Scripts/Editor/Audits'),
    ('Assets/Scripts/ClientSide (Presentation)/InputReceiver/CameraScript.cs', 'Assets/_Project/Scripts/Runtime/Presentation/Camera/CameraScript.cs'),
    ('Assets/Scripts/ClientSide (Presentation)/InputReceiver/MinimapCam.cs', 'Assets/_Project/Scripts/Runtime/Presentation/Camera/MinimapCam.cs'),
    ('Assets/Scripts/ClientSide (Presentation)/InputReceiver', 'Assets/_Project/Scripts/Runtime/Presentation/Input'),
    ('Assets/Scripts/ClientSide (Presentation)/MonoBehaviours', 'Assets/_Project/Scripts/Runtime/Presentation/Components'),
    ('Assets/Scripts/ClientSide (Presentation)/UI', 'Assets/_Project/Scripts/Runtime/Presentation/UI'),
    ('Assets/Scripts/Faction.cs', 'Assets/_Project/Scripts/Runtime/GameData/Faction.cs'),
    ('Assets/Resources/IconMapping.cs', 'Assets/_Project/Scripts/Runtime/Presentation/UI/IconMapping.cs'),
    ('Assets/Resources/InfoIconMapping.cs', 'Assets/_Project/Scripts/Runtime/Presentation/UI/InfoIconMapping.cs'),
    ('Assets/Resources/pngtree-heart-icon-png-image_4421855.png', 'Assets/_Project/Art/UI/Icons/Heart.png'),
    ('Assets/Scripts/SO', 'Assets/_Project/Data/Definitions'),
    ('Assets/Temp/MapGenTest/Editor', 'Assets/_Project/Tests/Editor/MapGeneration'),
    ('Assets/Temp/MapGenTest', 'Assets/_Project/Tests/Fixtures/MapGeneration'),
    ('Assets/Resources', 'Assets/_Project/Resources'),
    ('Assets/Scenes', 'Assets/_Project/Scenes'),
    ('Assets/Prefab', 'Assets/_Project/Prefabs'),
    ('Assets/Art', 'Assets/_Project/Art/Design'),
    *[(f'Assets/{name}', f'Assets/_Project/Art/{name}') for name in ['Materials', 'Meshes', 'Models', 'Textures']],
    ('Assets/UIImageAsset', 'Assets/_Project/Art/UI/Sprites'),
    ('Assets/GeneratedRevealMaterials', 'Assets/_Project/Art/Materials/GeneratedReveal'),
    ('Assets/MapGen', 'Assets/_Project/Data/Maps'),
    ('Assets/Settings', 'Assets/_Project/Settings'),
    ('Assets/InputSystem_Actions.inputactions', 'Assets/_Project/Settings/Input/InputSystem_Actions.inputactions'),
    ('Assets/MiniMapTexture.renderTexture', 'Assets/_Project/Art/UI/MiniMapTexture.renderTexture'),
    ('Assets/NewMat.mat', 'Assets/_Project/Art/Materials/LegacySample.mat'),
    ('Assets/DefaultVolumeProfile.asset', 'Assets/_Project/Settings/Rendering/DefaultVolumeProfile.asset'),
    ('Assets/UniversalRenderPipelineGlobalSettings.asset', 'Assets/_Project/Settings/Rendering/UniversalRenderPipelineGlobalSettings.asset'),
    ('Assets/Scripts/MovementAgentDocs', 'Docs/Architecture/Movement'),
    ('Docs/DataRefactor', 'Docs/Guides/GameData'),
    ('Docs/Multiplayer/Labs', 'Docs/MapGeneration/Labs'),
    ('Docs/Multiplayer/Research', 'Docs/MapGeneration/Research'),
    ('Docs/Multiplayer/Archive', 'Docs/MapGeneration/Archive'),
    *[(p.as_posix(), 'Docs/MapGeneration/' + p.name) for p in sorted(Path('Docs/Multiplayer').glob('MapGen*.md'))],
    ('Docs/Multiplayer/PlayerBootstrap.md', 'Docs/MapGeneration/PlayerBootstrap.md'),
    ('Docs/Multiplayer/GridSnapper_ResourceSpawn.md', 'Docs/MapGeneration/GridSnapper_ResourceSpawn.md'),
    ('Assets/TutorialInfo', ARCHIVE + '/UnityTemplate/TutorialInfo'),
    ('Assets/Readme.asset', ARCHIVE + '/UnityTemplate/Readme.asset'),
    ('Assets/New Terrain.asset', ARCHIVE + '/UnusedCandidates/New Terrain.asset'),
    ('Assets/_Project/Settings/New Universal Render Pipeline Asset_Renderer.asset', ARCHIVE + '/UnusedCandidates/New Universal Render Pipeline Asset_Renderer.asset'),
    ('Assets/_Recovery', ARCHIVE + '/EmptyFolders/_Recovery'),
    ('DA1-Game-RTS.slnx', ARCHIVE + '/GeneratedSolutions/DA1-Game-RTS.slnx'),
    ('TechTextAndChosingForRTS sample.slnx', ARCHIVE + '/GeneratedSolutions/TechTextAndChosingForRTS sample.slnx'),
]

def target(path):
    # Sequential transforms also handle nested moves whose parent moves later.
    for old, new in MOVES:
        if path == old or path == old + '.meta' or path.startswith(old + '/'):
            path = new + path[len(old):]
    return path

def meta_guids():
    result = {}
    for p in ROOT.joinpath('Assets').rglob('*.meta'):
        match = re.search(r'^guid: (\w+)', p.read_text(encoding='utf-8-sig'), re.M)
        if match:
            result[p.relative_to(ROOT).as_posix()] = match[1]
    return result

def main():
    os.chdir(ROOT)
    if Path('Docs/Reviews/CleanupManifest_2026-10-06.json').exists():
        raise RuntimeError('Migration already ran; do not repeat.')
    before = meta_guids()
    originals = [p for base in ['Assets', 'Docs', 'ProjectSettings'] for p in Path(base).rglob('*') if p.is_file()]
    paths = {p.as_posix(): target(p.as_posix()) for p in originals}
    hashes = {p.as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in originals if p.suffix == '.cs'}
    performed = []
    for old, new in MOVES:
        src, dst = ROOT / old, ROOT / new
        assert src.resolve().is_relative_to(ROOT) and dst.resolve().is_relative_to(ROOT)
        if not src.exists():
            raise RuntimeError(f'Missing move source: {old}')
        dst.parent.mkdir(parents=True, exist_ok=True)
        if dst.exists():
            # Merge only disjoint directories; never overwrite files.
            if not src.is_dir() or not dst.is_dir():
                raise RuntimeError(f'Destination collision: {new}')
            for child in list(src.iterdir()):
                if (dst / child.name).exists():
                    raise RuntimeError(f'Merge collision: {child}')
                shutil.move(str(child), str(dst / child.name))
            src.rmdir()
            # A new parent has no meta yet; retain the original directory meta.
        else:
            shutil.move(str(src), str(dst))
        if Path(old + '.meta').exists():
            if Path(new + '.meta').exists():
                raise RuntimeError(f'Meta collision: {new}')
            shutil.move(old + '.meta', new + '.meta')
        performed.append({'from': old, 'to': new})

    # Empty historical containers retain their metadata in the archive.
    for p in sorted(Path('Assets/Scripts').rglob('*'), key=lambda p: len(p.parts), reverse=True) if Path('Assets/Scripts').exists() else []:
        if p.is_dir() and not any(p.iterdir()):
            p.rmdir()
            meta = Path(str(p) + '.meta')
            if meta.exists():
                dest = Path(ARCHIVE) / 'ContainerMeta' / meta
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(meta), str(dest))
                paths[meta.as_posix()] = dest.as_posix()
    for name in ['Assets/Scripts', 'Assets/Temp']:
        p = Path(name)
        if p.exists() and not any(p.iterdir()):
            p.rmdir()
            meta = Path(name + '.meta')
            if meta.exists():
                dest = Path(ARCHIVE) / 'ContainerMeta' / meta
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(meta), str(dest))
                paths[meta.as_posix()] = dest.as_posix()

    # Rebind relative Markdown links from their original location.
    changed = []
    for old, new in paths.items():
        p = Path(new)
        if not p.exists() or p.suffix.lower() not in ['.md', '.cs', '.asset', '.unity', '.json', '.ps1', '.py', '.cjs', '.js', '.html']:
            continue
        try:
            content = p.read_text(encoding='utf-8-sig')
        except UnicodeDecodeError:
            continue
        original = content
        if p.suffix == '.md':
            def link(m):
                raw = m.group(1)
                angled = raw.startswith('<') and raw.endswith('>')
                value = raw[1:-1] if angled else raw
                if value.startswith(('#', '/', 'http:', 'https:', 'app:', 'codex:')):
                    return m.group(0)
                name, sep, anchor = value.partition('#')
                resolved = Path(os.path.normpath(str(Path(old).parent / name))).as_posix()
                if resolved not in paths and not (ROOT / resolved).exists():
                    return m.group(0)
                dest = paths.get(resolved, target(resolved))
                rel = os.path.relpath(dest, p.parent).replace('\\', '/')
                value = rel + (sep + anchor if sep else '')
                return '](' + ('<' + value + '>' if angled or ' ' in value else value) + ')'
            content = re.sub(r'\]\((<[^>]+>|[^)]+)\)', link, content)
        # Explicit project paths in tools, docs, scene settings and serialized data.
        replacements = sorted([(a, b) for a, b in paths.items() if a != b], key=lambda pair: len(pair[0]), reverse=True)
        # Prefixes cover directory paths as well as file references.
        replacements += sorted([(a, target(a)) for a, b in MOVES if a.startswith(('Assets/', 'Docs/'))], key=lambda pair: len(pair[0]), reverse=True)
        for a, b in replacements:
            content = content.replace(a, b)
        if content != original:
            p.write_text(content, encoding='utf-8', newline='\n')
            changed.append(new)

    # Generate meta only for newly introduced Assets directories, never existing files.
    for p in sorted(Path('Assets/_Project').rglob('*')) + [Path('Assets/_Project')]:
        if p.is_dir() and not Path(str(p) + '.meta').exists():
            Path(str(p) + '.meta').write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
    for old, guid in before.items():
        dest = paths.get(old, target(old))
        p = Path(dest)
        if not p.exists() or f'guid: {guid}' not in p.read_text(encoding='utf-8-sig'):
            raise RuntimeError(f'GUID not preserved: {old} -> {dest}')
    code_changed = [paths[old] for old, digest in hashes.items() if hashlib.sha256(Path(paths[old]).read_bytes()).hexdigest() != digest]
    report = {'moves': performed, 'file_paths': paths, 'preserved_meta_count': len(before), 'updated_text_files': changed, 'changed_cs_files': code_changed}
    Path('Docs/Reviews/CleanupManifest_2026-10-06.json').write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps({'moves': len(performed), 'preserved_meta': len(before), 'updated_text_files': len(changed), 'changed_cs_files': code_changed}, ensure_ascii=False))

if __name__ == '__main__':
    main()

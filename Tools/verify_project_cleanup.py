"""Verify relocation and refresh local generated csproj paths for compilation."""
from pathlib import Path
import json
import os
import re
import shutil
import uuid

root = Path(__file__).resolve().parent.parent
os.chdir(root)
manifest_path = Path('Docs/Reviews/CleanupManifest_2026-10-06.json')
data = json.loads(manifest_path.read_text(encoding='utf-8'))

# This component has editor helpers but is a runtime MonoBehaviour.
old = Path('Assets/_Project/Scripts/Editor/Art')
new = Path('Assets/_Project/Scripts/Runtime/Presentation/Art')
if old.exists():
    shutil.move(str(old), str(new))
    shutil.move(str(old) + '.meta', str(new) + '.meta')
    for original, destination in data['file_paths'].items():
        if destination.startswith(old.as_posix()):
            data['file_paths'][original] = new.as_posix() + destination[len(old.as_posix()):]
    for key in ['updated_text_files', 'changed_cs_files']:
        data[key] = [p.replace(old.as_posix(), new.as_posix()) for p in data[key]]
    data['moves'].append({'from': old.as_posix(), 'to': new.as_posix()})
    manifest_path.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding='utf-8')

paths = data['file_paths']
for name in ['Assembly-CSharp.csproj', 'Assembly-CSharp-Editor.csproj']:
    p = Path(name)
    editor = 'Editor' in name
    content = p.read_text(encoding='utf-8-sig')
    def compile_path(m):
        original = m.group(1).replace('\\', '/')
        dest = paths.get(original, original)
        if not Path(dest).exists() or dest.startswith(('Archive/', 'Docs/')):
            return ''
        is_editor = 'Editor' in Path(dest).parts
        if dest.startswith('Assets/_Project/') and is_editor != editor:
            return ''
        return '<Compile Include="' + dest.replace('/', '\\') + '" />'
    content = re.sub(r'<Compile Include="([^"]+)"\s*/>', compile_path, content)
    current = {m.replace('\\', '/') for m in re.findall(r'<Compile Include="([^"]+)"', content)}
    missing = [p for p in Path('Assets/_Project').rglob('*.cs') if ('Editor' in p.parts) == editor and p.as_posix() not in current]
    if missing:
        additions = '\n'.join('    <Compile Include="' + str(p).replace('/', '\\') + '" />' for p in missing)
        content = content.replace('</Project>', '  <ItemGroup>\n' + additions + '\n  </ItemGroup>\n</Project>')
    p.write_text(content, encoding='utf-8')

# Newly introduced directories outside the migration also need folder metadata.
for p in Path('Assets/_Project').rglob('*'):
    if p.is_dir() and not Path(str(p) + '.meta').exists():
        Path(str(p) + '.meta').write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')

missing = [new for old, new in paths.items() if not Path(new).exists()]
duplicates = {}
for p in Path('Assets').rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', p.read_text(encoding='utf-8-sig'), re.M)
    if match:
        duplicates.setdefault(match[1], []).append(p.as_posix())
duplicate_groups = [p for p in duplicates.values() if len(p) > 1]
print(json.dumps({'missing_moved_files': missing, 'duplicate_guids': duplicate_groups}, ensure_ascii=False))
if missing or duplicate_groups:
    raise SystemExit(1)

bad_links = []
for p in [Path('README.md'), *Path('Docs').rglob('*.md'), *Path('Assets/_Project').rglob('*.md')]:
    for match in re.finditer(r'\]\((<[^>]+>|[^)]+)\)', p.read_text(encoding='utf-8-sig')):
        value = match[1].strip('<>').split('#')[0]
        if value and not value.startswith(('http:', 'https:', '/', 'app:', 'codex:')) and not (p.parent / value).exists():
            bad_links.append({'file': p.as_posix(), 'target': value})
Path('Docs/Reviews/CleanupLinkCheck_2026-10-06.json').write_text(json.dumps(bad_links, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps({'unresolved_relative_links': bad_links}, ensure_ascii=False))

"""Update documentation after the user-requested deletion of MapGen labs/history."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parent.parent
removed = [
    'Docs/MapGeneration/Labs', 'Docs/MapGeneration/Archive', 'Docs/MapGeneration/Research',
    'Docs/MapGeneration/MapGen_Lab5_AcceptedAlgorithm.md',
    'Docs/MapGeneration/MapGen_Lab6_Unity_PortPlan.md',
    'Docs/MapGeneration/MapGen_Session_Handoff_2026-10-04.md',
]
markers = ['Labs/', 'Archive/', 'Research/', 'MapGen_Lab5_AcceptedAlgorithm.md', 'MapGen_Lab6_Unity_PortPlan.md', 'MapGen_Session_Handoff_2026-10-04.md']
for p in (root / 'Docs/MapGeneration').glob('*.md'):
    text = p.read_text(encoding='utf-8-sig')
    # Historical analytic experiment and its results no longer ship.
    if p.name == 'MapGen_CompetitiveTerrain_Design.md':
        text = text.split('## 11. Kiểm chứng đã làm và những gì chưa làm')[0].rstrip() + '\n'
    lines = []
    for line in text.splitlines():
        if any(marker in line for marker in markers):
            if line.startswith('> **Cập nhật 2026-10-03:**'):
                lines.append('> **Cập nhật 2026-10-06:** MapGen Unity đã chạy ổn theo xác nhận của chủ project. Lab/archive đã xóa. Tra cứu thuật toán hiện hành tại [MapGen_Unity_Rules](MapGen_Unity_Rules.md) và [mục lục](MapGen_INDEX.md); các đề xuất cũ bên dưới không thay thế code hiện tại.')
            continue
        if 'Các Phòng thí nghiệm tương tác (HTML Labs)' in line or 'File sẽ tạo & Tài liệu Giáo trình / Labs đi kèm' in line:
            if line.startswith('##'):
                lines.append('## 3. File dự kiến trong kế hoạch ban đầu')
            continue
        lines.append(line)
    text = '\n'.join(lines) + '\n'
    text = re.sub(r'\n{4,}', '\n\n\n', text)
    p.write_text(text, encoding='utf-8')

p = root / 'Docs/Multiplayer/README.md'
text = p.read_text(encoding='utf-8-sig')
text = '\n'.join(line for line in text.splitlines() if 'MapGen_Lab5_AcceptedAlgorithm.md' not in line)
text = text.replace('Các lab HTML và kế hoạch port cũ phục vụ tham khảo lịch sử. Scene preview và các file test đã được dọn khỏi project.', 'Lab và archive MapGen đã được xóa theo yêu cầu chủ project. Tra cứu bản Unity hiện hành qua mục lục và hướng dẫn gameplay ở trên.')
p.write_text(text + '\n', encoding='utf-8')

p = root / 'Docs/ProjectStructure.md'
text = p.read_text(encoding='utf-8')
text = text.replace('Lab/research/history MapGen ở Docs/MapGeneration.', 'Docs/MapGeneration giữ tài liệu Unity/config hiện hành và các kế hoạch còn lại; lab, nghiên cứu thử và archive MapGen đã được xóa.')
p.write_text(text, encoding='utf-8')
p = root / 'README.md'
text = p.read_text(encoding='utf-8')
text = text.replace('Kế hoạch multiplayer, kế hoạch port và lab là thiết kế/tham khảo;', 'Kế hoạch multiplayer và tài liệu kế hoạch cũ là thiết kế/tham khảo;')
p.write_text(text, encoding='utf-8')

manifest = root / 'Docs/Reviews/CleanupManifest_2026-10-06.json'
data = json.loads(manifest.read_text(encoding='utf-8'))
def deleted(path):
    return any(path == item or path.startswith(item + '/') for item in removed)
pruned = {old: new for old, new in data['file_paths'].items() if deleted(new)}
data.setdefault('user_requested_deletions', {}).update(pruned)
data['file_paths'] = {old: new for old, new in data['file_paths'].items() if not deleted(new)}
for key in ['updated_text_files', 'changed_cs_files']:
    data[key] = [p for p in data[key] if not deleted(p)]
manifest.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding='utf-8')

p = root / 'Docs/Reviews/ProjectCleanup_2026-10-06.md'
text = p.read_text(encoding='utf-8')
text += '\n## Xóa lịch sử MapGen theo yêu cầu chủ project\n\nĐã xóa Labs, Archive, Research trong Docs/MapGeneration cùng bản chốt Lab5, kế hoạch port Lab6 và handoff 04/10. Tổng 77 file, khoảng 6,27 MiB. Các link tới nội dung đã xóa được bỏ/cập nhật về rule Unity hiện hành. Code, prefab, theme và fixture MapGen trong Assets không thay đổi. Archive/ProjectCleanup-2026-10-06 của đợt dọn chung vẫn được giữ.\n'
p.write_text(text, encoding='utf-8')

from pathlib import Path
import re

root = Path(__file__).resolve().parent.parent
updates = {
    'Docs/Multiplayer/Sprint0_CodeAudit.md': [('Assets/Scripts/Game(New Simulation Logic)/System/', 'Assets/_Project/Scripts/Runtime/Simulation/')],
    'Docs/MapGeneration/MapGen_ImplementationPlan.md': [('Assets/Scripts/Game(New Simulation Logic)/', 'Assets/_Project/Scripts/Runtime/Simulation/')],
    'Docs/MapGeneration/MapGen_ResourceSpawn_Config.md': [('Code gameplay dùng chung vẫn ở Assets/Scripts, docs vẫn ở Docs/Multiplayer', 'Code gameplay dùng chung ở Assets/_Project/Scripts, docs MapGen ở Docs/MapGeneration')],
    'Docs/MapGeneration/Archive/2026-10-04/Labs/Lab5_README_history.md': [
        ('](Lab6_README.md)', '](../../../Labs/Lab6/Lab6_README.md)'),
        ('](Lab6_TileMap_Cliff_Ramp.html)', '](../../../Labs/Lab6/Lab6_TileMap_Cliff_Ramp.html)'),
        ('](../MapGen_Lab5_AcceptedAlgorithm.md)', '](../../../MapGen_Lab5_AcceptedAlgorithm.md)')],
}
for name, pairs in updates.items():
    p = root / name
    text = p.read_text(encoding='utf-8-sig')
    for old, new in pairs:
        text = text.replace(old, new)
    p.write_text(text, encoding='utf-8')
for name in ['DesignerSetup.md', 'DesignerTypeManual.md']:
    p = root / 'Docs/Guides/GameData' / name
    text = p.read_text(encoding='utf-8-sig')
    text = re.sub(r'\[([^\]]+)\]\((?:R2/[^)]+|SessionHandoff.md|DevelopmentPlan.md)\)', r'`\1` (tài liệu cũ chưa có trong checkout)', text)
    p.write_text(text, encoding='utf-8')
p = root / 'Docs/Reviews/README.md'
text = p.read_text(encoding='utf-8-sig')
text = text.replace('- [Review tĩnh', '- [Dọn cấu trúc project 06/10/2026](ProjectCleanup_2026-10-06.md): sơ đồ di chuyển, archive và kết quả kiểm tra.\n- [Review tĩnh', 1)
p.write_text(text, encoding='utf-8')
p = root / 'Docs/Reviews/ProjectCleanup_2026-10-06.md'
text = p.read_text(encoding='utf-8')
text += '\nKết quả biên dịch: Runtime thành công với 5 warning code cũ; lần chạy đồng thời có thêm cảnh báo/tranh chấp file output. Đã chạy lại Editor tuần tự (bao gồm Runtime dependency), thành công 0 error. Build tuần tự sau đó là incremental nên 0 warning không có nghĩa các warning code cũ đã được sửa.\n\nLiên kết tương đối đã được kiểm tra; các link lịch sử tới R2/SessionHandoff/DevelopmentPlan vốn thiếu file được ghi rõ là tài liệu chưa có trong checkout.\n'
p.write_text(text, encoding='utf-8')

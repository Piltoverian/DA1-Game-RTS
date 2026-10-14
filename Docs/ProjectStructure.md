# Cấu trúc dự án

Source Unity do dự án quản lý nằm trong Assets/_Project. Bộ tài liệu chính duy nhất nằm trong Docs.

```text
Assets/_Project/
  Scripts/
    Runtime/
      Simulation/    (map, movement, combat, construction, economy)
      Presentation/  (input, camera, terrain, UI)
      GameData/      (definitions, baking, validation)
      Events/
    Editor/
      GameData/      (registry, ID, validation cho designer)
      Terrain/       (import, geometry baker, atlas preview)
  Tests/Fixtures/    (asset gameplay đang được Main/registry dùng)
  Data/, Resources/, Scenes/, Prefabs/, Art/, Settings/
ArtSource/Terrain/JadeSlate/  (nguồn reference atlas duy nhất)
Docs/                       (hướng dẫn, kiến trúc, trạng thái, lịch sử)
Artifacts/                  (log/receipt kiểm chứng)
```

Không còn Tests/Editor hoặc runner Tools. Folder Fixtures còn tên cũ vì giữ đường dẫn/GUID của asset gameplay đang dùng. Công cụ import/design ở Scripts/Editor, không biên dịch vào player. Validator runtime vẫn nằm trong GameData/Validation và Simulation.

Main hiện tham chiếu MoonTerrain/LunarTheme.asset. JadeSlate là nguồn reference thiết kế; JadeSlateAtlas là đầu ra của tool tạo ví dụ. Không đổi asset scene trong lượt dọn.

[Chọn chủ đề](README.md) · [Trạng thái](Maintenance/CurrentState.md) · [Import atlas](TerrainVisualizer/14_SuDung_Atlas_Implementation.md).

Library, Logs, obj, bin, .vs và csproj Unity là dữ liệu generated/cache; không xóa cache đang dùng để dọn source. Thay đổi chưa commit chỉ tồn tại trong working tree; không reset diff chưa hiểu.

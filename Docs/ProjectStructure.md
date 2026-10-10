# Cấu trúc project

Nội dung do project quản lý nằm trong `Assets/_Project`. Nội dung Unity/vendor giữ ngoài thư mục này.

```text
Assets/_Project/
├── Scripts/
│   ├── Runtime/
│   │   ├── Simulation/
│   │   │   ├── Authoring, Commands, Players
│   │   │   ├── Combat, Construction, Economy, Resources
│   │   │   ├── Movement, MapGeneration
│   │   │   └── Physics, Selection, Lifecycle, Shared
│   │   ├── Presentation/ (Input, Camera, UI, Components, Art)
│   │   ├── GameData/
│   │   └── Events/
│   └── Editor/ (Audits, GameData)
├── Data/ (Definitions, Events, Maps)
├── Resources/
├── Scenes/
├── Prefabs/
├── Art/ (Design, Materials, Meshes, Models, Textures, UI)
├── Settings/
└── Tests/
    ├── Editor/MapGeneration/
    └── Fixtures/MapGeneration/
```

Movement/Overrides chứa cầu nối di chuyển cũ. GameData giữ định nghĩa, baking, component và validation. Chưa đổi class/namespace hoặc chia assembly.

RevealMaterialAutoSetup là MonoBehaviour có helper Editor nên vẫn ở Runtime/Presentation/Art. Resources giữ tên/key đang được Resources.Load sử dụng.

Scene cũ được giữ để đối chiếu. Build Settings giữ scene đã chọn, chỉ cập nhật đường dẫn.

## Điểm vào tài liệu

- [Portal designer](README.md).
- [Dữ liệu và authoring](Guides/GameData/README.md).
- [MapGeneration hiện hành](MapGeneration/MapGen_INDEX.md).
- [Movement](Architecture/Movement/MovementAgent/Architecture_Overview.md).
- [Multiplayer roadmap](Multiplayer/README.md).
- [Review và kiểm tra](Reviews/README.md).

Docs/MapGeneration giữ topology, Unity/config, spawn và resource. [Docs/TerrainVisualizer](TerrainVisualizer/README.md) là bộ nghiên cứu độc lập cho projection, artwork/atlas, renderer, picking/ECS và lab. History chỉ giữ redirect; archive nội dung lỗi thời đã được loại khỏi repository. Theme hiện hành là JadeSlateTerrainTheme; các harness tự chạy dùng một lần đã được xóa. Tài liệu movement đã rời Assets; meta cũ giữ để đối chiếu lịch sử.

## Dọn và khôi phục

[Quy trình Unity hiện hành](../Tools/README.md) dùng importer và validator Editor. Python, helper đóng gói và MarkdownReader WPF đã được loại bỏ.

[Báo cáo dọn project](Reviews/ProjectCleanup_2026-10-06.md) là ghi chép lịch sử. Archive template/meta cũ đã được loại khỏi repository; cache đang dùng bởi Unity vẫn giữ cục bộ.

Khi thêm nội dung, chọn nhóm theo trách nhiệm; tránh tên Temp/New/Refactor hoặc đặt asset trong Scripts. Khi di chuyển asset, giữ meta/GUID và kiểm tra serialized reference cùng đường tra cứu runtime.

## Terrain chính thức (10/10/2026)

- Gen/schema/theme/entry renderer: Scripts/Runtime/Simulation/MapGeneration.
- Render chunk, visual surface và ECS offset/restore: Scripts/Runtime/Presentation/Terrain.
- Hợp đồng core: 16 PNG top/ramp và 10 PNG cliff. Theme polished thêm 32 ground, 16 mountain cap và 4 decor; bộ Reference giữ vai trò fixture. Stock URP Unlit; không còn custom terrain shader.
- Regression test: CanonicalTerrainValidation, TerrainPresentationValidation và StockIsometricSpriteValidation trong Tests/Editor/MapGeneration.
- Terrain hiện hành dùng sprite isometric trên grid phẳng; xem [kiến trúc Terrain Visualizer](TerrainVisualizer/01_PhamVi_KienTruc.md). Việc tách docs không đổi đường dẫn các lớp runtime.

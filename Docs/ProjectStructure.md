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

Docs/MapGeneration giữ tài liệu Unity/config hiện hành và các kế hoạch còn lại; lab, nghiên cứu thử và archive MapGen đã được xóa. Tài liệu movement đã rời Assets; meta cũ giữ để đối chiếu lịch sử.

## Dọn và khôi phục

[Báo cáo dọn project](Reviews/ProjectCleanup_2026-10-06.md) liệt kê các thay đổi. Archive/ProjectCleanup-2026-10-06 giữ template, ứng viên thừa và meta container rỗng. Không xóa cache đang dùng bởi Unity.

Khi thêm nội dung, chọn nhóm theo trách nhiệm; tránh tên Temp/New/Refactor hoặc đặt asset trong Scripts. Khi di chuyển asset, giữ meta/GUID và kiểm tra serialized reference cùng đường tra cứu runtime.

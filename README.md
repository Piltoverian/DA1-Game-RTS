# RTS Unity project

Unity **6000.3.13f1**. Mở thư mục project bằng Unity Hub; dependencies nằm trong `Packages/manifest.json` và `Packages/packages-lock.json`.

Terrain Main hiện dùng **JadeSlateTerrainTheme**, từ vật liệu ImageGen + UV canonical. [Pipeline và bộ art hiện hành](Docs/TerrainVisualizer/05_Atlas_ArtPipeline.md), [kiểm tra và kết quả dọn dẹp](Docs/TerrainVisualizer/10_JadeSlate_Cleanup.md). Archive phục hồi và công cụ Python đã được loại khỏi repository. Giữ source Unity, artwork hiện hành và tài liệu Markdown/HTML.

Theo lần kiểm tra của chủ project, gameplay chạy được tới tạo map. Input/UI/selection từng hoạt động ở phiên bản trước nhưng chưa được test lại gần đây. Các nhận xét review tĩnh về những phần này cần kiểm chứng trên phiên bản hiện tại trước khi kết luận lỗi runtime.

## Điểm bắt đầu

- [Tài liệu và sơ đồ project](Docs/ProjectStructure.md).
- [Hướng dẫn chạy Main](Docs/MapGeneration/MapGen_Unity_ReadingGuide.md).
- [MapGen hiện hành](Docs/MapGeneration/MapGen_INDEX.md).
- [Terrain Visualizer — bộ nghiên cứu riêng](Docs/TerrainVisualizer/README.md).
- [Cấu hình dữ liệu cho designer](Docs/Guides/GameData/README.md).
- [Review và lịch sử kiểm tra](Docs/Reviews/README.md).

## Quy ước quản lý

`Assets`, `Packages`, `ProjectSettings` là nội dung nguồn của Unity. Giữ `.meta` đi cùng asset khi đổi tên hoặc di chuyển. `Library`, `Temp` ở root, `obj`, `Logs`, solution và csproj là dữ liệu cục bộ/sinh tự động.

`Assets/_Project/Tests/Fixtures/MapGeneration` chứa fixture có reference từ scene/registry, được giữ trong Git. Thư mục này khác với `Temp` ở root. Không dọn cache theo tên thư mục mà bỏ qua reference.

Tài liệu mô tả code hiện hành là điểm tra cứu chính. Kế hoạch multiplayer và tài liệu kế hoạch cũ là thiết kế/tham khảo; không mặc định là chức năng đã triển khai hoặc đã nghiệm thu.

# RTS Unity project

Unity **6000.3.13f1**. Mở thư mục project bằng Unity Hub; dependencies nằm trong `Packages/manifest.json` và `Packages/packages-lock.json`.

Theo lần kiểm tra của chủ project, gameplay chạy được tới tạo map. Input/UI/selection từng hoạt động ở phiên bản trước nhưng chưa được test lại gần đây. Các nhận xét review tĩnh về những phần này cần kiểm chứng trên phiên bản hiện tại trước khi kết luận lỗi runtime.

## Điểm bắt đầu

- [Tài liệu và sơ đồ project](Docs/ProjectStructure.md).
- [Hướng dẫn chạy Main](Docs/Multiplayer/MapGen_Unity_ReadingGuide.md).
- [MapGen hiện hành](Docs/Multiplayer/MapGen_INDEX.md).
- [Cấu hình dữ liệu cho designer](Docs/DataRefactor/README.md).
- [Review và lịch sử kiểm tra](Docs/Reviews/README.md).

## Quy ước quản lý

`Assets`, `Packages`, `ProjectSettings` là nội dung nguồn của Unity. Giữ `.meta` đi cùng asset khi đổi tên hoặc di chuyển. `Library`, `Temp` ở root, `obj`, `Logs`, solution và csproj là dữ liệu cục bộ/sinh tự động.

`Assets/Temp/MapGenTest` chứa fixture có reference từ scene/registry, được giữ trong Git. Thư mục này khác với `Temp` ở root. Không dọn cache theo tên thư mục mà bỏ qua reference.

Tài liệu mô tả code hiện hành là điểm tra cứu chính. Kế hoạch multiplayer, kế hoạch port và lab là thiết kế/tham khảo; không mặc định là chức năng đã triển khai hoặc đã nghiệm thu.

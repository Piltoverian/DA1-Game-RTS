# Dự án RTS trên Unity

Dự án game chiến thuật thời gian thực, xây dựng bằng Unity và kiến trúc DOTS/ECS. Người chơi điều khiển quân, khai thác tài nguyên, xây dựng công trình, huấn luyện đơn vị và nghiên cứu công nghệ trên bản đồ có địa hình nhiều tầng.

Định hướng mỹ thuật là tu tiên với góc nhìn đẳng trục: nền đất, vách, dốc và dãy núi được dựng từ atlas vật liệu. Logic di chuyển và tương tác chạy trên mặt phẳng XZ; độ cao phục vụ hiển thị và vị trí đối tượng trên địa hình.

## Các phần chính

- **Bản đồ và địa hình:** sinh bản đồ theo cấu hình, tạo vùng khởi đầu, bố trí tài nguyên và dựng hình địa hình.
- **Điều khiển quân:** nhận lệnh, tìm đường bằng trường hướng dùng chung, tránh vật cản và xử lý thay đổi lối đi.
- **Kinh tế và xây dựng:** khai thác, vận chuyển, thanh toán tài nguyên, đặt nhà và tiến độ xây dựng.
- **Dữ liệu gameplay:** cấu hình đơn vị, công trình, khả năng và công nghệ bằng ScriptableObject; chuyển dữ liệu sang BlobAsset để dùng trong ECS.
- **Định hướng nhiều người chơi:** máy chủ quyết định trạng thái, máy khách gửi lệnh và nội suy hình ảnh. Phần mạng đang trong lộ trình phát triển.

## Công nghệ

| Thành phần | Phiên bản của dự án |
|---|---|
| Unity Editor | 6000.3.13f1 |
| Entities / Unity Physics | 1.4.5 |
| Entities Graphics | 1.4.18 |
| Burst | 1.8.30 |
| Universal Render Pipeline | 17.3.0 |

Danh sách phụ thuộc đầy đủ nằm trong [Packages/manifest.json](Packages/manifest.json). Unity quản lý các phiên bản đã khóa qua `Packages/packages-lock.json`.

## Mở và chạy

1. Cài **Unity 6000.3.13f1** bằng Unity Hub.
2. Trong Unity Hub, thêm thư mục dự án chứa `Assets`, `Packages` và `ProjectSettings`.
3. Mở dự án, chờ Unity khôi phục package và biên dịch.
4. Mở scene [Main](Assets/_Project/Scenes/Main.unity), rồi bấm **Play**.

Cấu hình bản đồ hiện dùng nằm ở `Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset`. Hướng dẫn cấu hình chi tiết có trong [tài liệu chạy Main](Docs/MapGeneration/MapGen_Unity_ReadingGuide.md).

## Cấu trúc thư mục

| Thư mục | Nội dung |
|---|---|
| `Assets/_Project/` | Mã nguồn, scene, prefab, dữ liệu và hình ảnh của game |
| `Packages/`, `ProjectSettings/` | Phụ thuộc và cấu hình Unity |
| `Docs/` | Tài liệu nội bộ về kiến trúc, sử dụng và phát triển |
| `ArtSource/` | Nguyên liệu mỹ thuật và mô tả tạo ảnh |
| `Tools/`, `Artifacts/` | Công cụ hỗ trợ và kết quả kiểm chứng |

Giữ file `.meta` đi cùng tài nguyên khi đổi tên hoặc di chuyển. `Library`, `Temp`, `obj`, solution và `.csproj` là dữ liệu Unity sinh tự động.

## Tài liệu nội bộ

Bắt đầu tại [Docs/README.md](Docs/README.md). Bộ tài liệu có hướng dẫn cho người thiết kế dữ liệu, giải thích hệ thống và các bản đọc nhanh bằng tiếng Việt.

- [Tổng quan ngắn](Docs/README.md)
- [Cấu trúc mã nguồn](Docs/ProjectStructure.md)
- [Trạng thái phát triển](Docs/Maintenance/CurrentState.md)
- [Lộ trình tiếp theo](Docs/Maintenance/Roadmap.md)

Dự án đang phát triển. Các mục tiêu tải lớn và phiên nhiều người chơi cần được nghiệm thu riêng; trạng thái chi tiết được theo dõi trong tài liệu nội bộ.

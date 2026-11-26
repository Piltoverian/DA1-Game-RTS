# Dự án RTS trên Unity

> Merge 26/11/2026: tích hợp `Fog-of-wars` (fog of war và multiplayer/NetCode) vào bản HUD/demo. Các kết quả Play Mode/build ngày 25–26/11 bên dưới thuộc trước merge; cần nghiệm thu lại gameplay sau tích hợp. AI và restart demo chỉ chạy trong world local.

Game chiến thuật thời gian thực dùng DOTS/ECS: điều khiển unit, khai thác, xây dựng, sản xuất và nghiên cứu trên terrain nhiều tầng với Fog of War.

**Bắt đầu tại [Docs/README.md](Docs/README.md)** — bộ tài liệu hỗ trợ code duy nhất.

- [Mở project, phiên bản và tìm source](Docs/ProjectStructure.md)
- [Trạng thái đã kiểm chứng và việc cần test tiếp](Docs/Maintenance/CurrentState.md)

Mở project qua Unity Hub bằng phiên bản trong [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt), chờ import/compile rồi mở [Main](Assets/_Project/Scenes/Main.unity). Cách chọn local/host/client nằm trong docs.

## Công nghệ

| Thành phần | Phiên bản của dự án |
|---|---|
| Unity Editor | 6000.3.13f1 |
| Entities / Unity Physics | 1.4.5 |
| Entities Graphics | 1.5.0 |
| Burst | 1.8.30 |
| Universal Render Pipeline | 17.3.0 |

Danh sách phụ thuộc đầy đủ nằm trong [Packages/manifest.json](Packages/manifest.json). Unity quản lý các phiên bản đã khóa qua `Packages/packages-lock.json`.

## Mở và chạy

1. Cài **Unity 6000.3.13f1** bằng Unity Hub.
2. Trong Unity Hub, thêm thư mục dự án chứa `Assets`, `Packages` và `ProjectSettings`.
3. Mở dự án, chờ Unity khôi phục package và biên dịch.
4. Mở scene [Main](Assets/_Project/Scenes/Main.unity), rồi bấm **Play**.

Cấu hình `Main` hiện dùng bản demo **1 người đấu 1 AI** ở
`Assets/_Project/Data/Playable/SkirmishMap.asset` và `SkirmishStart.asset`.
Map 128×128 ô trên mặt bằng 250 đơn vị; mỗi bên bắt đầu với TownHall và 5 worker,
350 Gold, 350 Wood, 250 Food. Các cấu hình thử nghiệm cũ vẫn được giữ làm tham khảo.

## Chơi một trận hoàn chỉnh

1. Chọn worker bằng click hoặc kéo khung; click phải mỏ để khai thác. Gold, Wood
   và Food đều có nguồn thật; worker tự mang về kho/TownHall hoàn tất của mình.
2. Dùng menu worker để xây Resource Depot, Barracks và Defense Tower. Chọn trại
   để tạo Soldier; badge và tiến độ hiển thị hàng chờ thực. Chừa lối ra cho lính.
3. Chọn quân, click phải mục tiêu địch để tấn công. Tháp tự bảo vệ trong tầm.
4. Phá TownHall địch để thắng; mất TownHall của mình thì thua. Chọn **Play again**
   hoặc **Menu → Restart match** để bắt đầu trận mới với world ECS được khởi tạo lại.

AI thu thập, xây trại và trả chi phí tạo quân bằng cùng pipeline gameplay. Đội
quân được giới hạn 8 lính; đợt tấn công đầu sau khoảng 100 giây khi có ít nhất
3 lính, các đợt sau cách nhau 35 giây. Đây là AI cơ bản cho bản demo, chưa có
các mức khó hoặc chiến thuật nâng cấp.

WASD/phím mũi tên di chuyển camera; giữ Shift để pan nhanh; con lăn zoom;
minimap cho phép click/kéo để đổi vùng nhìn; Escape mở menu. Simulation tiếp
tục chạy khi mở menu. Worker/lính dùng tốc độ 10 thay cho 7 của prototype.

Các công cụ kiểm chứng trong Unity ở **Tools → RTS UI**: chuẩn bị nội dung 1v1,
kiểm tra asset, chạy complete playable loop smoke và build Windows. Smoke chỉ
chạy khi gọi chủ động trên một trận sạch; nó phát lệnh gameplay bình thường,
không cộng tài nguyên, gây damage trực tiếp hoặc hoàn tất công trình tức thì.

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

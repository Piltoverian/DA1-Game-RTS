# Dải điều khiển chung — 05/11/2026

Chủ dự án đã xác nhận nghiệm thu khung này. Công việc tiếp theo chuyển sang
[công trình và tài nguyên](BuildingContentIntegration.md). Checklist và giới hạn
kiểm tra của lượt triển khai dưới đây được giữ làm lịch sử.

## Prompt triển khai

Trong Main, tổ chức HUD dưới thành một khung chung bốn vùng từ trái sang phải:
thông tin đối tượng đang chọn/xem; tác vụ khả dụng; công việc đang thực hiện và
hàng chờ; minimap. Tham khảo cách chia vùng của ảnh AOE I người dùng cung cấp,
giữ theme tu tiên sơn ngọc/viền đồng, uGUI/TMP, portrait, HP, nhóm và tooltip.
“Đang quét vào” tiếp tục dùng cơ chế chọn/xem mỏ hiện tại, không thêm hover chọn.

Đọc ProductionQueueElement, BuildingConstruction và BuilderQueueElement thực.
Mục giống nhau liền nhau gom thành ô icon có số ở góc; số tính cả công việc
hiện tại nếu thuộc nhóm đầu. Giữ thứ tự A,A,B,A thành A×2 / B / A.
Thanh tiến độ và ETA chỉ thuộc job hiện tại, không phải tổng nhóm. Không suy
đoán ETA xây từ một worker vì có thể nhiều worker cùng đóng góp.
Không sửa simulation, thanh toán, hủy/refund, ownership, selection hay lệnh
minimap. Kiểm tra biên dịch/prefab và ghi riêng phần chưa nghiệm thu Play Mode.

## Hạng mục ưu tiên

| Ưu tiên | Hạng mục | File liên quan | Phụ thuộc | Tiêu chí nghiệm thu | Rủi ro |
| --- | --- | --- | --- | --- | --- |
| P0 | Khung chung và minimap bên phải | UICanvas.prefab; HudCommandDock.cs | Canvas hiện có | Bốn vùng giữ thứ tự, không chồng lấn; không click xuyên khung | Cửa sổ nhỏ khó đọc |
| P0 | Ghép thông tin/lệnh hiện có | UnitInfoPanel.cs; CommandMenu.cs | Lựa chọn và offers thực | Portrait/HP/nhóm/lệnh/lý do khóa vẫn đúng; trạng thái trống rõ ràng | Chọn nhóm chỉ có thông tin đại diện |
| P0 | Icon hàng chờ và badge số lượng | ProductionQueuePanel.cs | ProductionQueueElement | Ba job liền nhau cùng loại hiện số 3; giảm khi xong; A,A,B,A không đảo thứ tự | Người chơi có thể hiểu nhầm số là đang chạy đồng thời |
| P0 | Xây dựng và hàng xây worker | ProductionQueuePanel.cs | BuildingConstruction; BuilderQueueElement | Đúng mục tiêu/tiến độ; nhà xong/bị phá rời danh sách | Không có sprite thì chỉ hiện tên |
| P1 | Kiểm tra co giãn và thao tác | Main; HudCommandDock.cs | Các P0 | 1080p/720p/4:3/ultrawide, UI 80–120%; tooltip/hitbox đọc và bấm được | Hình học tĩnh không đảm bảo font/render |
| P1 | Nghiệm thu và ảnh báo cáo | Main; UIReportReadiness.md | Chạy Unity với player/map | Không lỗi mới; chọn/ra lệnh/xây/menu/minimap đúng; có ảnh Game View | Chưa chạy Play Mode/build |

Các P0 đã triển khai, chờ nghiệm thu Unity. P1 đã kiểm tra hình học tĩnh;
nghiệm thu thực tế và ảnh báo cáo còn lại. Migrate tháp/kho/trại lính legacy
vẫn là hạng mục riêng; không coi chúng hoàn tất nhờ thay bố cục.

## Phạm vi triển khai

- Prefab: `Assets/_Project/Prefabs/UI prefab/UICanvas.prefab`. Bốn vùng cùng
  nằm trong root khung lựa chọn cũ; không đổi tên hoặc xóa nội dung.
- HudCommandDock chỉ quản lý bố cục, tiêu đề, phân vùng và nhãn không có lệnh.
  CommandMenu vẫn quản lý grid/dispatch; chế độ embedded dừng resize khung
  chung và vẽ nền riêng. Không thay kiến trúc gameplay.
- Khung cao tối thiểu 248 đơn vị Canvas; tăng khi lệnh cần thêm hàng. Thông tin
  rộng 280, tác vụ 312, minimap 220; queue dùng phần ngang còn lại. Dưới 1140
  đơn vị Canvas thì thu nhỏ toàn khung; cần nghiệm thu độ dễ đọc ở cửa sổ nhỏ.
- Tiêu đề vùng và work tile được tạo lúc chạy: Edit Mode không thể hiện đầy đủ.
- Tối đa sáu ô work được tái sử dụng. Khi vượt sáu nhóm, ô thứ sáu báo số nhóm
  còn lại; hover xem tối đa bốn nhóm kế tiếp và số nhóm chưa liệt kê. Queue thật
  không bị cắt hoặc đổi thứ tự.
- Tiến độ luôn của mục tiêu đầu; việc xây ưu tiên khi đối tượng đang xây.
  Không tổng hợp hàng chờ của cả nhóm chọn hoặc toàn bản đồ.
- Giữ sprite prototype; thiếu sprite vẫn có tên/số, không dùng ảnh giả.
- Minimap giữ grid/ECS, click/kéo camera; không thêm fog, ping, lệnh chiến đấu.

## Nghiệm thu trong Main

1. Play ở 1920×1080: một khung, thông tin → tác vụ → work/queue → minimap.
2. Chọn worker/quân/TownHall/nhóm/mỏ; ảnh và thông tin đúng, các vùng không xê dịch.
3. Xếp ba job giống nhau: một ô số 3; tiến độ/ETA chỉ job đầu; hoàn tất giảm số.
   Thử A,A,B,A khi có offers hợp lệ; chờ dân số/paused khi gameplay tạo được.
4. Chọn nhà đang xây, rồi worker có nhiều nhà xếp hàng; kiểm tra mục tiêu bị
   phá/nhà hoàn tất/đổi lựa chọn/mỏ không giữ nội dung cũ. Work tile chỉ đọc.
5. Click/kéo minimap phải: camera di chuyển, không đổi nhóm hoặc phát lệnh world.
6. Thử 1280×720, 1440×1080, 2560×1080 và UI 80/100/120%; mở menu,
   hover tooltip, kiểm tra chữ và vùng bấm ở mép màn hình.

Đã kiểm tra compiler C# runtime đi kèm Unity và tham chiếu local prefab.
Hình học bốn vùng và ô queue đạt 12 tổ hợp của bốn kích thước màn hình và
UI 80/100/120%; đây là tính toán bố cục, không phải kết quả render Unity.
Chưa chạy Play Mode, test tự động, player build hoặc đo FPS/GC cho lượt này.
Không có ảnh Game View mới làm bằng chứng nghiệm thu.

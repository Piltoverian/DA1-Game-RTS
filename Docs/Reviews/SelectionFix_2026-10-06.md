# Selection worker sau MapGen — 06/10/2026

Runtime test trong Main xác nhận bootstrap Ready, một LocalTestPlayer ID 1, 20 unit và worker được kiểm tra có EntityOwner/Selectable ID 1, Selected và DragSelectableEntity.

Trước sửa: SelectSystem nhận request click/drag và bật Selected thành công, nhưng SelectManager.TryGetPlayerId trả false. Resolver trả ngay từ nhánh TestPlayer MonoBehaviour trong SubScene và không fallback sang LocalTestPlayer đã bake. Đây là điểm chặn input dù player ECS tồn tại.

Sau sửa: ưu tiên LocalTestPlayer ECS; khi chưa có baked identity mới dùng TestPlayer MonoBehaviour. Từ chối nhiều local identity và kiểm tra context/bootstrap trước khi gửi lệnh.

Smoke test Play Mode ghi PASS: inputPlayerReady=True, clickSelected=True, dragSelected=True, inputClickSelected=True. Phần inputClick gọi SingleSelecting với vị trí màn hình suy ra từ worker; chưa mô phỏng chuột vật lý trên Game View. Test khôi phục selection trước đó và thoát Play nếu chính test đã mở Play.

Worker được chọn để test nằm ngoài viewport camera hiện tại; báo cáo projection này không phải lỗi owner hoặc collider. Khi kiểm tra bằng chuột, cần đưa camera tới worker của đúng player local.

Runtime/Editor biên dịch thành công, 0 error, 5 warning cũ. Chạy lại test từ Tools > Selection > Test spawned worker selection; báo cáo ở Artifacts/SelectionSmoke/report.txt.

# FlowFieldAssignmentSystem.cs

Hệ thống này chịu trách nhiệm xử lý các `TargetChangeRequest` và thực hiện việc gán "đường đi thực tế" cho Unit thông qua Flow Field.

---

## 1. Quy trình xử lý (Workflow)
Khi một Unit có `TargetChangeRequest`, hệ thống sẽ thực hiện các bước sau:

### Bước 1: Tìm kiếm trong Cache
Sử dụng `FlowFieldCacheHelper.TryGetFieldFromCache` để xem đã có đường đi nào dẫn đến ô lưới đích đó chưa. Việc này giúp tiết kiệm tài nguyên tối đa.

### Bước 2: Tìm kiếm toàn cục (Fallback)
Nếu trong Cache không có (vừa bị xóa hoặc chưa kịp lưu), hệ thống sẽ quét qua toàn bộ thực thể `FlowField` hiện có để tìm mục tiêu trùng khớp.

### Bước 3: Khởi tạo đường đi mới
Nếu cả hai bước trên đều thất bại, hệ thống sẽ gọi `FlowFieldCacheHelper.CreateFlowField` để tạo ra một thực thể đường đi mới và đăng ký nó vào Cache.

### Bước 4: Gán và Kích hoạt
Gọi `FlowFieldHelper.AssignFieldToMoveComponent` để:
- Cập nhật `FieldEntity` cho Unit.
- Tăng bộ đếm tham chiếu (Ref Count) cho Field.
- Đặt trạng thái `stuckTime = 0` (Vì đây là lệnh mới, Unit chưa thể bị kẹt).

---

## 2. Dọn dẹp (Cleanup)
Sau khi đã gán đường đi thành công, hệ thống sẽ xóa `TargetChangeRequest` khỏi Unit để tránh việc tính toán lặp lại ở khung hình tiếp theo.

---

## 3. Thứ tự thực thi (Execution Order)
Hệ thống này chạy ngay sau `MovementAgentPathRequestSystem` và trước `IntegrationFieldSystem`.
- **Lý do**: Để đảm bảo rằng ngay khi một yêu cầu được tạo ra, nó sẽ có một `FieldEntity` (dù là rỗng/đang chờ tính toán) trước khi hệ thống tính toán lưới bắt đầu làm việc.

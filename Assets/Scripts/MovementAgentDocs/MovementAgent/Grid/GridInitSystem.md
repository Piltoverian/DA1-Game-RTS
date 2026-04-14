# GridInitSystem.cs

Hệ thống này thực hiện việc cấp phát bộ nhớ và khởi tạo giá trị mặc định cho bản đồ ô lưới ngay khi bắt đầu mô phỏng.

---

## 1. Vai trò chính
Trong ECS, các Buffer (`DynamicBuffer`) cần được cấp phát kích thước cụ thể trước khi sử dụng. `GridInitSystem` đảm bảo rằng lưới đã sẵn sàng để các hệ thống khác (như tính toán địa hình hay hòn đảo) ghi dữ liệu vào.

---

## 2. Quy trình OnUpdate

1. **Truy vấn thực thể Grid**: Tìm thực thể có `GridComponent` cùng các Buffer `GridNodeCost` và `GridIsland`.
2. **Thiết lập Collider**: (Tùy chọn) Cập nhật Layer vật lý cho Collider của Grid để hỗ trợ Raycast hoặc va chạm nếu cần.
3. **Cấp phát bộ nhớ (Resize)**:
   - Tính toán tổng số ô lưới: `width * height`.
   - Sử dụng `ResizeUninitialized` để chuẩn bị bộ đệm cho `cbuffer` và `ibuffer`.
4. **Khởi tạo nội dung**:
   - Mặc định `cost = 1` (Ô trống, có thể đi qua).
   - Mặc định `islandID = 0` (Chưa thuộc đảo nào).

---

## 3. Tối ưu (Self-Disabling)
Tương tự như các hệ thống khởi tạo khác, `GridInitSystem` tự tắt (`state.Enabled = false`) sau khi hoàn thành nhiệm vụ ở khung hình đầu tiên. Điều này giúp loại bỏ hoàn toàn chi phí xử lý của nó trong suốt thời gian còn lại của trò chơi.

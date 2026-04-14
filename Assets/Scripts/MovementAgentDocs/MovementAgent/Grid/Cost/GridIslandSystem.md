# GridIslandSystem.cs

Hệ thống này thực hiện việc phân tích địa hình để chia bản đồ thành các "hòn đảo" (Islands) riêng biệt. Điều này cực kỳ quan trọng để ngăn lính cố gắng đi xuyên qua biển hoặc các vùng không thể tới được.

---

## 1. Tại sao cần chia hòn đảo?
Trong thuật toán Flow Field, nếu bạn đặt đích đến ở một hòn đảo và Unit ở một hòn đảo khác, Flow Field có thể dẫn Unit đi "húc đầu vào tường" vì nó chỉ tính toán hướng ngắn nhất mà không biết có đường đi thực sự hay không. Việc chia đảo giúp hệ thống `IntegrationFieldSystem` biết cách đặt các "Seeds" hợp lý.

---

## 2. Thuật toán Flood Fill
Hệ thống sử dụng thuật toán loang (Flood Fill) dựa trên hàng đợi (`NativeQueue`):

1. **Khởi tạo**: Duyệt qua từng ô trên lưới.
2. **Phát hiện vùng mới**: Nếu gặp một ô có thể đi bộ (`cost < 250`) mà chưa được đánh dấu, hệ thống sẽ bắt đầu một hòn đảo mới với một `islandID` duy nhất.
3. **Loang**: Đưa ô đó vào hàng đợi và bắt đầu kiểm tra 8 ô xung quanh:
   - Nếu ô hàng xóm cũng đi bộ được, gán cùng `islandID` và tiếp tục loang.
   - **Lưu ý về góc chéo**: Hệ thống kiểm tra xem việc đi chéo có bị kẹt giữa 2 vật cản không. Nếu có, nó sẽ không loang theo hướng chéo đó để tránh lỗi di chuyển xuyên tường.
4. **Kết thúc**: Lặp lại cho đến khi mọi ô trên bản đồ đều đã được xử lý.

---

## 3. Hiệu năng
- Hệ thống sử dụng bộ nhớ tạm (`Allocator.Temp`) để xử lý nhanh trong một khung hình.
- Sau khi tính toán xong toàn bộ bản đồ, hệ thống tự tắt (`state.Enabled = false`).
- Dữ liệu hòn đảo được lưu vào `DynamicBuffer<GridIsland>` để các hệ thống tìm đường sử dụng lâu dài.

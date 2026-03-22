# UnitSpatialSystem.cs

Hệ thống này chịu trách nhiệm xây dựng bộ chỉ mục không gian (Spatial Index) cho toàn bộ Unit, giúp hệ thống né tránh có thể tìm ra hàng xóm gần nhất mà không cần phải so sánh khoảng cách với hàng nghìn Unit khác.

---

## 1. Cơ chế Hash Grid (Bucket Map)
Thay vì thuật toán $O(N^2)$ (so sánh mọi Unit với mọi Unit), hệ thống chia bản đồ thành các ô lưới (Grid Cells). Mỗi ô lưới sẽ chứa một danh sách các Entity đang đứng bên trong nó.

- **`BucketContainer`**: Một thực thể Singleton nắm giữ `NativeParallelMultiHashMap<int, Entity>`. 
- **Khóa (Key)**: Chỉ số của ô lưới (`gridIndex`).
- **Giá trị (Value)**: Entity đang đứng tại ô đó.

---

## 2. Quy trình cập nhật (OnUpdate)
Hệ thống liên tục kiểm tra vị trí của lính:
1. Tính toán `newIndex` dựa trên vị trí hiện tại của Unit.
2. So sánh với `oldIndex` (được lưu trong `MovementAgentAvoidanceComponent`).
3. **Nếu Unit đã di chuyển sang ô lưới mới**:
   - Xóa Entity khỏi ô cũ trong HashMap.
   - Thêm Entity vào ô mới trong HashMap.
   - Cập nhật `gridIndex` mới cho Unit.

---

## 3. Quản lý Tài nguyên
`UnitSpatialSystem` là "chủ sở hữu" của `BucketMap`. 
- Nó khởi tạo bộ nhớ ở trạng thái `Persistent` (tồn tại xuyên suốt game).
- Nó có trách nhiệm gọi `Dispose()` trong hàm `OnDestroy` để đảm bảo không bị rò rỉ bộ nhớ (Memory Leak).

---

## 4. Tầm quan trọng đối với Né tránh
Nhờ có hệ thống này, `MovementAgentAvoidanceSystem` chỉ cần lấy danh sách Entity trong ô lưới hiện tại và 8 ô lân cận. Việc này giảm số lượng phép tính từ hàng triệu xuống còn vài chục phép tính mỗi Unit, cho phép game chạy mượt mà với số lượng quân đội cực lớn.

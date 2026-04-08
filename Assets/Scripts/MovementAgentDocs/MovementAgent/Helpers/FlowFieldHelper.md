# FlowFieldHelper.cs

Lớp tiện ích này chịu trách nhiệm quản lý vòng đời của các Flow Field và cơ chế gán chúng cho Unit.

---

## 1. Phương thức: FlowFieldInit
Khởi tạo một thực thể Flow Field hoàn toàn mới. Đây là nơi thiết lập các Buffer và dữ liệu ban đầu cho thuật toán tìm đường.

- **Dữ liệu được gán**:
    - `FlowFieldStatus`: Trạng thái "Requested" (Yêu cầu tính toán).
    - `FlowField`: Lưu tọa độ đích đến của lưới.
    - `FlowFieldRefCount`: Khởi tạo bằng 0.
    - `FieldNode` buffer: Kích thước bằng toàn bộ Grid (width * height). Mỗi node mặc định có `bestcost = int.MaxValue` (Chưa tìm thấy đường).
    - `IslandSeed` buffer: Dùng để lưu trữ các "điểm hạt giống" của các đảo cho cơ chế đa đảo.

---

## 2. Phương thức: AssignFieldToMoveComponent
Hàm quan trọng nhất để gán đường đi cho một Unit. Nó thực hiện quy trình sau:

### 2.1 – Giải phóng Field cũ (Dọn dẹp)
Nếu Unit đang bám theo một Flow Field cũ, hệ thống sẽ giảm `FlowFieldRefCount` của thực thể đó. Khi bộ đếm tham chiếu này về 0, hệ thống `FlowFieldCleanUp` sẽ tự động xóa thực thể cũ để giải phóng bộ nhớ.

### 2.2 – Cập nhật dữ liệu cho Unit
- Gán `FieldEntity` mới.
- Cập nhật `currentworldtarget`.
- Đặt `useSlotTarget = false` và `isSettled = false` để kích hoạt lại quá trình di chuyển.

### 2.3 – Đăng ký vào Field mới
Tăng `FlowFieldRefCount` của thực thể Flow Field mới. Điều này đảm bảo rằng chúng ta không bao giờ xóa nhầm một đường đi khi vẫn còn Unit đang đứng trên đó.

---

## 3. Quản lý Tài nguyên (Resource Management)
FlowFieldHelper giúp giải quyết bài toán lớn trong RTS: **Làm sao để hàng nghìn quân l lính đi đến cùng một đích mà chỉ tốn tài nguyên tính toán một lần?**

Nhờ cơ chế đếm tham chiếu, hàng nghìn Unit có thể trỏ vào cùng một `FieldEntity`. Khi lính cuối cùng đến đích hoặc chết, tài nguyên này sẽ tự động được dọn dẹp.

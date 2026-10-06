# FlowDirectionSystem.cs

Hệ thống này chịu trách nhiệm bước cuối cùng trong quá trình tạo Flow Field: Tính toán hướng di chuyển (`direction`) cho từng ô lưới dựa trên bản đồ chi phí (`bestcost`) đã có.

---

## 1. Cơ chế vận hành
Sau khi `IntegrationFieldSystem` đã tính toán xong khoảng cách từ mọi ô đến mục tiêu, `FlowDirectionSystem` sẽ bắt đầu làm việc.

- **Đối tượng xử lý**: Các thực thể `FlowField` có trạng thái `CalculatingDirection`.
- **Phương pháp**: Sử dụng `IJobChunk` để xử lý song song nhiều Flow Field cùng lúc, tận dụng tối đa sức mạnh đa nhân của CPU.

---

## 2. Thuật toán tính hướng (CalculateDirectionFieldJob)
Trên mỗi ô lưới, thuật toán thực hiện:
1.  **Kiểm tra 8 hàng xóm**: Lấy `bestcost` của 8 ô xung quanh (ngang, dọc, chéo).
2.  **Tìm ô tối ưu**: So sánh và chọn ra ô hàng xóm có `bestcost` thấp nhất (tức là gần đích nhất).
3.  **Tạo Vector hướng**:
    - Nếu tìm thấy hàng xóm tốt hơn ô hiện tại: Tạo một vector hướng về phía hàng xóm đó và chuẩn hóa nó (`math.normalize`).
    - Nếu không tìm thấy (đang ở chính đích đến): Vector hướng sẽ là `(0, 0)`.

---

## 3. Chuyển đổi trạng thái (State Transition)
Khi toàn bộ các ô trong một Flow Field đã được tính toán xong hướng di chuyển:
- Trạng thái của Flow Field sẽ chuyển từ `CalculatingDirection` sang **`Ready`**.
- Lúc này, các Unit đang bám theo Flow Field này sẽ chính thức nhận được dữ liệu hướng để bắt đầu di chuyển.

---

## 4. Hiệu suất
Sử dụng `v128 chunkEnabledMask` và `ArchetypeChunk` giúp hệ thống này cực kỳ nhanh, có thể xử lý hàng chục bản đồ lưới kích thước lớn mà không gây drop FPS.
```csharp
[UpdateAfter(typeof(IntegrationFieldSystem))]
```
Hệ thống này bắt buộc phải chạy sau khi bản đồ chi phí đã hoàn tất.

# MovementAgentAuthoring.cs

File này đóng vai trò là cầu nối giữa Inspector của Unity (MonoBehaviour) và hệ thống ECS (Entity Component System). Nó cho phép nhà phát triển thiết lập các thông số di chuyển của Unit một cách trực quan.

---

## 1. Các thông số cấu hình (Inspector Fields)
Người dùng có thể điều chỉnh các giá trị sau trên Prefab của Unit:

- `speed`: Tốc độ tối đa.
- `radius`: Kích thước vật lý của Unit (dùng cho né tránh).
- `formationRange`: Khoảng cách mà Unit bắt đầu chuẩn bị dàn đội hình (ví dụ: 25m).
- `formationType`: Loại đội hình mặc định (Box hoặc Circle).
- **Testing**: Cho phép đặt một mục tiêu giả lập (`testTarget`) để kiểm tra di chuyển ngay khi nhấn Play mà không cần Code điều khiển.

---

## 2. Cơ chế Tự động Tinh chỉnh (OnValidate)
Đây là một tính năng quan trọng giúp đảm bảo Unit luôn di chuyển mượt mà mà không cần người dùng phải tính toán toán học phức tạp.

Khi bạn thay đổi `speed` hoặc `radius` trong Inspector, hàm này sẽ tự động cập nhật:
- **`stoppingDistance`**: Được tính bằng `radius * 1.5f`. Điều này đảm bảo Unit có thể tiến đủ gần vào vị trí Slot của mình mà không bị dừng lại quá sớm.
- **`arrivalRadius`**: Được tính dựa trên tốc độ (`speed * 0.8f`). Tốc độ càng cao thì Unit cần quãng đường hãm phanh càng dài để tránh hiện tượng đi quá đà (overshoot).

```csharp
private void OnValidate()
{
    stoppingDistance = radius * 1.5f; 
    arrivalRadius = Mathf.Max(radius * 4f, speed * 0.8f);
}
```

---

## 3. Quy trình Bake (Baker)
Hàm `Bake` sẽ chuyển đổi dữ liệu từ `MovementAgentAuthoring` sang các Component ECS:

1. **Thêm dữ liệu di chuyển**: `MovementAgentComponent`, `MovementAgentFormationComponent`.
2. **Thêm dữ liệu né tránh**: `MovementAgentAvoidanceComponent` (khởi tạo `sidePreference` dựa trên Entity Index để tránh xung đột trực diện).
3. **Thêm dữ liệu điều hướng**: `MovementSteeringComponent`.
4. **Khởi tạo Context Steering**: 
   - Thiết lập `Resolution = 16` (16 hướng né tránh).
   - Thêm `ContextMapElement` buffer và `ContextHistoryElement` buffer.
   - Các buffer này được khởi tạo trống để sẵn sàng cho `MovementAgentAvoidanceSystem` sử dụng.

---

## 4. Ghi chú đặc biệt
Nếu `useTestTarget` được bật, Baker sẽ tự động thêm một `TargetChangeRequest` ngay khi khởi tạo, giúp Unit bắt đầu hành trình ngay lập tức khi vào game.

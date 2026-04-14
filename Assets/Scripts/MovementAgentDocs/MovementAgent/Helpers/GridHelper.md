# GridHelper.cs

Lớp tĩnh (`static class`) này cung cấp các hàm tiện ích để chuyển đổi giữa tọa độ không gian thực (3D World Space) và tọa độ ô lưới (2D Grid Space).

---

## 1. Phương thức: WorldToGrid
Chuyển đổi vị trí thực tế của Unit sang vị trí ô trên lưới.

- **Đầu vào**: `float3 worldPos`, `GridComponent grid`.
- **Cơ chế**: Trừ đi điểm gốc (`origin`) của lưới và chia cho `cellsize`. Kết quả được làm tròn xuống (`floor`) để tìm đúng chỉ số ô.
```csharp
public static int2 WorldToGrid(float3 worldPos, GridComponent grid)
```

---

## 2. Phương thức: GridToWorld
Chuyển đổi tọa độ một ô lưới sang vị trí 3D tại trung tâm của ô đó.

- **Đầu vào**: `int2 gridPos`, `GridComponent grid`.
- **Cơ chế**: Nhân tọa độ ô với `cellsize` và cộng thêm `cellsize / 2` để lấy điểm chính giữa ô. Trục Y mặc định trả về 0.
```csharp
public static float3 GridToWorld(int2 gridPos, GridComponent grid)
```

---

## 3. Quản lý Chỉ mục (Index Management)

### GetNodeIndex
Tính toán chỉ mục 1 chiều (1D Index) từ tọa độ 2D.
- Dùng để truy cập các mảng phẳng (`NativeArray`) như `GridNodeCost` hay `FieldNode`.
- Công thức: `y * width + x`.

### GetGridPosFromIndex
Chuyển đổi ngược lại từ chỉ mục 1 chiều sang tọa độ `int2`.
- Dùng khi cần biết vị trí thực kế của một ô từ chỉ số mảng.

---

## 4. Ứng dụng
Hầu hết các hệ thống như `IntegrationFieldSystem`, `AvoidanceSystem` và `SpatialSystem` đều phụ thuộc vào lớp này để xác định vị trí của Unit trên bản đồ dữ liệu của game.

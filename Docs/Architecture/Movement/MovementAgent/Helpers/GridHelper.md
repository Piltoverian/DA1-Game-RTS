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

## 4. NaturalBlockedTargetResolver
Lớp tĩnh nằm cùng file `GridHelper.cs`, chịu trách nhiệm phân loại và phân giải (resolve) mục tiêu khi người chơi click vào vùng vật cản.

### Các hàm kiểm tra cơ bản
- `IsCellInBounds(int2 cell, GridComponent grid)`: Kiểm tra ô lưới có nằm trong phạm vi `[0 .. width - 1, 0 .. height - 1]` hay không.
- `IsCellWalkable(int2 cell, GridComponent grid, DynamicBuffer<GridNodeCost> gridCosts)`: Kiểm tra ô nằm trong biên và có `cost < 255`.

### `TryGetBlockageGridBounds` (Kiểm tra Building Blockage theo Cell)
- Tính hộp bao của công trình từ `BlockageData` và `LocalToWorld` (mở rộng `+0.01f` / `-0.01f` khớp với `CostChangeSystem`) rồi quy đổi sang phạm vi ô lưới `[bMinGrid .. bMaxGrid]`.
- **Quy ước thiết kế**: Nếu footprint của công trình lấn sang bất kỳ phần nào của một ô lưới, toàn bộ ô lưới đó được xem là thuộc công trình (`BuildingBlockage`).

### `TryResolveTarget` & `TryResolveNaturalBlockedCell`
Phân giải tọa độ click `rawWorldTarget` thành `resolvedCell`, `resolvedWorldTarget` và `TargetResolutionKind`:
1. **Ngoài bản đồ**: Trả về `false` (`TargetResolutionKind.None`).
2. **Thuộc công trình (`TryGetBlockageGridBounds == true`)**: Giữ nguyên ô click và tọa độ gốc, trả về `TargetResolutionKind.BuildingBlockage` để hệ thống điều hướng chạy logic bám chu vi công trình.
3. **Ô đi được bình thường (`cost < 255`)**: Giữ nguyên ô click và tọa độ gốc, trả về `TargetResolutionKind.Direct`.
4. **Vật cản tự nhiên (`cost ≥ 255` và không thuộc công trình)**:
   - Quét 4 hướng theo thứ tự cố định: **Bắc `(0, +1)` → Đông `(+1, 0)` → Nam `(0, -1)` → Tây `(-1, 0)`**.
   - Ở mỗi hướng, tìm ô walkable đầu tiên (`cost < 255`) và tính khoảng cách trên mặt phẳng XZ từ điểm click chính xác (`rawWorldTarget.xz`) đến **cạnh vào (entry edge)** của ô đó.
   - Chọn ứng viên có khoảng cách ngắn nhất; ứng viên đến sau chỉ thắng khi `distance < bestDistance - 0.0001f` (giữ thứ tự ưu tiên Bắc → Đông → Nam → Tây khi khoảng cách bằng nhau).
   - Nếu tìm thấy: trả về `resolvedCell`, `resolvedWorldTarget = GridHelper.GridToWorld(resolvedCell, grid)` (tâm ô) và `TargetResolutionKind.NaturalResolved`.
   - Nếu cả 4 hướng đều kịch biên mà không có ô walkable: trả về `false` (`TargetResolutionKind.None`).

---

## 5. Ứng dụng
Hầu hết các hệ thống như `FlowFieldAssignmentSystem`, `IntegrationFieldSystem`, `MovementAgentTargetSystem`, `AvoidanceSystem` và `SpatialSystem` đều phụ thuộc vào `GridHelper` và `NaturalBlockedTargetResolver` để quy đổi tọa độ và xử lý vật cản trên bản đồ.

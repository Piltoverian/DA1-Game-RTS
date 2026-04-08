# FlowFieldInit.cs

File này định nghĩa các cấu trúc dữ liệu cơ bản (Structs) cho hệ thống Flow Field. Mặc dù tên file là `Init`, nhưng vai trò chính của nó là chứa các định nghĩa `IComponentData` và `IBufferElementData`.

---

## 1. FieldNode (`IBufferElementData`)
Đây là đơn vị dữ liệu nhỏ nhất trên lưới của Flow Field. Mỗi ô lưới sẽ có một `FieldNode`.

- **`bestcost`**: Chi phí thấp nhất (khoảng cách) từ ô này về đến đích. Giá trị càng nhỏ càng gần đích.
- **`direction`**: Vector hướng (`float2`) chỉ về phía ô hàng xóm có chi phí thấp nhất.

---

## 2. IslandSeed (`IBufferElementData`)
Dùng cho cơ chế **Multi-Island Pathfinding** (Tìm đường đa đảo).
- **`islandID`**: ID của hòn đảo.
- **`seedPosition`**: Vị trí "hạt giống" trên đảo đó. Đây là điểm đích phụ cho các Unit thuộc đảo này nếu đích chính nằm ở đảo khác.

---

## 3. FlowField (`IComponentData`)
Thành phần nhận diện một thực thể là Flow Field.
- **`targetcell`**: Tọa độ ô lưới đích đến của toàn bộ bản đồ này.

---

## 4. FlowFieldRefCount (`IComponentData`)
Bộ đếm tham chiếu dùng để quản lý vòng đời.
- **`value`**: Số lượng Unit đang sử dụng Flow Field này. Khi giá trị này về 0, Field có thể bị xóa an toàn.

# GridComponent.cs

File này định nghĩa cấu trúc dữ liệu cơ bản cho hệ thống bản đồ ô lưới (Grid) trong ECS.

---

## 1. GridComponent (`IComponentData`)
Đây là thành phần cốt lõi chứa thông tin cấu hình của lưới.

- **`width` / `height`**: Số lượng ô lưới theo chiều ngang và dọc.
- **`cellsize`**: Kích thước của mỗi ô lưới (đơn vị Meter).
- **`origin`**: Tọa độ góc dưới bên trái (`Minimum Bounds`) của lưới trong không gian thế giới.

---

## 2. GridNodeCost (`IBufferElementData`)
Một Buffer chứa chi phí di chuyển (Cost) của từng ô lưới.
- Dữ liệu được lưu trữ dưới dạng mảng phẳng 1 chiều.
- Chỉ số ô được tính toán thông qua `GridHelper.GetNodeIndex`.
- **Giá trị đặc biệt**: `250` hoặc `int.MaxValue` thường được dùng để đánh dấu vật cản không thể đi qua.

---

## 3. GridIsland (`IBufferElementData`)
Một Buffer chứa ID của "hòn đảo" mà ô lưới đó thuộc về.
- Dùng để phục vụ cho thuật toán tìm đường đa đảo.
- Nếu hai ô có cùng `islandID`, nghĩa là có một đường đi liên tục kết nối chúng mà không bị cản bởi nước hoặc tường.

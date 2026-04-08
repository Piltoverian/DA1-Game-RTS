# GridDebug.cs

File này cung cấp các công cụ trực quan hóa (Visualization) mạnh mẽ giúp lập trình viên kiểm tra dữ liệu của hệ thống Grid và Flow Field ngay trong cửa sổ **Scene** của Unity.

---

## 1. Các chế độ hiển thị (FlowFieldDebugMode)

Bạn có thể thay đổi chế độ xem trong Inspector:
- **`Grid`**: Hiển thị khung dây (Wireframe) của toàn bộ lưới.
- **`Islands`**: Tô màu mỗi ô lưới dựa trên ID hòn đảo của nó. Các ô cùng màu nghĩa là thuộc cùng một vùng có thể đi lại được.
- **`Integration`**: Hiển thị chi phí tích lũy (`bestcost`). Màu sắc chuyển từ **Xanh (gần đích)** sang **Đỏ (xa đích)**.
- **`Direction`**: Hiển thị các mũi tên chỉ hướng di chuyển của Flow Field tại mỗi ô.

---

## 2. Các tham số hỗ trợ
- **`drawStep`**: Nếu bản đồ quá lớn (ví dụ 1024x1024), việc vẽ 1 triệu mũi tên sẽ làm Unity bị treo. `drawStep` giúp chỉ vẽ cách quãng (vẽ 1 ô, bỏ qua N ô) để duy trì FPS.
- **`targetFieldIndex`**: Vì trong game có thể có hàng trăm Flow Field cùng lúc, tham số này cho phép bạn chọn chính xác bản đồ của Unit nào mà bạn muốn soi chi tiết.

---

## 3. Cơ chế hoạt động
Hệ thống sử dụng hàm `OnDrawGizmos` của Unity:
1. Truy cập vào thế giới ECS thông qua `World.DefaultGameObjectInjectionWorld`.
2. Lấy dữ liệu từ các `DynamicBuffer` như `GridNodeCost`, `GridIsland`, và `FieldNode`.
3. Chuyển đổi tọa độ chỉ mục (Index) sang tọa độ thế giới (World Space) bằng `GridHelper`.
4. Thực hiện vẽ các khối Cube, Line hoặc Arrow tương ứng.

---

## 4. Lưu ý sử dụng
Cung gỡ lỗi này chỉ hoạt động khi game đang chạy (**Play Mode**) vì dữ liệu Flow Field được tạo ra và lưu trữ động trong bộ nhớ ECS.

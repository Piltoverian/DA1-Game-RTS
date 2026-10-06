# FlowFieldCacheHelper.cs

Lớp tiện ích này quản lý hệ thống Cache cho các Flow Field. Trong một game RTS, nhiều nhóm lính có thể di chuyển đến các đích khác nhau. Việc lưu trữ lại các đường đi đã tính toán giúp tránh việc phải tính toán lại CPU cho cùng một đích đến nhiều lần.

---

## 1. Hằng số: MAX_FLOWFIELDS
Hệ thống giới hạn tối đa **160** Flow Field được lưu trong bộ nhớ cùng một lúc. Con số này đủ cho hầu hết các tình huống điều khiển quân đội phức tạp mà không gây tốn quá nhiều RAM.

---

## 2. Phương thức: TryGetFieldFromCache
Tìm kiếm một đường đi đã tồn tại dựa trên tọa độ ô lưới đích (`targetCell`).

- **Cơ chế**: Duyệt qua danh sách Cache. Nếu khớp tọa độ, nó sẽ cập nhật `lastUsedFrame` thành khung hình hiện tại.
- **LRU Logic**: Việc cập nhật `lastUsedFrame` giúp hệ thống biết được đường đi nào đang được sử dụng thường xuyên nhất.

---

## 3. Phương thức: RemoveLeastUsed (Cơ chế LRU)
Khi Cache đầy (vượt quá 160), hệ thống cần xóa bớt.
- **Thuật toán**: Tìm kiếm mục nhập có `lastUsedFrame` thấp nhất (cũ nhất) và xóa nó đi. Điều này đảm bảo những đường đi lâu rồi không ai dùng sẽ bị loại bỏ trước.

---

## 4. Phương thức: CreateFlowField
Hàm tổng hợp để tạo và lưu trữ Flow Field mới.

1. Kiểm tra xem Cache có đầy không, nếu có thì gọi `RemoveLeastUsed`.
2. Gọi `FlowFieldHelper.FlowFieldInit` để tạo thực thể Flow Field.
3. Thêm mục nhập mới vào `DynamicBuffer<FlowFieldCacheEntry>` kèm theo thông tin về khung hình hiện tại.

---

## 5. Lợi ích của hệ thống Cache
- **Tiết kiệm CPU**: Không phải chạy thuật toán Dijkstra/Eikonal mỗi khi người chơi click vào cùng một vùng đất.
- **Quản lý bộ nhớ thông minh**: Tự động dọn dẹp các đường đi cũ thông qua cơ chế LRU (Least Recently Used).

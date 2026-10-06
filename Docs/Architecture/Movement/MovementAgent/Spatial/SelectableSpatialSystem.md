# SelectableSpatialSystem.cs

Hệ thống này giúp tối ưu hóa việc lựa chọn Unit (Selection) bằng cách đưa các thực thể có thể chọn được (`Selectable`) vào bộ chỉ mục không gian chung.

---

## 1. Vai trò chính
Khi người chơi quét chuột (Drag select) hoặc click vào một điểm trên màn hình, thay vì phải kiểm tra xem chuột có trúng hàng nghìn Unit không, hệ thống Selection sẽ:
1. Xác định ô lưới tại vị trí chuột.
2. Lấy danh sách các Entity trong ô đó từ `BucketMap`.
3. Chỉ kiểm tra va chạm với danh sách nhỏ này.

---

## 2. Dùng chung tài nguyên (Shared BucketMap)
Hệ thống này không tự tạo ra mảng băm riêng. Nó sử dụng chung `BucketContainer` mà `UnitSpatialSystem` đã tạo ra.
- **Tiết kiệm bộ nhớ**: Không tốn thêm RAM để lưu trữ nhiều bản đồ không gian khác nhau.
- **Tính nhất quán**: Cả né tránh và lựa chọn đều dùng chung một lưới dữ liệu.

---

## 3. Quy trình cập nhật
Tương tự như `UnitSpatialSystem`, hệ thống này:
- Theo dõi sự thay đổi ô lưới (`GridIndex`) của các thực thể `Selectable`.
- Cập nhật vị trí của chúng trong `BucketMap` mỗi khi di chuyển.

---

## 4. Thứ tự thực thi
Hệ thống chạy sau `UnitSpatialSystem` để đảm bảo Singleton đã sẵn sàng, và chạy trước `SelectSystem` (hệ thống xử lý click chuột thực sự) để dữ liệu luôn là mới nhất.

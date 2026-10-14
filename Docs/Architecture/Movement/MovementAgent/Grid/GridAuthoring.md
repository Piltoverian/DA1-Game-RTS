# GridAuthoring.cs

tệp này cho phép nhà phát triển trực tiếp cấu hình lưới trên môi trường Unity Editor thông qua một thành phần GameObject.

---

## 1. Các thông số cấu hình

- **`MincellSize`**: Kích thước tối thiểu của một ô lưới.
- **`mapType`**: Kiểu kích thước bản đồ (`Small`: 64x64, `Medium`: 128x128, `Large`: 256x256).

---

## 2. Cơ chế chuyển dữ liệu sang ECS (Baking xử lý logic)
Hệ thống chuyển dữ liệu sang ECS sẽ tự động tính toán các giá trị của `GridComponent` dựa trên đối tượng mà nó được gắn vào:

1. **Lấy kích cỡ từ Renderer**: Nó sử dụng `renderer.bounds` của GameObject (thường là một tấm Plane lớn làm nền đất) để xác định kích thước thực tế của thế giới.
2. **Tính toán `cellsize`**: Kích thước thực tế chia cho độ phân giải của `mapType`.
3. **Xác định điểm gốc (`origin`)**: Lấy điểm tọa độ tối thiểu (`bounds.min`) làm điểm bắt đầu của lưới.
4. **Khởi tạo bộ đệm**: Tạo sẵn các bộ đệm `GridNodeCost` và `GridIsland` trên thực thể lưới để sẵn sàng nạp dữ liệu địa hình.

---

## 3. Ràng buộc (kiểm tra hợp lệ)
Trong quá trình chuyển dữ liệu sang ECS, hệ thống sẽ thực hiện kiểm tra lỗi:
- Nếu GameObject gắn `GridAuthoring` không có `Renderer`, nó sẽ báo lỗi.
- Nếu kích thước thực tế của vật thể quá nhỏ so với độ phân giải `mapType` yêu cầu (khiến `cellsize` quá bé), hệ thống sẽ từ chối chuyển dữ liệu sang ECS để đảm bảo hiệu suất.
- Điều này giúp lập trình viên tránh các lỗi thiết lập bản đồ sai lệch ngay từ khâu thiết kế mức.

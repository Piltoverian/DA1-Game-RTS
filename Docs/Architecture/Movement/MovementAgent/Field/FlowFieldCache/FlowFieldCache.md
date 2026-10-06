# FlowFieldCache.cs

File này định nghĩa cấu trúc dữ liệu cho hệ thống Cache của Flow Field. Đây là nơi lưu trữ thông tin về các đường đi đã được tính toán để có thể tái sử dụng nhanh chóng.

---

## 1. FlowFieldCache (`IComponentData`)
Một thành phần rỗng (Tag Component) dùng để đánh dấu thực thể Singleton quản lý Cache.

---

## 2. FlowFieldCacheEntry (`IBufferElementData`)
Định nghĩa một mục nhập (Entry) trong danh sách Cache. Mỗi mục nhập chứa:

- **`targetCell`**: Tọa độ ô lưới đích đến. Đây là "khóa" (Key) để tìm kiếm trong Cache.
- **`flowField`**: Thực thể (Entity) chứa dữ liệu đường đi tương ứng.
- **`lastUsedFrame`**: Khung hình cuối cùng mà đường đi này được Unit yêu cầu. Dùng cho thuật toán dọn dẹp LRU (Least Recently Used).

---

## 3. Tầm quan trọng
Việc lưu trữ theo cặp `(targetCell, flowField)` cho phép hệ thống `FlowFieldAssignmentSystem` kiểm tra nhanh chóng xem có cần phải tạo mới đường đi hay không, từ đó tối ưu hóa đáng kể tài nguyên CPU.

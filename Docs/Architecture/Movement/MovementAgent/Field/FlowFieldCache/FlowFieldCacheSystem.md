# FlowFieldCacheSystem.cs

Hệ thống này chịu trách nhiệm khởi tạo (khởi tạo) hệ thống bộ nhớ đệm cho trường hướng Field ngay khi trò chơi bắt đầu.

---

## 1. Vai trò chính
Trong kiến trúc ECS, bộ nhớ đệm được quản lý thông qua một **duy nhất Entity**. Hệ thống `FlowFieldCacheInitSystem` đảm bảo thực thể này được tạo ra một lần duy nhất.

---

## 2. Quy trình OnCreate
Khi hệ thống được khởi tạo:
1. Tạo một thực thể duy nhất cho `FlowFieldCache`.
2. Thêm một `DynamicBuffer` chứa danh sách các `FlowFieldCacheEntry`.
3. bộ đệm này ban đầu trống và sẽ được lấp đầy bởi `FlowFieldAssignmentSystem`.

---

## 3. Tối ưu (Self-tắt)
Sau khi đã thực hiện xong việc khởi tạo trong khung hình đầu tiên, hệ thống tự đặt `state.Enabled = false`.
- **Lý do**: Việc khởi tạo chỉ cần diễn ra 1 lần. Tắt hệ thống này giúp tiết kiệm tài nguyên CPU cho các khung hình tiếp theo.

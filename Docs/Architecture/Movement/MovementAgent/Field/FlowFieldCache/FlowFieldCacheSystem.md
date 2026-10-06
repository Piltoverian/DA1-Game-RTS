# FlowFieldCacheSystem.cs

Hệ thống này chịu trách nhiệm khởi tạo (Bootstrap) hệ thống Cache cho Flow Field ngay khi game bắt đầu.

---

## 1. Vai trò chính
Trong kiến trúc ECS, Cache được quản lý thông qua một **Singleton Entity**. Hệ thống `FlowFieldCacheInitSystem` đảm bảo thực thể này được tạo ra một lần duy nhất.

---

## 2. Quy trình OnCreate
Khi hệ thống được khởi tạo:
1. Tạo một thực thể Singleton cho `FlowFieldCache`.
2. Thêm một `DynamicBuffer` chứa danh sách các `FlowFieldCacheEntry`.
3. Buffer này ban đầu trống và sẽ được lấp đầy bởi `FlowFieldAssignmentSystem`.

---

## 3. Tối ưu (Self-Disabling)
Sau khi đã thực hiện xong việc khởi tạo trong khung hình đầu tiên, hệ thống tự đặt `state.Enabled = false`.
- **Lý do**: Việc khởi tạo chỉ cần diễn ra 1 lần. Tắt hệ thống này giúp tiết kiệm tài nguyên CPU cho các khung hình tiếp theo.

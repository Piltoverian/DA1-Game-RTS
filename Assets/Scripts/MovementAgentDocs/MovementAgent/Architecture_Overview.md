# Movement Agent - Tổng quan Kiến trúc & Luồng xử lý

Tài liệu này mô tả cách các thành phần trong `MovementAgent` phối hợp với nhau để biến một cú click chuột thành hành động di chuyển thông minh của đại quân.

---

## 1. Các lớp (Layers) trong hệ thống
Hệ thống được chia thành 4 lớp chính theo mô hình Agentic:

| Lớp | Folder | Chức năng chính |
| :--- | :--- | :--- |
| **L0 - Data** | `AgentMovementData/` | Chứa Component (`IComponentData`) và Authoring. |
| **L1 - Grid** | `Grid/` | Quản lý dữ liệu bản đồ, chi phí (Cost) và Đảo (Islands). |
| **L2 - Logic** | `Field/`, `LocalAvoidance/` | Tính toán Pathfinding (Flow Field) và Né tránh va chạm. |
| **L3 - Command** | `CommandBridge/` | Tiếp nhận yêu cầu di chuyển từ bên ngoài vào Agent. |
| **Support** | `Spatial/`, `Helpers/` | Tăng tốc tìm kiếm hàng xóm và các hàm toán học dùng chung. |

---

## 2. Vòng đời của một lệnh di chuyển (Command Flow)
Khi người chơi ra lệnh di chuyển cho một nhóm Unit, quy trình sau sẽ diễn ra:

### Bước 1: Phát hiện yêu cầu (`PathRequestSystem`)
Hệ thống kiểm tra xem `currentworldtarget` của Unit có thay đổi so với đích đến cũ không. Nếu có, nó sẽ gắn một `TargetChangeRequest` vào Unit.

### Bước 2: Phân bổ đội hình (`GroupFormationSystem`)
Các Unit có cùng mục tiêu sẽ được gom nhóm. Hệ thống tính toán các "Slot" (vị trí đứng) trong đội hình (Box/Circle) và gán `slotTarget` cho từng Unit dựa trên khoảng cách gần nhất.

### Bước 3: Gán đường đi (`FlowFieldAssignmentSystem`)
Dựa trên mục tiêu mới, hệ thống sẽ tìm trong `Cache` xem đã có Flow Field dẫn đến đó chưa.
- Nếu có: Gán ngay `FieldEntity` cho Unit.
- Nếu không: Khởi tạo một Flow Field mới và yêu cầu `IntegrationFieldSystem` tính toán.

### Bước 4: Di chuyển và Né tránh (`AvoidanceSystem`)
Mỗi frame, Unit sẽ đọc hướng từ Flow Field và kết hợp với đích đến hiện tại (`slotTarget`). Sau đó, nó đi qua hai lớp chống va chạm:
1. **Lớp ORCA** (Tối ưu vận tốc): Tính toán vận tốc giúp né tránh dự phòng các va chạm trong tương lai gần với các Agent lân cận mà không làm lệch lạc quỹ đạo chung.
2. **Lớp Separation** (Chống nén): Tính toán một vector lực khuếch tán dựa trên khoảng cách, đẩy dạt các Unit ra ngay lập tức nếu chúng vi phạm bán kính vật lý của nhau (hard-collision prevention).

### Bước 5: Thực thi (`ActuatorSystem`)
Kết hợp Preferred Velocity (vận tốc tối ưu ban đầu), ORCA Velocity, và lực Separation Force để tính toán vận tốc cuối cùng. Áp dụng cơ chế Hội tụ (First-come-first-settled) để hệ thống nhận diện khi Agent đến đích có thể lập tức dừng ("được neo") mà không bị xô đẩy bởi các Unit đang tụ lại phía sau. Cuối cùng, cập nhật hướng nhìn (`Rotation`) và ghi dữ liệu vào `LocalTransform` của hệ thống lý thuyết (DOTS).

---

## 3. Hệ thống Chỉ mục Không gian (Spatial Indexing)
Để né tránh mượt mà mà không làm sụt giảm FPS, chúng ta sử dụng `UnitSpatialSystem`.
- **Cơ chế**: Chia bản đồ thành các ô "Buckets". Mỗi Unit được ghi danh vào đúng ô của mình.
- **Lợi ích**: Khi cần né hàng xóm, Unit chỉ cần kiểm tra các Unit trong ô hiện tại và 8 ô xung quanh, thay vì kiểm tra toàn bộ Unit trên bản đồ.

---

## 4. Debug & Quan sát
Hệ thống đi kèm với một **MovementAgentDebugSystem** chuyên dụng:
- **Tia Xanh dương**: Hướng Unit muốn đi.
- **Tia Đỏ**: Hướng Unit sợ va chạm.
- **Vạch Vàng Cam**: Điểm Slot trong đội hình.
- **Vạch Tím**: Đích đến thực tế trên đảo.

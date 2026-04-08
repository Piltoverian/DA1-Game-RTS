# FlowFieldState.cs

File này định nghĩa các trạng thái vòng đời (Lifecycle) của một Flow Field. Việc sử dụng máy trạng thái (State Machine) giúp các hệ thống khác nhau biết khi nào cần nhảy vào xử lý dữ liệu.

---

## 1. Các trạng thái (FieldState)

Hệ thống di chuyển sử dụng 4 trạng thái chính:

| Trạng thái | Mô tả | Hệ thống xử lý |
| :--- | :--- | :--- |
| **`Requested`** | Vừa được tạo ra, chưa có dữ liệu gì. | `FlowFieldAssignmentSystem` |
| **`CalculatingCost`** | Đang được tính toán bản đồ chi phí (Dijkstra/BFS). | `IntegrationFieldSystem` |
| **`CalculatingDirection`** | Đã có chi phí, đang tính toán các vector hướng. | `FlowDirectionSystem` |
| **`Ready`** | Đã hoàn tất mọi tính toán. Unit có thể sử dụng để di chuyển. | `AvoidanceSystem` |

---

## 2. FlowFieldStatus (`IComponentData`)
Đây là Component lưu trữ trạng thái hiện tại của một Flow Field.
- **`Value`**: Kiểu `enum FieldState` (độ rộng 1 byte để tiết kiệm bộ nhớ).

---

## 3. Tầm quan trọng
Nhờ hệ thống trạng thái này, chúng ta có thể:
- **Xử lý phi tập trung**: Các hệ thống khác nhau chỉ quan tâm đến các thực thể ở trạng thái mà chúng chịu trách nhiệm.
- **Tránh Jitter**: Unit sẽ không cố gắng di chuyển theo một bản đồ đang tính toán dở dang, tránh việc Unit bị xoay vòng hoặc đứng yên vô nghĩa.

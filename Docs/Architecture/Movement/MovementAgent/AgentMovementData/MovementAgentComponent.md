# MovementAgentComponent.cs

File này định nghĩa các thành phần dữ liệu cốt lỗi (`IComponentData`) cho hệ thống di chuyển của Agent. Đây là "bộ nhớ" của Unit, lưu trữ trạng thái, mục tiêu và các cấu hình điều hướng.

---

## 1. TargetResolutionKind & MovementAgentComponent

### Enum `TargetResolutionKind` (`byte`)
Phân loại kết quả phân giải (resolve) mục tiêu di chuyển của Unit:
- `None = 0`: Chưa được resolve hoặc mục tiêu không hợp lệ (ngoài bản đồ / bị bao vây kín).
- `Direct = 1`: Ô click nằm trong lưới và đi được trực tiếp (`cost < 255`, không thuộc công trình).
- `BuildingBlockage = 2`: Ô click nằm trong phạm vi ô lưới của công trình (`BlockageData`).
- `NaturalResolved = 3`: Ô click là vật cản tự nhiên (`cost ≥ 255`, không thuộc công trình) và đã được dời sang ô walkable gần nhất theo 4 hướng.

### Struct `MovementAgentComponent`
Thành phần chính lưu trữ thông tin về đích đến, trạng thái phân giải mục tiêu và thực thể đường đi (Flow Field).

| Trường | Giải thích |
| :--- | :--- |
| `speed` | Tốc độ di chuyển cơ bản của Unit. |
| `hastarget` | Flag cho biết Unit có đang trong trạng thái có mục tiêu hay không. |
| `FieldEntity` | Thực thể chứa Flow Field mà Unit đang bám theo. |
| `currentworldtarget` | Tọa độ đích đến gốc mà người chơi đã click (giữ nguyên để xoay mặt và tính đội hình). |
| `realTarget` | Tọa độ đích điều hướng thực tế ở frame hiện tại (tâm ô resolved, điểm chu vi công trình, hoặc `IslandSeed` khi đi liên đảo). |
| `navigationTargetCell` | Ô lưới đích đã được resolve dùng cho Flow Field (`FlowField.targetcell`). |
| `targetResolutionGeneration` | Thế hệ `grid.generation` tại thời điểm resolve mục tiêu gần nhất. |
| `targetResolutionKind` | Loại kết quả resolve (`None`, `Direct`, `BuildingBlockage`, `NaturalResolved`). |
| `slotTarget` | Tọa độ vị trí cụ thể mà Unit cần đứng trong đội hình. |
| `useSlotTarget` | Nếu `true`, Unit sẽ bỏ qua Flow Field để lái trực tiếp vào `slotTarget`. |
| `velocity` | Vận tốc hiện tại của Unit (dùng để nội suy mượt mà). |
| `lookAtPoint` | Điểm mà Unit sẽ hướng mặt về sau khi đã dừng hẳn tại đích. |

---

## 2. MovementAgentAvoidanceComponent
Dữ liệu phục vụ cho việc né tránh va chạm (Local Avoidance) và Context Steering.

- `radius`: Bán kính vật lý của Unit.
- `avoidanceForce`: Hướng đẩy dự kiến từ hệ thống né tránh.
- `lastAvoidDir`: Hướng né tránh của khung hình trước (Dùng để chống rung - Anti-Jitter).
- `separationForce`: Lực đẩy "cứng" khi các Unit đã thực sự chồng lấn lên nhau.
- `IsStatic`: Nếu `true`, Unit được coi là vật cản tĩnh (đã đứng yên tại chỗ).
- `avoidTimer`: Thời gian giữ hướng né tránh hiện tại để tránh đổi hướng quá nhanh.

---

## 3. MovementSteeringComponent
Lưu trữ các tham số điều hướng và trạng thái kẹt (Stuck).

- `arrivalRadius`: Bán kính bắt đầu giảm tốc khi gần đến đích.
- `formationRange`: Khoảng cách mà Unit bắt đầu chuyển sang chế độ dàn hàng đội hình.
- `isSettled`: Đã ổn định vị trí tại đích hay chưa.
- `stuckTime`: Tổng thời gian Unit không di chuyển đáng kể khi đang có mục tiêu.
- `lastPosition`: Vị trí của Unit ở khung hình trước để tính toán độ dời.

---

## 4. Context Steering Buffers
Các Buffer dùng để tính toán hướng đi tối ưu dựa trên Interest (Sở thích) và Danger (Nguy hiểm).

- `ContextMapElement`: Lưu trữ giá trị `Interest` và `Danger` cho từng tia hướng (thường là 16 tia).
- `ContextHistoryElement`: Lưu trữ lịch sử `Interest` để áp dụng bộ lọc EMA (Exponential Moving Average) làm mượt hướng.

---

## 5. TargetChangeRequest
Một "Tag Component" được thêm vào khi Unit nhận lệnh di chuyển mới, kích hoạt việc tính toán lại đội hình và đường đi.

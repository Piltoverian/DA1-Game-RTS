# Hướng Dẫn Đọc Code & Flow Hệ Thống Movement Agent

Tài liệu này được viết ngắn gọn, đúng trọng tâm để bạn nắm lại dự án ngay lập tức sau một thời gian dài không đụng tới. Dưới đây là giải thích luồng chạy thực tế và cách tìm đúng file để đọc.

---

## 1. Code chạy ra làm sao? (Execution Pipeline)

Hệ thống Movement hoạt động hoàn toàn tự động dựa trên ECS (Entity Component System). Khi bạn click chuột ra lệnh di chuyển (ví dụ: Worker đi hái khoáng), code sẽ chạy theo chuỗi tuần tự sau:

1. **Nhận lệnh (Command Bridge):**
   - Các hệ thống bên ngoài (như `WorkerGatherSystem`) gọi `MoveOverride` để báo cho Agent biết nó cần đi đâu. Khi có mục tiêu mới, component `TargetChangeRequest` được gắn vào Unit.
   - *File liên quan:* `MovementAgentPathRequestSystem.cs`, `MoveOverrideSystem.cs`

2. **Dàn trận (Formation):**
   - **Chỉ chạy 1 lần duy nhất lúc xuất phát.**
   - Quét tất cả Unit đang có lệnh di chuyển tới cùng 1 đích. Sau đó, nó sinh ra một mảng các vị trí (Slot) xếp theo hình vuông hoặc tròn xung quanh đích đến. Nó có cơ chế `IsValidSlot` để chủ động bỏ qua các tọa độ đè lên tòa nhà/vật cản (`cost >= 250`).
   - Dùng thuật toán **Greedy** để chia phần. Mỗi Unit sẽ ôm một cái đích phụ này (`slotTarget`) để khi tới gần đích thật thì tách ra chiếm chỗ.
   - *File liên quan:* `MovementAgentGroupFormationSystem.cs`

3. **Tìm đường vĩ mô (Flow Field):**
   - **Chỉ chạy 1 lần lúc xuất phát.**
   - Tìm trong `Cache` xem đã có lưới (Flow Field) nào chỉ tới đích đó chưa, nếu chưa thì báo `IntegrationFieldSystem` tính lưới mới để lách qua các chướng ngại vật tĩnh (Tường, Vách đá).
   - *File liên quan:* `FlowFieldAssignmentSystem.cs`

4. **Tính hướng đi tối ưu (Targeting):**
   - **Chạy liên tục mỗi Frame.**
   - Đọc vector từ lưới Flow Field để biết cần rẽ hướng nào qua chướng ngại vật.
   - Khi tới gần đích (`formationRange`), nó bỏ Flow Field và chuyển sang đi thẳng vào `slotTarget` đã được chia phần ở Bước 2.
   - *File liên quan:* `MovementAgentTargetSystem.cs`

5. **Né tránh đám đông (Local Avoidance - ORCA):**
   - **Chạy liên tục mỗi Frame.**
   - Nhận "Vận tốc mong muốn" từ Bước 4, đem đối chiếu với tất cả hàng xóm (neighbor) trong bán kính gần.
   - Dùng thuật toán RVO2 (Reciprocal Velocity Obstacles) để bóp méo vận tốc sao cho các Unit không đụng nhau mà vẫn trượt lết được tới đích.
   - *File liên quan:* `MovementAgentORCASystem.cs`

6. **Áp dụng vật lý & Chống kẹt (Actuator):**
   - **Chạy liên tục mỗi Frame.**
   - Đẩy các Unit văng ra (Separation Force) nếu lỡ bị đè lấn lên nhau ở cùng 1 frame.
   - **Chống kẹt (Stuck Detection):** So sánh khoảng cách di chuyển vật lý của frame hiện tại với frame trước (`lastPosition`). Nếu thấy Unit lết quá chậm (do tông vào Building hoặc bị kẹt cứng), nó sẽ đếm giờ (`stuckTime`).
   - Khi kẹt quá lâu, nó bắt Unit đó dừng hẳn (`isSettled = true`). Lúc này `MoveOverrideSystem` sẽ ngắt lệnh di chuyển, báo cho hệ thống bên trên biết là "Tôi không lết thêm được nữa, coi như đã tới nơi".
   - *File liên quan:* `MovementAgentActuatorSystem.cs`

---

## 2. Cách đọc code như thế nào? (Where to look)

Để không bị ngợp, hãy tuân theo quy tắc thư mục sau, cần sửa phần nào thì chui vào đúng Folder phần đó:

### 2.1. Nơi chứa dữ liệu cốt lõi (Components)
👉 *Thư mục:* `AgentMovementData/`
- `MovementAgentComponent.cs`: Lưu vận tốc hiện tại, đích đến thực tế, và `slotTarget`.
- `MovementSteeringComponent`: Lưu trạng thái có bị kẹt không (`stuckTime`), có dừng hẳn chưa (`isSettled`), và lưu lại vị trí frame cũ (`lastPosition`) để tính độ dời vật lý.

### 2.2. Khi muốn sửa logic Né tránh / Lách nhau
👉 *Thư mục:* `LocalAvoidance/`
- Nếu muốn chỉnh sửa thuật toán né nhau -> Đọc `Avoidance/MovementAgentORCASystem.cs` và file toán học `Helpers/ORCAMath.cs`.
- Nếu thấy quân bị đè lên nhau, kẹt vào mép nhà mãi không thoát, hoặc nhận diện nhầm "isSettled" -> Đọc `Actuation/MovementAgentActuatorSystem.cs` (tập trung vào khối Anti-Deadlock).

### 2.3. Khi muốn sửa thuật toán xếp Đội hình (Formation)
👉 *Thư mục:* `LocalAvoidance/Formating/`
- Đọc `MovementAgentGroupFormationSystem.cs`.
- Xem các hàm `FindBoxSlotsWorld` và `FindCircleSlotsWorld` để biết cách nó đẻ ra mảng Slot xung quanh mục tiêu.
- Xem vòng lặp `while (itAssign.MoveNext())` ở hàm `OnUpdate` để hiểu cách nó chia Slot (thuật toán Greedy) cho từng Unit lúc bắt đầu nhận lệnh.

### 2.4. Khi code hệ thống khác (Harvesting, Combat) cần điều khiển Unit di chuyển
👉 *Thư mục:* `CommandBridge/`
- Tuyệt đối **KHÔNG** chọc thẳng thay đổi tọa độ `LocalTransform` hay thay đổi `MovementAgentComponent.velocity` của Unit.
- Để cấp lệnh di chuyển, hãy dùng Helper API: Gọi `MovementAgentAPI.SetTarget(...)`.
- Nếu hệ thống của bạn dùng ISystem (như `WorkerGatherSystem`), hãy Add/Enable component `MoveOverride` cho Unit. Hệ thống `MoveOverrideSystem` sẽ tự động cầu nối với Movement Agent. Tương tự, gọi `MovementAgentAPI.StopAgent(...)` hoặc Disable `MoveOverride` để ép Agent đứng lại.

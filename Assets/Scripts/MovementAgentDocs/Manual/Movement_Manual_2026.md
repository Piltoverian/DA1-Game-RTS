# Movement Agent - Cẩm Nang Kỹ Thuật (Cập nhật Mới Nhất)

Tài liệu này tổng hợp luồng xử lý (Flow) của hệ thống di chuyển (Movement Agent) trong dự án RTS, bao gồm cả những bản vá và nâng cấp thuật toán mới nhất nhằm chống kẹt, giữ đội hình và tương tác với Command System.

---

## 1. Flow Di Chuyển Hoàn Chỉnh (Movement Flow)

Khi người chơi (hoặc AI) chọn một đạo quân và click chuột phải vào bản đồ, quy trình sau sẽ diễn ra nghiêm ngặt theo từng System (chạy theo thứ tự Pipeline):

1. **`MovementAgentPathRequestSystem` (Phát hiện Lệnh):** 
   - Kiểm tra xem mục tiêu thế giới (`currentworldtarget`) có thay đổi hay không.
   - Nếu đổi, gắn thẻ component tạm thời: `TargetChangeRequest`.
   
2. **`FlowFieldAssignmentSystem` (Tìm đường Vĩ mô):**
   - Đọc `TargetChangeRequest` để gán FlowField (Lưới véc-tơ). 
   - Nó dùng `Cache` để kiểm tra xem đã có lưới nào chỉ tới đích đó chưa, nếu chưa thì báo `IntegrationFieldSystem` tính lưới mới.

3. **`MovementAgentGroupFormationSystem` (Xếp Đội Hình - Đã Nâng Cấp):**
   - Chạy đồng bộ (Single-thread) lúc vừa nhận lệnh.
   - **Thuật toán Slot-Centric Greedy:** Các Slot (điểm đứng trong Box/Circle) được sinh ra từ đích lùi dần ra ngoài. Slot ở tâm sẽ ưu tiên chọn các Agent đang đi đầu, Slot ở rìa sẽ "hứng" các Agent lẹt đẹt đi sau.
   - Điều này đảm bảo khi nguyên bầy tịnh tiến tới đích, chúng không bao giờ vắt chéo đường (Cross paths) của nhau.

4. **`TargetRequestCleanupSystem` (Late Update):**
   - Xóa `TargetChangeRequest` đi để dọn dẹp, đảm bảo FlowField và Formation chỉ tính 1 lần duy nhất lúc xuất phát.

5. **`MovementAgentTargetSystem` (Hòa trộn Hướng đi):**
   - Lấy vector tổng hợp từ `FlowField` (đường vòng qua chướng ngại) và `DirectVelocity` (đường kéo thẳng tới đích).
   - Xác định xem Agent đã tới gần đích (`formationRange`) chưa để bắt đầu chuyển hướng đi thẳng vào `slotTarget`.

6. **`MovementAgentORCASystem` (Né Tránh Động Dạng Vận Tốc):**
   - Sử dụng giải thuật RVO2 (Reciprocal Velocity Obstacles).
   - Tự động thay đổi vận tốc để lách qua nhau mà không mất hoàn toàn hướng đi tới đích. Dữ liệu mượt mà, nhưng không đảm bảo 100% chống kẹt nếu gặp chướng ngại vật cứng.

7. **`MovementAgentActuatorSystem` (Vật lý & Anti-Deadlock - Đã Nâng Cấp):**
   - Cộng dồn lực đẩy cứng (Separation Force) để không cho 2 Unit đè lên nhau.
   - **Đếm kẹt vật lý (Stuck Detection):** Tính toán *độ dời vật lý thực tế* so với frame trước (`lastPosition`). Dù vận tốc ORCA có cao, nhưng nếu Agent bị chặn vật lý (chỉ lết được < 10% tốc độ thực), `stuckTime` sẽ tăng. 
   - Khi `stuckTime` vượt quá các mốc thời gian (1.0s, 1.5s, 2.5s), Agent sẽ tự động bị neo lại (`isSettled = true`) và ngừng cố gắng chen lấn.

---

## 2. Giao tiếp với Command & Economy (Lệnh Cấp Cao)

Hệ thống Movement hoạt động độc lập như một cỗ máy vật lý. Các lệnh cấp cao (ví dụ: Worker đi hái tài nguyên) tương tác với Movement thông qua thành phần **MoveOverride**.

* **`MoveOverrideSystem`**: Chèn ngang (Override) mục tiêu của Agent. Khi được kích hoạt, Agent tạm thời không tuân theo các lệnh click chuột phổ thông mà sẽ đi theo tọa độ của `MoveOverride`.
* **Cải tiến ECB**: `MoveOverrideSystem` hiện dùng `Allocator.Temp` (chạy tức thời) thay vì `BeginSimulationECB` để tránh crash khi Entity bị tiêu diệt giữa chừng (combat death).

### Worker Gather Flow (Cải Tiến Fix Kẹt):
Các System kinh tế (VD: `WorkerGatherSystem`) dùng chung cơ chế sau:
- Yêu cầu di chuyển tới mỏ quặng / Depot bằng cách bật `MoveOverride`.
- Kiểm tra khoảng cách: Hệ thống bù trừ thêm *Kích thước chân đế (FootprintSize)* của các Tòa nhà `BuildingData` (VD: +3.0 units thay vì chỉ đếm khoảng cách tới tâm).
- Nhờ thế, khi Worker tông vào thành tòa nhà và bị Actuator báo `isSettled`, hệ thống Kinh tế lập tức ghi nhận giao dịch thành công (Gather/Drop) mà không gây ra vòng lặp vô tận (Infinite loop issuing Move Commands).

---

## 3. Kiến Trúc Dữ Liệu (Core Components)

Để dễ debug, bạn cần nắm rõ 3 Component lõi cấu thành nên 1 Movement Agent:

1. **`MovementAgentComponent`**: Dữ liệu sống còn (`velocity`, `speed`, `slotTarget`, `hastarget`).
2. **`MovementSteeringComponent`**: Dữ liệu điều hướng và chống kẹt (`stuckTime`, `isSettled`, `lastPosition`).
3. **`MovementAgentAvoidanceComponent`**: Dữ liệu không gian (`radius`, `separationForce`, số lượng hàng xóm lân cận).

## 4. Troubleshooting (Xử lý sự cố thường gặp)

- **Agent đứng xoay vòng không đi:** Hãy bật Debugging `MovementAgentDebugSystem` lên. Kiểm tra vạch màu Tím (FlowField) có bị chặn bởi đảo (Island) nào không.
- **Lỗi văng Exception khi di chuyển rớt FPS:** Kiểm tra lại bộ cài `UnitSpatialSystem`. Nếu Buckets (bảng băm) bị quá tải khi spawn quá nhiều quân, nó có thể throw lỗi Burst.
- **Quân đi thành cục đè lên nhau:** Tính năng ORCA Separation đang không đủ mạnh. Cần tăng trọng số của `SeparationForce` trong `ActuatorSystem`.

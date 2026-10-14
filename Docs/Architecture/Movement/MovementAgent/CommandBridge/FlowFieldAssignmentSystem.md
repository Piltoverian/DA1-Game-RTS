# FlowFieldAssignmentSystem.cs

Hệ thống này chịu trách nhiệm xử lý các `TargetChangeRequest` và thực hiện việc gán "đường đi thực tế" cho Unit thông qua trường hướng Field.

---

## 1. Quy trình xử lý (Quy trình)
Hệ thống sử dụng `m_RequestQuery` (`WithAll<TargetChangeRequest, MovementAgentComponent, MovementSteeringComponent>()`) và chỉ chạy khi có ít nhất một Unit đang mang `TargetChangeRequest`, tránh cấp phát `NativeArray`/`ECB` dư thừa ở các khung hình bình thường.

Khi một Unit có `TargetChangeRequest`, hệ thống thực hiện các bước sau:

### Bước 0: Phân giải mục tiêu một lần (mục tiêu Resolution)
Gọi `NaturalBlockedTargetResolver.TryResolveTarget` một lần duy nhất cho mỗi request:
- **Nếu mục tiêu không hợp lệ (`false` - ngoài bản đồ hoặc bị bịt kín cả 4 hướng)**:
  - Gọi `FlowFieldHelper.AssignFieldToMoveComponent(..., Entity.Null, ...)` để ghi nhận giảm ref-số lượng (`-1`) của `FieldEntity` cũ đúng 1 lần thông qua `refCountDeltas`.
  - Đồng bộ `MovementAgentFieldCleanUpData.FieldEntity = Entity.Null` (nếu có) để hệ thống dọn dẹp không trừ ref-số lượng lần thứ hai.
  - Đặt `hastarget = false`, `navigationTargetCell = targetCell`, `targetResolutionGeneration = grid.generation`, `targetResolutionKind = TargetResolutionKind.None`, `velocity = float3.zero`, `preferredVelocity = float3.zero` và `isSettled = true`.
- **Nếu mục tiêu hợp lệ (`true`)**:
  - Thu được `targetCell` (`resolvedCell`), `resolvedWorldTarget` và `resolutionKind` (`Direct`, `BuildingBlockage` hoặc `NaturalResolved`).

### Bước 1: Tìm kiếm trong bộ nhớ đệm
Sử dụng `FlowFieldCacheHelper.TryGetFieldFromCache(targetCell, ...)` để xem đã có đường đi nào dẫn đến ô lưới đích đã phân giải (`targetCell`) chưa. Các Unit click vào cùng một cụm vật cản tự nhiên và phân giải ra cùng `targetCell` sẽ dùng chung một trường hướng Field trong bộ nhớ đệm.

### Bước 2: Tìm kiếm toàn cục (phương án dự phòng)
Nếu trong bộ nhớ đệm không có, hệ thống quét qua mảng thực thể `FlowField` hiện có để tìm thực thể có `targetcell == targetCell` và `GridGeneration == grid.generation`.

### Bước 3: Khởi tạo đường đi mới
Nếu cả hai bước trên đều không tìm thấy, hệ thống gọi `FlowFieldCacheHelper.CreateFlowField` với `targetCell` và `resolvedWorldTarget` để tạo mới thực thể trường hướng Field và đăng ký vào bộ nhớ đệm.

### Bước 4: Gán và Lưu trạng thái Resolution
- Gọi `FlowFieldHelper.AssignFieldToMoveComponent` để cập nhật `FieldEntity` và cộng dồn thay đổi ref-số lượng vào `refCountDeltas`.
- Giữ nguyên `move.currentworldtarget = rawWorldTarget` (để phục vụ xoay mặt và xếp đội hình).
- Lưu trạng thái đã phân giải vào `MovementAgentComponent`:
  - `move.navigationTargetCell = targetCell`
  - `move.targetResolutionGeneration = grid.generation`
  - `move.targetResolutionKind = resolutionKind`
  - `move.realTarget = resolvedWorldTarget`
- Đặt lại `stuckTime = 0`, `isSettled = false`.

---

## 2. Lưu ý về Dọn dẹp (dọn dẹp)
Khác với phiên bản cũ, hệ thống này **KHÔNG CÒN** tự tay xóa thành phần `TargetChangeRequest` sau khi gán đường đi xong.
- Request được giữ tới cuối bước cố định bước mô phỏng và dọn bởi TargetRequestCleanupSystem. Runtime hiện không có GroupFormationSystem; giữ request không phải để tính vị trí đội hình.
- Việc dọn dẹp được giao lại cho `TargetRequestCleanupSystem` chạy cuối `FixedStepSimulationSystemGroup`, sau assignment và bộ thực thi chuyển động. Request tạo ở Simulation phải tồn tại tới bước cố định bước mô phỏng xử lý nó; xóa ở LateSimulation có thể làm mất lệnh mới.

---

## 3. Thứ tự thực thi (Execution thứ tự)
Hệ thống này chạy ngay sau `MovementAgentPathRequestSystem` và trước `IntegrationFieldSystem`.
- **Lý do**: Để đảm bảo rằng ngay khi một yêu cầu được tạo ra, nó sẽ có một `FieldEntity` (dù là rỗng/đang chờ tính toán) trước khi hệ thống tính toán lưới bắt đầu làm việc.

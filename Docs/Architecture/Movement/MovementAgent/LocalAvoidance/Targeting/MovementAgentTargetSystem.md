# MovementAgentTargetSystem.cs

Hệ thống này đóng vai trò là "bộ não" điều hướng, quyết định xem Unit nên đi theo hướng nào của Flow Field hay lái thẳng vào vị trí đội hình (Slot).

---

## 1. Tái tạo Mục tiêu Cơ sở (`baseGoal`) & Đồng bộ hóa Đảo (Island Sync)
Nếu `!move.hastarget` hoặc `move.targetResolutionKind == TargetResolutionKind.None`, hệ thống bỏ qua ngay lập tức.

Mỗi frame, hệ thống **không** dùng lại `move.realTarget` của frame trước (vì `realTarget` có thể đã bị ghi đè tạm thời bởi `IslandSeed` hoặc điểm chu vi công trình), mà tái tạo đích điều hướng từ trạng thái đã lưu:

1. **Nhánh Công trình (`foundBuilding == true` qua `NaturalBlockedTargetResolver.TryGetBlockageGridBounds`)**:
   - Quét chu vi hình chữ nhật của công trình `[bMinGrid.x - 1 .. bMaxGrid.x + 1, bMinGrid.y - 1 .. bMaxGrid.y + 1]` để chọn ô walkable trên cùng đảo có khoảng cách kết hợp (`distToAgent + distToClick × 0.25f`) nhỏ nhất làm `islandGoal`.
2. **Nhánh Không phải Công trình (hoặc Công trình đã bị phá hủy khi Unit đang đi tới)**:
   - Kiểm tra `NaturalBlockedTargetResolver.IsCellInBounds(move.navigationTargetCell, Grid)`. Nếu ngoài biên thì dừng Unit (`hastarget = false`, `isSettled = true`).
   - Tái tạo `baseGoal`:
     - Nếu `move.targetResolutionKind == TargetResolutionKind.NaturalResolved`: `baseGoal = GridHelper.GridToWorld(move.navigationTargetCell, Grid)` (tâm ô đã resolve).
     - Nếu `move.targetResolutionKind == TargetResolutionKind.Direct` (hoặc fallback khi `BuildingBlockage` không còn tìm thấy công trình): `baseGoal = move.currentworldtarget`.
   - **Đồng bộ hóa Đảo (Island Sync)**:
     - Kiểm tra `targetIsland` tại `move.navigationTargetCell`.
     - Nếu `myIsland != 0` và `myIsland != targetIsland`: tìm trong `IslandSeedLookup` hạt giống (`Seed`) của đảo hiện tại và đặt tạm `islandGoal = GridHelper.GridToWorld(seedGrid, Grid)`.
     - Khi Unit đã sang cùng đảo (`myIsland == targetIsland`), `islandGoal` tự động khôi phục về `baseGoal`.
3. Cuối cùng, ghi `move.realTarget = islandGoal`.

---

## 2. Quản lý Đội hình (Slot Formation) - Có kiểm tra hướng tiếp cận
Khi Unit tiến vào phạm vi `formationRange` (mặc định 25m):
- **Trước tiên, kiểm tra hướng FlowField:** Lấy `direction` (hướng đi tối ưu) tại ô lưới của Agent và so sánh với hướng từ Agent tới `slotTarget` bằng dot product.
  - **Dot > -0.2** (slot cùng chiều hoặc hơi lệch): Slot nằm trên đường tiếp cận tự nhiên → chấp nhận.
  - **Dot < -0.2** (slot ngược chiều): Slot nằm phía bên kia tòa nhà/chướng ngại vật → **bỏ qua slot**, tiếp tục đi theo FlowField tới cạnh gần nhất.
  - **Ngoại lệ:** Nếu Agent đã rất gần slot (< 3x `stoppingDistance`) thì luôn chấp nhận.
- Nếu qua được bước kiểm tra trên, mới so sánh `pathDist` vs `directDistToSlot` (đường đi vs đường chim bay) để quyết định chuyển sang lái trực tiếp tới slot.

---

## 3. Tính toán Vận tốc mong muốn (Desired Velocity)
Đây là phần cốt lõi của hệ thống steering, vận tốc được tính bằng cách hòa trộn (**Blending**):
- **`flowVelocity`**: Lấy từ Flow Field (giúp né vật cản tĩnh tốt).
- **`directVelocity`**: Hướng thẳng tới mục tiêu (giúp Unit dàn hàng vào Slot mượt mà).
- **Trọng số (`targetWeight`)**: Càng gần đích, Unit càng ưu tiên `directVelocity` để ổn định vị trí.

---

## 4. Kiểm tra Dừng (Arrival & Damping)
- **Hãm phanh**: Khi nằm trong `arrivalRadius`, vận tốc sẽ giảm dần theo tỉ lệ khoảng cách để Unit không bị dừng đột ngột.
- **Dừng hẳn**: Nếu cách đích ít hơn `stoppingDistance`, Unit sẽ xóa mục tiêu và chuyển sang trạng thái `isSettled`.

---

## 5. Theo dõi kẹt (Anti-Deadlock)
Hệ thống liên tục cập nhật `minDistanceToTarget`. Nếu trong một khoảng thời gian mà Unit không thể tiến gần đích hơn giá trị này, hệ thống sẽ tăng `stuckTime` (xử lý ở Actuator) để kích hoạt các biện pháp giải tỏa kẹt.

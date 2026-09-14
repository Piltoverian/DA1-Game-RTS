# MovementAgentTargetSystem.cs

Hệ thống này đóng vai trò là "bộ não" điều hướng, quyết định xem Unit nên đi theo hướng nào của Flow Field hay lái thẳng vào vị trí đội hình (Slot).

---

## 1. Đồng bộ hóa Đảo (Island Sync)
Nếu người chơi ra lệnh di chuyển sang một hòn đảo khác:
- Hệ thống sẽ tìm trong `IslandSeedLookup` để lấy tọa độ "hạt giống" (Seed) của hòn đảo hiện tại mà Unit đang đứng.
- `realTarget` sẽ tạm thời được đặt tại Seed này thay vì đích đến cuối cùng. Điều này đảm bảo Unit luôn đi đến điểm "bờ biển" gần đích nhất thay vì đi lung tung.

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

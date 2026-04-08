# MovementAgentTargetSystem.cs

Hệ thống này đóng vai trò là "bộ não" điều hướng, quyết định xem Unit nên đi theo hướng nào của Flow Field hay lái thẳng vào vị trí đội hình (Slot).

---

## 1. Đồng bộ hóa Đảo (Island Sync)
Nếu người chơi ra lệnh di chuyển sang một hòn đảo khác:
- Hệ thống sẽ tìm trong `IslandSeedLookup` để lấy tọa độ "hạt giống" (Seed) của hòn đảo hiện tại mà Unit đang đứng.
- `realTarget` sẽ tạm thời được đặt tại Seed này thay vì đích đến cuối cùng. Điều này đảm bảo Unit luôn đi đến điểm "bờ biển" gần đích nhất thay vì đi lung tung.

---

## 2. Quản lý Đội hình (Slot Formation)
Khi Unit tiến vào phạm vi `formationRange` (mặc định 25m):
- Hệ thống bắt đầu so sánh giữa việc đi theo Flow Field và đi thẳng tới `slotTarget`.
- Nếu khoảng cách đường đi (`pathDist`) không quá lơn so với đường chim bay tới Slot, Unit sẽ chuyển sang chế độ lái trực tiếp (`useSlotTarget = true`).

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

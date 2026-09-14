# MovementAgentActuatorSystem.cs

Đây là hệ thống cuối cùng trong chuỗi xử lý di chuyển (chạy sau ORCA), chịu trách nhiệm tổng hợp các lực, cập nhật vị trí thực tế của Unit và xử lý các tình huống bị kẹt (Stuck).

---

## 1. Safety Net: Lực đẩy vật lý (Separation Force)
Dù hệ thống có ORCA để né tránh trong tương lai, đôi khi các Unit vẫn bị chồng lấn (Hard Overlap) do chênh lệch vận tốc hoặc không gian hẹp. Lớp Separation Force giải quyết triệt để việc này:
- Lực này lấy trực tiếp từ `avoidance.separationForce` (tính toán ở ORCASystem).
- Nó đẩy dạt các Unit ra xa nhau ngay lập tức ở cấp độ Positional (Vị trí) trước cả khi áp dụng vận tốc mới, đảm bảo 100% không bao giờ đè lên nhau.

---

## 2. Xử lý kẹt vật lý (Anti-Deadlock / Stuck Detection)
Hệ thống sử dụng cơ chế đếm thời gian kẹt (Stuck Time) tinh vi dựa trên độ dời vật lý thực tế:
- **Theo dõi vị trí:** So sánh vị trí hiện tại so với `lastPosition` ở frame trước.
- **Tính toán quãng đường:** Nếu quãng đường di chuyển thực tế nhỏ hơn 10% so với lý thuyết (`move.speed * DeltaTime`), hệ thống nhận diện Unit đang bị chặn cứng bởi vật thể/tòa nhà, và tăng nhanh `stuckTime`. Nếu đang lách qua được, thời gian này tăng rất chậm.
- **Ngưỡng chịu đựng (Threshold):**
  - Bị bao vây bởi neighbor: 1.0 giây.
  - Đã vào tầm dàn đội hình (gần đích): 1.5 giây.
  - Còn ở xa đích: 2.5 giây.
- **Early Settle:** Khi vượt quá ngưỡng, Unit tự động bị neo lại (`isSettled = true`), triệt tiêu vận tốc thành 0. Động thái này báo cho hệ thống bên trên (ví dụ: `MoveOverrideSystem`, `WorkerGatherSystem`) biết rằng Unit không thể đi tiếp được nữa để xử lý logic tiếp theo.

---

## 3. Cập nhật Vị trí & Quay mặt (Motion & Rotation)
- **Vận tốc (Velocity):** Lấy trực tiếp từ `move.velocity` do ORCA tính toán.
- **Quay mặt:** Sử dụng `math.slerp` để xoay dần theo hướng vận tốc hiện tại. Chỉ xoay khi vận tốc đủ lớn (để tránh hiện tượng xoay mòng mòng khi đứng im).

---

## 4. Thứ tự thực thi
Chạy **SAU** `MovementAgentORCASystem` để lấy được vận tốc an toàn cuối cùng. Khâu này "chốt" mọi thông số xuống `LocalTransform` cho Unity render.

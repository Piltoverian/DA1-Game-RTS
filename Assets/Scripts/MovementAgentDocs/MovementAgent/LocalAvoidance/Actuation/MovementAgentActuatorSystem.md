# MovementAgentActuatorSystem.cs

Đây là hệ thống cuối cùng trong chuỗi xử lý di chuyển, chịu trách nhiệm áp dụng vận tốc đã tính toán vào vị trí thực tế của Unit và xử lý các tình huống bị kẹt (Stuck).

---

## 1. Hòa trộn Trọng số (Weighted Blending)
Thay vì chỉ cộng các vector hướng, hệ thống sử dụng trọng số để cân bằng giữa mục tiêu và né tránh:
- **`avoidWeight`**: Tỉ lệ thuận với số lượng hàng xóm xung quanh. Càng đông đúc, Unit càng ưu tiên hướng né tránh (`avoidDir`) hơn là hướng đích (`goalDir`).
- Kết quả là hướng di chuyển tự nhiên, không bị khựng lại khi gặp vật cản.

---

## 2. Xử lý kẹt (Anti-Deadlock)
Hệ thống có 2 cơ chế để giải quyết việc Unit bị kẹt:

### Cơ chế 1: Early Settle (Dừng sớm)
Nếu Unit không thể tiến gần đích hơn trong một khoảng thời gian (vượt quá `stuckThreshold`), hệ thống sẽ coi như Unit đã "đến đích" và buộc nó dừng lại. Việc này giúp giải tỏa các nút thắt cổ chai nơi Unit cứ cố chen lấn vô ích.

### Cơ chế 2: Nudge (Đẩy nhẹ)
Nếu Unit bị kẹt hơn 0.5 giây, một lực đẩy ngẫu nhiên nhỏ sẽ được áp dụng. Điều này giống như việc Unit "nhích" sang một bên để tìm khe hở thoát ra.

---

## 3. Lực đẩy vật lý (Separation Force)
Áp dụng lực `separationForce` từ hệ thống né tránh. Lực này mạnh hơn vận tốc thông thường và có tác dụng đẩy các Unit đang chồng lấn nhau ra xa ngay lập tức, đảm bảo tính thẩm mỹ và logic vật lý.

---

## 4. Cập nhật Vị trí & Quay mặt (Motion & Rotation)
- **Làm mượt vận tốc**: Sử dụng `math.lerp` với `lerpFactor` tùy biến (nhanh hơn khi ở xa, chậm hơn khi gần đích) để Unit tăng tốc và hãm phanh êm ái.
- **Quay mặt**: Sử dụng `math.slerp` để Unit xoay hướng nhìn dần dần về phía vector vận tốc, tránh hiện tượng Unit xoay 180 độ tức thời gây đau mắt.

---

## 5. Thứ tự thực thi
Hệ thống này chạy **SAU** `MovementAgentTargetSystem` và `MovementAgentAvoidanceSystem` để đảm bảo nó có dữ liệu vận tốc và né tránh mới nhất cho khung hình hiện tại.

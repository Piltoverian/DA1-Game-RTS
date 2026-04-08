# UnitMovementMath.cs

Lớp này chứa toàn bộ các công thức toán học "xương sống" cho hệ thống di chuyển, từ việc nội suy hướng đi cho đến tính toán mức độ nguy hiểm của các tia né tránh. Tất cả các hàm đều được tối ưu bằng `[BurstCompile]` để chạy trên nền Job System với hiệu suất cao nhất.

---

## 1. Toán học Flow Field (Flow Field Math)

### 1.1 CalculateFlowVelocity (Bilinear Interpolation)
Đây là hàm quan trọng nhất để giúp Unit di chuyển mượt mà. Thay vì chỉ đi theo hướng của ô lưới hiện tại, hệ tâm sẽ lấy 4 ô lân cận và thực hiện nội suy hướng dựa trên vị trí chính xác của Unit.

- **Cơ chế**: Sử dụng hàm `math.lerp` để hòa trộn hướng giữa 4 ô (00, 10, 01, 11).
- **Lợi ích**: Unit sẽ rẽ hướng một cách êm ái, tránh hiện tượng "đi giật cục" khi bước sang ô mới.

---

## 2. Toán học Né tránh (Avoidance Math)

### 2.1 CalculateGridGradient (Né vật cản Grid)
Hàm này biến các ô vật cản (Cost >= 250) thành các nguồn "lực đẩy".
- **Lực đẩy Gradient**: Vector đẩy hướng ra xa vật cản, tỉ lệ nghịch với bình phương khoảng cách (`1 / distSq`).
- **Ứng dụng**: Giúp Unit "cảm nhận" được tường từ xa và chủ động dạt ra thay vì đợi đến khi va chạm vật lý mới xử lý.

### 2.2 CalculateDanger (Time-To-Collision Lite)
Tính toán mức độ nguy hiểm của một tia hướng trong Context Steering. Nó không chỉ dựa vào khoảng cách (`dist`) mà còn dựa vào **vận tốc tương đối** giữa hai Unit.

- **Consensus (Sự đồng thuận)**:
  - Nếu hai Unit đi cùng hướng (Consensus > 0.8): `danger *= 0.1f`. Chúng có thể nén chặt lại để tạo thành bầy đàn đi song song.
  - Nếu hai Unit đối đầu (Consensus < -0.5): `danger *= 1.5f`. Hệ thống sẽ tăng mức độ nguy hiểm để bắt buộc chúng phải né nhau từ sớm.

---

## 3. Toán học Tổng hợp (Solver Math)

### 3.1 CalculateQuadraticOffset (Nội suy Parabol)
Sau khi Context Steering chọn được tia có Interest cao nhất (ví dụ tia số 4), hướng thực tế có thể nằm giữa tia số 4 và tia số 5.
- **Cơ chế**: Sử dụng nội suy Parabol (`vM`, `vC`, `vP`) để tìm điểm cực đại thực sự.
- **Lợi ích**: Tăng độ phân giải ảo cho hệ thống né tránh, giúp hướng di chuyển của Unit không bị giới hạn cứng vào 16 hướng cơ bản.

---

## 4. Tối ưu hiệu năng
Tất cả các hàm trong file này đều sử dụng:
- Kiểu dữ liệu `float2`, `float3` từ thư viện `Unity.Mathematics` (SIMD optimized).
- Không cấp phát bộ nhớ động (`No allocations`).
- Tương thích hoàn toàn với `IJobEntity`.

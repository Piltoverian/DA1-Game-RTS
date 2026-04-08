# MovementAgentAvoidanceSystem.cs

Đây là hệ thống phức tạp nhất trong bộ điều khiển di chuyển, sử dụng thuật toán **Context Steering** kết hợp với **Velocity-Aware Danger** để giúp Unit né tránh nhau và vật cản một cách mượt mà, tự nhiên.

---

## 1. Quy trình xử lý Context Steering (6 Bước)

### Bước 1: Khởi tạo (Clear & Initialize)
Hệ thống xóa dữ liệu cũ trong `contextMap` (thường có 16 tia hướng). Mỗi tia sẽ bắt đầu với `Interest = 0` và `Danger = 0`.

### Bước 2: Tạo Sở thích (Interest Generation)
Dựa trên mục tiêu lấy từ `TargetSystem`, hệ thống tính toán vector hướng tới đích. Các tia hướng trùng với hướng đích sẽ có `Interest` cao nhất (sử dụng hàm `dot product`).

### Bước 3: Đánh giá Nguy hiểm (Danger Evaluation)
Sử dụng `BucketMap` để tìm các Unit hàng xóm xung quanh:
- **Velocity-aware**: Nếu hàng xóm đang đi cùng hướng (**Consensus** cao), hệ thống sẽ giảm Danger để các Unit có thể đi sát nhau (nén quân). Nếu đối đầu, Danger sẽ tăng cao để né sớm.
- **Static Check**: Các Unit đã đứng yên được coi là vật cản "cứng" và có mức độ nguy hiểm cao hơn.

### Bước 4: Né vật cản Grid (Grid Obstacle Danger)
Sử dụng **Gradient** từ bản đồ Grid. Nếu một tia hướng đâm thẳng vào tường, giá trị `Danger` của tia đó sẽ bị đẩy lên tối đa. Điều này giúp Unit "nhìn thấy" tường và lượn vòng qua thay vì húc đầu vào.

### Bước 5: Giải quyết (Resolve & Solver)
- **Danger Masking**: Loại bỏ các tia có Danger quá cao (`Interest = 0`).
- **EMA Hysteresis**: Sử dụng bộ lọc làm mượt theo thời gian để hướng di chuyển không bị thay đổi đột ngột (chống rung - Anti-Jitter).

### Bước 6: Nội suy Parabol (Interpolation)
Sử dụng hàm `CalculateQuadraticOffset` để tìm ra hướng tối ưu nằm giữa các tia. Điều này giúp Unit có thể di chuyển theo bất kỳ góc độ nào thay vì chỉ giới hạn trong 16 hướng cố định.

---

## 2. Các lực bổ trợ (Lực đẩy vật lý)
Ngoài hướng di chuyển, hệ thống còn tính toán `separationForce`. Đây là lực đẩy "cứng" chỉ xuất hiện khi các Unit đã thực sự chồng lấn lên nhau, giúp chúng tách nhau ra một cách nhanh chóng.

---

## 3. Hiệu năng tích hợp
- **Spatial Indexing**: Nhờ `BucketMap`, hệ thống chỉ kiểm tra các Unit ở các ô lưới lân cận, giúp duy trì hiệu suất ổn định ngay cả khi có hàng nghìn Unit trên màn hình.
- **Burst Compiler**: Toàn bộ Job được biên dịch sang mã máy tối ưu, tận dụng tập lệnh SIMD.

# MovementAgentDebugSystem.cs

Hệ thống này cung cấp khả năng quan sát trực tiếp "suy nghĩ" của mỗi Unit trong quá trình di chuyển. Nó giúp lập trình viên biết tại sao Unit lại rẽ trái, rẽ phải hoặc tại sao nó bị đứng yên.

---

## 1. Các thành phần hiển thị (hình ảnh Elements)

Khi các tùy chọn được bật trong cấu hình, hệ thống sẽ vẽ:
- **Vận tốc (Màu Trắng)**: Vận tốc thực tế mà Unit đang di chuyển.
- **Hướng mong muốn (Màu Xanh lá)**: Hướng mà Unit muốn đi (tới đích hoặc tới hạt giống của đảo) trước khi né tránh.
- **Tia Context Steering (Màu Xanh dương & Đỏ)**:
    - **Xanh dương (Interest)**: Các tia hướng mà Unit "thích" vì nó dẫn tới đích.
    - **Đỏ (Danger)**: Các tia hướng bị coi là "nguy hiểm" do có Unit khác hoặc vật cản.
- **Đường mục tiêu**:
    - **Màu Vàng Cam**: Nối từ Unit tới vị trí vị trí trong đội hình.
    - **Màu Tím**: Nối từ Unit tới đích thực tế trên đảo (`realTarget`).
- **Vòng tròn (Màu Xám)**: Thể hiện bán kính vật lý của Unit.

---

## 2. Đặc điểm kỹ thuật
- **Chế độ Main luồng**: Hệ thống sử dụng hàm `UnityEngine.Debug.DrawLine` và `DrawRay`. Do đó, nó chạy trên luồng chính (Main luồng) thay vì Job hệ thống để có thể tương tác với API của Unity Editor.
- **Presentation Group**: Hệ thống chạy trong `PresentationSystemGroup`, đảm bảo việc vẽ diễn ra sau khi mọi tính toán xử lý logic di chuyển đã hoàn tất.
- **Tối ưu**: Chỉ vẽ khi có `MovementAgentDebugConfig` và các flag tương ứng được bật, tránh gây tốn tài nguyên khi không cần gỡ lỗi.

---

## 3. Cách sử dụng để gỡ lỗi
- Nếu Unit không di chuyển, hãy bật **Interest Rays**. Nếu không có tia xanh, nghĩa là Unit không tìm thấy đường đi tới đích.
- Nếu Unit đi xuyên qua nhau, hãy bật **Danger Rays**. Nếu không có tia đỏ khi Unit đứng sát nhau, nghĩa là hệ thống Spatial hoặc Avoidance đang có vấn đề.
- Nếu Unit dừng quá xa đích, hãy bật **mục tiêu Lines** để xem `slotTarget` có đang bị đặt sai vị trí không.

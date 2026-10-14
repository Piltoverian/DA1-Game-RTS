# MovementAgent: tổng quan hiện tại

Cập nhật 2026-10-12 theo mã khi chạy. lối chơi dùng XZ; LocalTransform.Y phục vụ địa hình height/render.

## Luồng một lệnh

1. API/MoveOverride nhận lệnh; PathRequest yêu cầu gán trường khi cần.
2. Assignment phân giải mục tiêu và tìm bộ nhớ đệm; nếu chưa có thì tạo trường Requested.
3. GridNeighborMaskSystem chuẩn bị mặt nạ 8 hướng hợp lệ dùng chung khi kết nối lưới đổi; Integration dùng mặt nạ để lan chi phí bằng Dial ngăn hàng đợi sau khi reset/chọn hạt giống.
4. Direction tạo hướng từ bestcost, chuyển trường Ready.
5. mục tiêu chọn realTarget, đọc hướng của ô chứa lính và hòa trộn hướng trực tiếp khi đoạn tới đích clear.
6. ORCA xử lý hàng xóm và tách hàng xóm.
7. Actuator áp separation/velocity qua MoveAndSlide, quay mặt và bật PendingHeight khi XZ đổi.
8. Height đặt Y = địa hình độ cao + điểm gốc độ lệch rồi tắt đang chờ.

Navigation/avoidance chạy theo bước cố định bước mô phỏng. Height còn có đạt Simulation trước TransformSystemGroup cho sinh đối tượng muộn. Một khung hình dựng hình có thể chứa nhiều bước cố định bước mô phỏng.

Thư mục khi chạy hiện không có MovementAgentGroupFormationSystem hoặc Context Steering. Trang đội hình lỗi thời đã loại trong lượt dọn 2026-10-12.

## Đọc tiếp

- [Luồng mã](Logic_Explainer.md)
- [ngăn giải thích bằng ví dụ](Field/IntegrationField/IntegrationFieldSystem.md)
- [hệ thống tính mặt nạ trước Integration](Grid/GridNeighborMaskSystem.md)
- [Direction](Field/FlowField/FlowDirectionSystem.md)
- [Components](AgentMovementData/MovementAgentComponent.md)
- [Authoring và diameterCells](AgentMovementData/MovementAgentAuthoring.md)
- [trường hướng lấy mẫu](Helpers/UnitMovementMath.md)
- [Actuator](LocalAvoidance/Actuation/MovementAgentActuatorSystem.md)
- [Bảo trì và hiệu năng](Maintenance_Guide.md)

ngăn không gian của UnitSpatialSystem nhóm lính theo vị trí để tìm hàng xóm. ngăn chi phí trong Integration nhóm nút theo chi phí; đây là hai mục đích khác nhau.

ngăn đã khớp FIFO trên 1000 trường tổng hợp ×3 lần với adapter được quản lý. Chưa ghi nhận kết quả Unity/Burst sau thay đổi; người dùng đang tự chạy thử.

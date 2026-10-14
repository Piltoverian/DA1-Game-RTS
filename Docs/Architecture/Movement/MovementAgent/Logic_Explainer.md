# MovementAgent: đọc mã theo luồng

Cập nhật 2026-10-12. [Tổng quan](Architecture_Overview.md) mô tả luồng xử lý hiện tại.

## Lệnh và trường

lối chơi dùng MovementAgentAPI hoặc MoveOverride. PathRequest/Assignment phân giải navigationTargetCell và dùng bộ nhớ đệm. currentworldtarget là đích yêu cầu; realTarget có thể là đích trực tiếp, ô resolved, viền nhà hoặc hạt giống cùng vùng liên thông.

Field tính khi tạo mới hoặc lưới sinh dữ liệu đổi, không phải chỉ một lần lúc xuất phát.

## FIFO cũ và ngăn hiện tại

FIFO cũ đưa tất cả hạt giống chi phí 0 vào cùng hàng đợi. Lấy ô ra, xét hàng xóm; khi chi phí mới tốt hơn thì cập nhật bestcost và thêm vào hàng đợi hàng xóm. Ô đã xử lý có thể được thêm vào hàng đợi lại, nhiều mục cùng một ô có thể cùng chờ. Khi lấy ra, FIFO đọc bestcost mới nhất rồi xét hàng xóm.

ngăn giữ quy tắc cập nhật đó nhưng lấy khay chi phí thấp nhất trước. Mục lưu chi phí lúc thêm vào hàng đợi; nếu khác bestcost hiện tại thì bỏ mục cũ. Ví dụ A/80 được thay bằng A/35: xử lý A/35 trước rồi bỏ A/80. [Giải thích 269 khay](Field/IntegrationField/IntegrationFieldSystem.md).

## Chuyển động

mục tiêu chạy bước cố định, dùng hướng của ô chứa lính; không song tuyến tính. CalculateSafeFlowVelocity thử hướng về tâm ô kế hoặc tâm ô hiện tại nếu đường trường hướng từ vị trí lệch tâm có nguy cơ đâm góc.

Gần realTarget, hòa trộn trường hướng với hướng trực tiếp chỉ khi ClearSegment xác nhận đi được. arrivalRadius là vùng hãm; stoppingDistance là ngưỡng đến đích.

ORCA tính velocity/separation. Actuator áp cả hai qua MoveAndSlide trên XZ. Kẹt xa đích giữ hastarget; chỉ settle vì crowd khi gần realTarget, có hàng xóm, ít tiến triển >0,5 s và đoạn tới đích clear.

isSettled có thể true khi lính đang chờ và hastarget vẫn true. Đọc hastarget/stopReason để xác định lệnh.

## Nơi sửa mã

| Phần | mã trong khi chạy chuyển động |
|---|---|
| Lệnh | CommandBridge/API, PathRequest, Assignment |
| Cost | Field/IntegrationField/IntegrationFieldSystem, IntegrationBucketQueue |
| Hướng ô | Field/FlowField/FlowDirectionSystem |
| Goal và preferredVelocity | LocalAvoidance/Targeting/MovementAgentTargetSystem |
| Né lính | LocalAvoidance/Avoidance/MovementAgentORCASystem, Helpers/ORCAMath |
| Slide/separation | LocalAvoidance/Actuation/MovementAgentActuatorSystem, Helpers/GridObstacleAvoidance |
| Height | Height/TerrainHeightSystem, PendingHeight |
| dựng lại | Field/FlowField/FlowFieldInvalidationSystem |

Formation/Context Steering trong tài liệu cũ không thuộc thư mục khi chạy hiện tại.

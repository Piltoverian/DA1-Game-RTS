# MovementAgentAuthoring

Cập nhật 2026-10-12 theo bộ chuyển dữ liệu sang ECS hiện tại.

| Inspector | Ý nghĩa |
|---|---|
| speed | Tốc độ |
| diameterCells | Đường kính theo ô; migrate từ tên bán kính cũ |
| arrivalRadius | Vùng hãm tốc |
| stoppingDistance | Ngưỡng đến đích |
| rotationSpeed | Nội suy quay |
| useTestTarget / testTarget | Lệnh thử lúc chuyển dữ liệu sang ECS |

OnValidate clamp diameterCells>=0,01; stoppingDistance=diameterCells×0,5 ×1,5; arrivalRadius=tối đa(diameterCells×0,5 ×4,speed×0,8).

bộ chuyển dữ liệu sang ECS tạo MovementAgentComponent, MovementAgentAvoidanceComponent, MovementSteeringComponent. `Avoidance.radius` ban đầu bằng diameterCells/2; không nhầm với bán kính thế giới ECS sau căn lưới.

Có khối va chạm và không có ModelSnapperAuthoring thì thêm ModelSnapSource/Snapping. Xem [lưới bộ căn lưới](../../../../MapGeneration/GridSnapper_ResourceSpawn.md).

bộ chuyển dữ liệu sang ECS không tạo đội hình thành phần, sidePreference hoặc Context Steering buffers. useTestTarget thêm TargetChangeRequest. Height được xử lý bằng baking/height hệ thống riêng.

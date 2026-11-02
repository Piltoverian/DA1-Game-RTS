# Movement: điều hướng và dịch chuyển

## Luồng một lệnh

```text
MoveOverride / MovementAgentAPI.SetTarget
    → TargetChangeRequest → FlowFieldAssignmentSystem
    → resolve target + tìm/tạo FlowField Requested
    → IntegrationFieldSystem → FlowDirectionSystem → Ready
    → MovementAgentTargetSystem → preferredVelocity
    → MovementAgentORCASystem → velocity/separation
    → MovementAgentActuatorSystem → XZ + PendingHeight
    → terrain height cache → LocalTransform.Y
```

Navigation/avoidance ở FixedStepSimulationSystemGroup. Height có pass trước TransformSystemGroup để xử lý spawn muộn. Một render frame có thể chứa nhiều fixed tick; đừng diễn giải chi phí mỗi tick thành chi phí mỗi frame khi chưa đo.

| Nơi sửa | Source |
|---|---|
| Nhận/khôi phục request | [MovementAgentAPI.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/CommandBridge/API/MovementAgentAPI.cs) · [MovementAgentPathRequestSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/CommandBridge/MovementAgentPathRequestSystem.cs) |
| Resolve mục tiêu, field ownership và cache | [FlowFieldAssignmentSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/CommandBridge/FlowFieldAssignmentSystem.cs) · [FlowFieldHelper.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Helpers/FlowFieldHelper.cs) · [FlowFieldCacheHelper.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Helpers/FlowFieldCacheHelper.cs) |
| Cost và integration | [IntegrationFieldSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Field/IntegrationField/IntegrationFieldSystem.cs) · [IntegrationBucketQueue.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Field/IntegrationField/IntegrationBucketQueue.cs) |
| Hướng theo ô | [FlowDirectionSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Field/FlowField/FlowDirectionSystem.cs) |
| Goal và preferred velocity | [MovementAgentTargetSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/LocalAvoidance/Targeting/MovementAgentTargetSystem.cs) · [UnitMovementMath.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Helpers/UnitMovementMath.cs) |
| Tránh hàng xóm | [MovementAgentORCASystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/LocalAvoidance/Avoidance/MovementAgentORCASystem.cs) · [ORCAMath.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Helpers/ORCAMath.cs) |
| Collision/slide và arrival | [MovementAgentActuatorSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/LocalAvoidance/Actuation/MovementAgentActuatorSystem.cs) · [GridObstacleAvoidance.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Helpers/GridObstacleAvoidance.cs) |

## Đích yêu cầu và đích điều hướng

MovementAgentComponent phân biệt currentworldtarget (ý định gốc), navigationTargetCell (đích cho field) và realTarget (đích thực tế/seed cùng island). TargetResolutionKind và generation lưu cách resolve. Mục tiêu blocked có thể được phân giải sang ô/viền hợp lệ; không ghi đè ý định gốc chỉ để cache trùng.

PathRequest bù request thiếu, field mất, target chưa resolve hoặc natural target stale theo generation. SetTarget chủ động phát request kể cả di chuyển trong cùng một ô; bỏ request chỉ vì cell không đổi sẽ làm mất đích XZ mới.

## Grid, cost và connectivity

[GridHelper.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Helpers/GridHelper.cs) dùng floor với origin/cellsize; index = y × width + x, grid Y tương ứng world Z. Helper index không thay bounds validation. Width của vertex grid là width+1; không dùng nhầm với width terrain.

GridInit chờ terrain sẵn sàng, cost walkable thường là 1, blocked 255. [CostChangeSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Grid/Cost/CostChangeSystem.cs) ghép thay đổi blocker; [GridIslandSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Grid/Cost/GridIslandSystem.cs) tính vùng liên thông. Terrain blocked không được mở lại chỉ vì một dynamic blocker bị xóa.

[GridNeighborMaskSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Grid/GridNeighborMaskSystem.cs) cache mặt nạ 8 hướng theo topologyVersion; xét diagonal corner, cliff/ramp crossing. Writer mới của GridNodeCost phải tăng topologyVersion khi walkability đổi; isDirty/generation riêng không đủ. Field stale/rebuild và island generation vẫn có nhiệm vụ riêng.

## Integration và direction

Integration lan bestcost từ seed bằng bucket Dijkstra. Cost cạnh không âm: cost ô 0..254 + khoảng cách 10/14, tối đa **268**, nên có **269 bucket vòng**. Entry nhớ cost lúc enqueue; khi pop mà cost khác bestcost thì bỏ entry stale. Ô A được cải thiện 80 → 35 phải xử lý 35, không dùng lại entry 80. Slot queue được tái sử dụng; đổi trọng số phải xét lại giới hạn bucket và tính không âm.

Single-island target walkable với metadata hiện hành có nhánh seed trực tiếp; blocked/multi-island/stale dùng đường seed đầy đủ. Chọn seed và propagation không được dùng reachability Euclidean thay connectivity. FlowDirection đọc bestcost và mask để tạo hướng rồi publish field Ready.

FlowFieldInvalidationSystem ưu tiên field refCount cao. MinRecalcPerFrame=12 là mức tối thiểu trong budget theo số field stale/heartbeat, không phải trần số integration mới; mỗi field vẫn có thể chạy hết queue trong job. Các marker Integration.Reset/Seeds/Propagate giúp tách reset, seed và lan cost. Không suy FPS/Burst từ benchmark thuật toán ngoài Unity.

## Target, ORCA và actuation

- Target đọc hướng của ô chứa agent. CalculateSafeFlowVelocity điều chỉnh theo tâm ô để tránh cắt góc khi agent lệch tâm.
- Gần realTarget, chỉ blend hướng trực tiếp nếu ClearSegment hợp lệ. arrivalRadius dùng hãm tốc; stoppingDistance dùng xác định đến đích.
- ORCA tạo velocity/separation từ hàng xóm. Actuator đưa cả correction và velocity qua MoveAndSlide trên XZ, không teleport xuyên blocker.
- Kẹt xa đích giữ lệnh. CrowdedDestination chỉ settle gần goal, có hàng xóm và đoạn tới goal clear sau thời gian ít tiến triển; không hủy lệnh chỉ vì stuckTime lớn.
- isSettled có thể true khi đang chờ nhưng hastarget còn true. Đọc hastarget/stopReason để biết kết thúc lệnh.
- diameterCells là đường kính theo ô; radius runtime là đơn vị world. Height độc lập renderer, xem [Terrain](Terrain.md).

## API và lifetime

SetTarget/ResumeAgent khởi động lại tracking. StopAgent dùng được với ref component trong job; PauseAgent giữ field và đặt Paused; ClearTarget giải phóng field và cleanup reference. Arrival/CrowdedDestination/InvalidTarget giữ lý do riêng. Gameplay không tự viết lại các cụm reset velocity/preferredVelocity/steering.

Cache giới hạn **20 entry**, không phải 20 field đang sống. Field bị evict vẫn có thể được agent sử dụng; vì vậy assignment có đường tìm toàn bộ field sau cache miss. Đây là chủ đích hiện tại. Không xóa đường đó nếu chưa đổi ownership/cache policy.

Assignment tổng hợp ref-count delta trong batch; các field tạo bằng ECB có Entity tạm. Playback phải remap cả FieldEntity của agent, cleanup component và cache buffer. Cleanup chỉ destroy field không còn ref và không nằm trong cache. Không tự Dispose native storage do system khác sở hữu.

## Debug và giới hạn cần nhớ

GridDebug, MovementAgentDebugAuthoring/System và spatial bucket nằm trong Movement/Debugger và Movement/Spatial. Unit/MoveAgent/Selectable buckets phục vụ nhóm query khác nhau; bucket không gian không phải bucket chi phí của Integration.

Source Integration hiện có MaxIslands=10000 nhưng một số vòng seed dừng ở 1000; kiểm tra nhánh này trước khi mở rộng tải nhiều island. Chưa có clearance class đầy đủ cho unit lớn hoặc bằng chứng hết overlap/far-stall trong mọi tình huống.

Khi sửa: đo số field thực sự tạo/rebuild, retarget cùng ô, blocker mở/đóng, target blocked, nhiều island, bottleneck, unit lớn, chết/despawn và native memory. Đọc p95/p99/worst cùng workload/hardware; mục tiêu 2.000 unit chưa được chứng minh.

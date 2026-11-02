# Gameplay: từ input tới state simulation

## Command và quyền điều khiển

```text
Input/selection → command snapshot → CommandQueue
    → kiểm tra issuer/source/capability → handler → state/request gameplay
```

Entry point: [UnitController.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Components/UnitController.cs), [CommandQueue.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Commands/CommandQueue.cs), [CommandAuthority.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Commands/Validation/CommandAuthority.cs).

CommandAuthority yêu cầu playerId không âm, entity tồn tại, không phải Prefab và EntityOwner.PlayerID khớp issuer. Cùng team không tự cấp quyền điều khiển. RPC tương lai phải lấy issuer từ connection, không tin playerId tự khai trong payload.

CommandQueue snapshot và clear buffer trước dispatch vì placement/production có thể tạo entity. Lệnh thêm trong quá trình xử lý dành cho update sau. TargetTo dùng một TryResolveTargetAction để kiểm tra/phân loại; giữ thứ tự mục tiêu snapshot, build được phép, gather, attack và build còn lại. Handler vẫn xác minh điều kiện gameplay và visibility trước làm việc. [Luật FOW](FogOfWarRendering.md) là nguồn duy nhất cho mục tiêu khuất.

## Xây mới, giao nhà dở và repair

```text
BuildOffer → BuildingPlacer preview → PlanBuild + snapshot workers
    → PlaceBuildingRequest → validate/pay/instantiate
    → Build cho các worker hợp lệ → BuilderSystem → ConstructionSystem
```

- PlanBuild là công trình mới. SourcesEntity giữ buffer worker tại thời điểm gửi; đổi selection sau đó không đổi lệnh. Payload được hủy sau xử lý; lọc owner, worker còn sống, offer, movement và trùng lặp trước placement.
- Build giao nhà dở và có FIFO. Repair dùng TargetTo + RepairOnly, không FIFO/tự tìm nhà khác. Không suy loại ý định chỉ từ việc target có BuildingConstruction.
- Footprint chưa biết được lưu thành kế hoạch; worker tiếp cận snapshot rồi mới thử placement thực tế. Vùng thấy có thể dùng request nhóm tạo một nhà; các thử Unknown được kiểm tra lại khi worker tới nơi.
- Placement kiểm tra prefab, BuildOffer/quyền, footprint/surface/occupancy và chi phí. Thất bại sau thanh toán phải rollback tiền/công trình, không để thanh toán treo.
- Builder vào Building khi đủ BuildRange; work tăng theo EntityWork.Rate × deltaTime, clamp phần còn thiếu. Hoàn thành/thất bại nối FIFO; hết queue tìm nhà dở đang thấy trong cửa sổ ±5 ô X/Z, đúng owner/offer và island cạnh footprint. RepairOnly hết việc thì Idle.
- Tiếp cận dùng closest = clamp(workerXZ, footprintMin, footprintMax), đẩy ra theo radius + 0.01. Stopping distance tạm được khôi phục khi làm việc/dừng/thay lệnh. Footprint dùng LocalTransform anchor, không dùng offset model trong LocalToWorld.
- Move thay thế hủy queue và placement request đang chờ. CancelBuildingRequest đi riêng; không ép cancel vào FIFO command.

Nguồn: [BuildingPlacer.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Input/BuildingPlacer.cs), [BuildingPlacementPreview.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Input/BuildingPlacementPreview.cs), [BuildingPlacementSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Construction/BuildingPlacementSystem.cs), [BuilderSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Construction/BuilderSystem.cs), [ConstructionSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Construction/ConstructionSystem.cs), [BuildingCancelSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Construction/BuildingCancelSystem.cs).

## Resource payment và production

Chi phí SO được Registry Validate gộp theo ResourceType trước bake. Runtime [ResourcePayment.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Economy/Validation/ResourcePayment.cs) từ chối cost trùng/âm/không hữu hạn và kiểm tra số dư trước trừ; lỗi giữa các bước hoàn lại phần đã trả. Caller sở hữu NativeList paid và phải Dispose. Không gộp lại chi phí tĩnh mỗi lần thanh toán.

Ví dụ: cấu hình wood 20 + wood 30 được normalize thành wood 50; số dư 40 phải từ chối toàn bộ giao dịch. Không trừ một phần tài nguyên rồi bỏ lệnh vì thiếu loại khác.

[ProductionJobs.cs](../../Assets/_Project/Scripts/Runtime/GameData/Component/ProductionJobs.cs) phân giải GameBlobReference theo type key + ID, đối chiếu đúng offer của producer, capacity, health/completion và unlock trước pay/enqueue. Registry có blob không đồng nghĩa producer có quyền sản xuất. Payment ledger gắn ItemID; cancel/death cần dọn queue/payment/pending tech trước destroy producer. [ProductionSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Construction/ProductionSystem.cs) xử lý work/completion, train/research theo dữ liệu đã bake.

## Gather

[WorkerGatherSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Economy/WorkerGatherSystem.cs) giữ các state Idle → GoingToNode → Gathering → ReturningDepot. Node/depot phải còn hợp lệ và tiếp cận được theo island. Node khuất chỉ được tiếp cận bằng thông tin đã thấy; tự tìm node thay thế chỉ chọn node visible. Giữ carry/depot/search khi mục tiêu cạn hoặc mất. Các cache node/depot hiện được dựng trong system mỗi tick; chưa chuyển sang persistent cache.

Dừng qua [MovementAgentAPI](Movement.md#api-và-lifetime); code công việc không tự định nghĩa lại trạng thái dừng của agent. Dữ liệu state/target nằm ở WorkerGatherData/TargetCache; không dùng việc renderer đang ẩn để kết luận node đã chết.

## Combat, selection và presentation

- [FindTargetSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Combat/FindTargetSystem.cs) tìm target; [ShootAttackSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Combat/ShootAttackSystem.cs) xử lý attack/chase; bullet/artillery mover và [EntityDeathSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Combat/EntityDeathSystem.cs) xử lý projectile/death. Target mới phải theo owner/team và FOW; luật attack đang chạy rồi mất vision vẫn còn mở, xem [CurrentState](../Maintenance/CurrentState.md).
- Selection đi qua input manager → SelectionRequest → SelectSystem. Click/drag khác nhau; SelectManager dùng thời gian giữ và chiều rộng rect. UI phải đợi World/context/bootstrap sẵn sàng.
- TestPlayer/LocalTestPlayer xác định viewer local. Trong chế độ bootstrap, PlayerContextAuthoring liên kết context trong roster, không tạo player thứ hai. Age/resources đầu trận do MapConfig/PlayerBootstrapSettings.
- UI icon/name đi qua EntityPresentation → RegistryAuthoring.Instance → GameDataRegistry SO, không đọc chúng từ blob gameplay. [GameData](../Guides/GameData.md) mô tả publication và giới hạn lifecycle.

## Kiểm tra khi sửa

Trong Main: đổi selection sau gửi PlanBuild; nhiều worker cùng nhà; FIFO/repair/cancel/Move thay thế; thiếu tiền và rollback; node cạn/khuất; depot bị mất; production queue/death; cùng team không điều khiển hộ; UI khi đổi viewer. Phân biệt test handler/ECS với thao tác chuột và toàn luồng Play Mode.

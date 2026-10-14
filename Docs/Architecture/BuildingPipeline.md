# Luồng đặt nhà và xây công trình

Tài liệu mô tả implementation hiện tại ngày 14/10/2026. [CurrentState](../Maintenance/CurrentState.md) giữ trạng thái kiểm chứng; [Handoff](../Maintenance/Handoff.md) giữ việc tiếp theo.

## Hai ý định, hai command

`PlanBuild` đặt một công trình mới. `Build` giao một worker xây/sửa công trình đã tồn tại. Không dùng sự có/không có `BuildingConstruction` để chọn giữa hai path.

```text
Worker được chọn → nút BuildOffer → BuildingPlacer preview
→ click xác nhận → AddPlanBuildToQueue (snapshot toàn bộ selection)
→ CommandQueue.PlanBuild → PlaceBuildingRequest + danh sách worker
→ BuildingPlacementSystem validate / thanh toán / tạo công trình
→ một CommandQueue.Build cho mỗi worker hợp lệ → BuilderSystem

Click phải công trình đang dở → UnitController
→ CommandQueue.Build → HandleBuild → BuilderSystem
```

`AddPlanBuildToQueue` giữ danh sách sources trong buffer của entity payload, được tham chiếu bằng `SourcesEntity`. Selection thay đổi sau khi gửi không thay đổi lệnh đã gửi. `HandlePlanBuild` lọc chủ sở hữu, khả năng di chuyển/xây, offer phù hợp, worker còn sống và trùng lặp; payload được hủy sau xử lý. Nếu không có worker hợp lệ, không phát placement request.

Placement kiểm tra cấu trúc prefab, offer/quyền của worker, vị trí/footprint và tài nguyên. Khi tạo nhà thành công, gửi `Build` cho tất cả worker hợp lệ trong request tới cùng entity công trình. Không tạo một nhà riêng cho từng worker. Placement lỗi không được giữ khoản thanh toán/công trình đã tạo; rollback có trong hệ thống hiện tại.

`HandleBuild` xác nhận quyền, khả năng xây và trạng thái công trình; đặt `GoingToSite` hoặc thêm công trình vào `BuilderQueueElement` nếu worker đang xây. Đây là validation nghiệp vụ của `Build`, không phải phép phân loại command.

## Tiếp cận bằng clamp

Với footprint `[worldMin, worldMax]`, điểm gần nhất là `closest = math.clamp(workerPosition.xz, worldMin, worldMax)`. Đích di chuyển là điểm này cộng hướng ra phía worker nhân `radius + 0.01`, để tâm unit đứng ngoài collider. Không dò ô quanh nhà, không dùng bucket để chọn điểm tiếp cận.

Khoảng dừng khi tiếp cận được giới hạn theo phần BuildRange còn lại; khôi phục khi bắt đầu xây, dừng builder hoặc nhận lệnh thay thế. Worker ở trong tầm footprint chuyển `GoingToSite → Building`. Khi `Building`, work cộng theo `EntityWork.Rate × deltaTime`, giới hạn bởi work còn thiếu; `ConstructionSystem` cập nhật phase.

Grid, builder và navigation dùng anchor logic `LocalTransform.Position` cho footprint. `BlockageTransforms.Capture` cung cấp anchor đó cho ba bước FlowFieldAssignment/Integration/MovementAgentTarget; không dùng offset căn giữa model trong `LocalToWorld` làm dịch footprint.

Preview dùng transform đã snap của prefab trước khi show; phần fallback cube lấy kích thước từ footprint. Đây là implementation, chưa phải xác nhận hoàn tất Play Mode trên bản clamp cuối.

## Nguồn chính

- [CommandQueue](../../Assets/_Project/Scripts/Runtime/Simulation/Commands/CommandQueue.cs)
- [BuildingPlacer](../../Assets/_Project/Scripts/Runtime/Presentation/Input/BuildingPlacer.cs)
- [BuildingPlacementSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Construction/BuildingPlacementSystem.cs)
- [BuilderSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Construction/BuilderSystem.cs)
- [BlockageTransforms](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Blockage/BlockageComponent.cs)

Tham chiếu hành vi cũ: commit `d0f1ca0f4d7b885988badc901888ab0709aa70e2`. Bản cũ cho BuildingPlacer tạo placement request trực tiếp, rồi placement mới giao `Build` cho worker. Bản hiện tại giữ hai ý định riêng và đưa `PlanBuild` qua queue.

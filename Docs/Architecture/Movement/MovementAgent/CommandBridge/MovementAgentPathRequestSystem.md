# MovementAgentPathRequestSystem.cs

Hệ thống này đóng vai trò là "người giám sát" các yêu cầu di chuyển. Nó liên tục kiểm tra xem Unit có cần một đường đi mới (Flow Field) hay không.

---

## 1. Chức năng chính
Hệ thống quét qua tất cả các Unit có `MovementAgentComponent` và xác định xem mục tiêu hiện tại của Unit đã có đường đi tương ứng chưa.

---

## 2. IdentifyPathRequestJob
Đây là một `IJobEntity` được biên dịch với Burst (`[BurstCompile]`) và gắn attribute `[WithNone(typeof(TargetChangeRequest))]` để chạy song song và tự động bỏ qua những Unit đang có sẵn `TargetChangeRequest`, tránh việc gọi `ECB.AddComponent<TargetChangeRequest>` trùng lặp trong cùng một vòng đời request.

> [!NOTE]
> Hệ thống này **không** sao chép `BlockageData`/`LocalToWorld` và **không** quét 4 hướng mỗi tick. Toàn bộ việc phân giải mục tiêu chỉ diễn ra một lần tại `FlowFieldAssignmentSystem` và được kiểm tra lại tại đây thông qua trạng thái đã lưu trên `MovementAgentComponent`.

### Điều kiện kích hoạt yêu cầu mới:
Khi `move.hastarget == true`, hệ thống sẽ thêm `TargetChangeRequest` vào Unit nếu rơi vào một trong 3 trường hợp sau:
1. **Chưa có đường đi hoặc mất liên kết**: `move.FieldEntity == Entity.Null`, `move.targetResolutionKind == TargetResolutionKind.None`, hoặc `FlowFieldLookup` không còn tìm thấy component `FlowField` trên `move.FieldEntity`.
2. **Thế hệ bản đồ thay đổi đối với mục tiêu tự nhiên đã resolve**: `move.targetResolutionKind == TargetResolutionKind.NaturalResolved` và `move.targetResolutionGeneration != Grid.generation` (khi có công trình mới xây/phá làm thay đổi lưới, cần resolve lại ô đích).
3. **Lệch ô đích điều hướng**: `math.any(move.navigationTargetCell != currentField.targetcell)` (so sánh trực tiếp với `navigationTargetCell` thay vì `WorldToGrid(move.realTarget)` vì `realTarget` có thể đang tạm giữ tọa độ `IslandSeed` hoặc chu vi công trình).

---

## 3. Vai trò của TargetChangeRequest
`TargetChangeRequest` hoạt động như một tín hiệu (Signal) để:
- Kích hoạt `MovementAgentGroupFormationSystem` tính toán lại vị trí đội hình.
- Kích hoạt `FlowFieldAssignmentSystem` tìm kiếm hoặc tạo mới một Flow Field phù hợp.

---

## 4. Thứ tự thực thi (Execution Order)
Hệ thống này chạy **TRƯỚC** `IntegrationFieldSystem`.
- **Lý do**: Để các yêu cầu di chuyển được ghi nhận ngay trong cùng một khung hình tính toán lưới, giúp giảm độ trễ (latency) khi người chơi ra lệnh.
```csharp
[UpdateBefore(typeof(IntegrationFieldSystem))]
```

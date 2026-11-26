# Fog of War local: render và lệnh

FOW quyết định player được biết ô/entity nào và giữ ký ức hình khi mất vision. Snapshot/memory mô tả ở trang này vẫn là dữ liệu local. Ghost live unit/building/resource đã có relevancy phía server; phần memory client khi mất relevancy chưa được nối. [Terrain](Terrain.md) sở hữu geometry/renderer; [Multiplayer](../Multiplayer/README.md) sở hữu chunk/session; [CurrentState](../Maintenance/CurrentState.md) ghi giới hạn kiểm chứng.

## Luồng dữ liệu

```text
Fixed tick: UnitSpatial → NeutralSpatial → FogOfWarSpatial
    ↓ Presentation
LocalFogPresentationSystem
    → FogOfWarSnapshot.Collect(em, playerId, frame)
    → FogOfWarRenderView.Apply(frame)
    ├─ entity visible: renderer gốc; capture hình building/resource
    ├─ unit/đạn khuất: ẩn hình, không giữ ký ức
    └─ building/resource khuất: vẽ hình cuối thấy độc lập entity thật

TerrainVisualSource (compile map một lần theo revision/hash)
    + View.Terrain.Cells (chỉ index Explored) + Frame.VisibleCells
    → LocalTerrainView → TerrainView → MapTerrainPresentationSystem → renderer
```

[Adapter local](../../Assets/_Project/Scripts/Runtime/Presentation/Components/LocalFogPresentationSystem.cs) chạy LocalSimulation trước map/EntitiesGraphics. Viewer từ LocalTestPlayer, fallback SelectHelper. Adapter sở hữu frame và hình nhớ; đổi viewer/grid/map revision thì Clear và tăng ScopeVersion. Không thêm WorldId/MapId/MapRevision vào frame.

[FogTerrainMemory](../../Assets/_Project/Scripts/Runtime/Simulation/FogOfWar/FogTerrainData.cs) giữ tập index Explored; Version đổi khi membership hoặc scope đổi. Không giữ bản sao GridTerrain. Geometry, visual đã lọc và texture mask thuộc [TerrainView](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainView.cs), do producer local/network sở hữu và dispose.

## Entity và ký ức

[Payload](../../Assets/_Project/Scripts/Runtime/Simulation/FogOfWar/FogEntityRenderData.cs) dùng Entity local, ID string GameReg hoặc fallback entity đang sống để capture. Không serialize Entity local thành network ID. Bộ nhớ trạng thái ở [view](../../Assets/_Project/Scripts/Runtime/Presentation/Components/FogOfWarRenderView.cs).

| Trạng thái | Hình và dữ liệu |
|---|---|
| Visible | Native renderer cập nhật bình thường; building/resource capture mesh/material copy/matrix và RevealHeight. |
| Unit/đạn mất visible | Ẩn renderer gốc, bỏ khỏi view. |
| Building/resource khuất | Giữ snapshot cuối và Graphics.DrawMesh bằng dữ liệu đã capture; không đọc state mới để dựng ký ức. |
| Nhìn lại footprint cũ nhưng entity không còn | View bỏ record, adapter giải phóng hình nhớ. |
| Chưa từng thấy entity | Không hiện, kể cả ô đã Explored qua chia sẻ lịch sử. |

Đối tượng tĩnh chỉ gồm building/resource; các vật cản khác là terrain. Root/hình con tra LinkedEntityGroup và Parent. DisableRendering chỉ áp phần graphics, không disable simulation. SpriteRenderer cần quản lý riêng và khôi phục khi hiện lại/dispose; selection/minimap đối chiếu trạng thái chọn hiện tại. Material ký ức được sao chép để không nhận shader state mới trong fog. LocalToWorld đã chứa snap/scale, không áp PostTransformMatrix lần hai.

Snapshot vẫn giữ ability đang thực thi, job production đầu queue, Health Current/MaxOverride và Resource Amount theo hợp đồng đã có. Các trường health/ability/job hiện chưa có consumer UI trong source; chưa xóa hoặc tách một schema song song. Không clone gameplay hoặc gửi queue/danh sách khả năng. Ghost live entity thuộc phần [multiplayer](../Multiplayer/README.md); không dùng queue Owner-only để cung cấp job của đối phương và chưa nối memory network vào snapshot local này.

## Tầm nhìn và terrain

Tầm nhìn theo ô với `dx² + dz² <= R²`, không xét occlusion. Hợp vision theo player/team mode; một ô footprint visible đủ nhận toàn entity, không tự reveal các ô footprint còn lại.

Collect trả full index Explored và Visible mỗi lần, không mang raw terrain. Explored gồm lịch sử chia sẻ từ đồng đội, chỉ cho phép nhận terrain; không cho phép biết entity mới trong ô đó. Đạn không mở tầm nhìn.

LocalTerrainView lọc visual đã compile theo membership; không compile lại khi chỉ biết thêm ô. GeometryVersion đổi theo source, scope hoặc membership. Chỉ visibility đổi thì cập nhật mask; không dựng lại mesh hoặc tăng geometry version. Thêm ô vẫn rebuild toàn bộ tập hình đã biết, chưa có incremental mesh.

```text
Mask = 1       nếu Visible và có visual được phép biết
       80/255  nếu Explored nhưng khuất
       0       nếu Unknown
```

Mask Point, biên cứng theo ô; Unknown không có record visual/sprite và nền camera đen. Entity có luật visible/ký ức riêng. Texture chỉ upload khi pixel đổi.

## Chung mốc height

CornerHeights/TopHeight trong visual đã được compiler chuẩn hóa theo **map đầy đủ**, cùng mốc với simulation. Không dựng lại datum từ riêng tập Explored hoặc nâng unit thêm lần nữa; xem [height và picking](Terrain.md#height-và-picking). Frame FOW không cần GridTerrain placeholder/TerrainBaseLevel. Camera showcase chỉ được đọc visual đã biết.

## Lệnh và thông tin đã biết

Luật dưới đây áp dụng trước khi handler dùng trạng thái thật của target. Cơ chế command/FIFO/payment nằm ở [Gameplay](Gameplay.md).

| Lệnh | Luật hiện tại |
|---|---|
| Attack mới | Chỉ nhận target địch đang Visible, không cùng đội; khuất thì Move tới vị trí click. Chưa chốt luật mới khi Attack đang chạy rồi mất vision. |
| Move | Cho phép tới vị trí, kể cả vùng Unknown. |
| Build preview | Xanh khi toàn footprint đang Visible và hợp lệ; đỏ khi toàn footprint đang Visible và không hợp lệ. Terrain bất biến đã biết có thể báo đỏ sớm. Thiếu thông tin thì Unknown, không dùng occupancy khuất để báo màu. |
| PlanBuild Unknown | Lưu kế hoạch trước, chưa tạo nhà/chi tiền ngay. Worker tiếp cận; khi đủ visibility hoặc đã tới BuildRange thì gửi placement request kiểm tra thực tế. Điều kiện thấy toàn footprint dành cho preview, không cấm thử placement khi đã tới nơi. |
| Build | Hoàn thành hoặc thất bại thì tiếp tục FIFO; hết queue tìm công trình dở lân cận. Không có việc phù hợp mới Idle. |
| Repair | Nhận mục tiêu từ snapshot, tới kiểm tra rồi sửa nếu còn việc. Không có queue/tự nối công trình khác; mục tiêu mất hoặc hết việc thì Idle. |
| Gather | Tới resource trong snapshot để kiểm tra trước khi dùng trạng thái thật. Giữ luồng khai thác, mang hàng, depot và tìm thay thế; tự tìm chỉ chọn resource đang thấy. |

Build tìm việc theo cùng cửa sổ ±5 ô X/Z như Gather (không phải hình tròn), chọn gần nhất theo khoảng cách XZ và kiểm tra island tiếp cận từ cạnh footprint. Chỉ chọn nhà dở đang thấy, đúng chủ sở hữu/BuildOffer, chưa đủ work và còn sống.

Pipeline: input Mono → snapshot đã biết → command giữ vị trí/footprint → đi kiểm tra → xác minh thực tế → làm việc / FIFO tiếp theo / tìm lân cận / Idle. Move thay thế hủy queue và placement request đang chờ. Movement thất bại ngoài tầm kết thúc lần thử, không phát lại vô hạn.

Nguồn:

- [FogOrderRules](../../Assets/_Project/Scripts/Runtime/Simulation/FogOfWar/FogOrderRules.cs): truy vấn grid/visibility; không điều khiển movement hoặc sửa state công việc.
- [BuildingPlacementPreview](../../Assets/_Project/Scripts/Runtime/Presentation/Input/BuildingPlacementPreview.cs): xanh/đỏ/Unknown trước khi gửi placement request.
- [KnownTargetOrders](../../Assets/_Project/Scripts/Runtime/Simulation/Commands/KnownTargetOrders.cs) và [FogTargetTravelSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Commands/FogTargetTravelSystem.cs): tiếp cận snapshot, kiểm tra reachability, chuyển state khi thất bại; dừng qua MovementAgentAPI.
- [FogCommandPicking](../../Assets/_Project/Scripts/Runtime/Presentation/Components/FogCommandPicking.cs), [UnitController](../../Assets/_Project/Scripts/Runtime/Presentation/Components/UnitController.cs): picking building/resource từ view, lấy đối tượng tĩnh gần ray nhất trước phân loại lệnh. Bounds hiện dùng footprint/hộp cao 2 đơn vị, chưa phải mesh raycast.
- [CommandQueue](../../Assets/_Project/Scripts/Runtime/Simulation/Commands/CommandQueue.cs): FogKnownTarget giữ thông tin lúc phát lệnh, kể cả khi Entity sau đó mất. ResourceTypes được adapter ghi khi thấy; không thêm Type vào FogResourceSnapshot chỉ có Amount.
- [BuilderSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Construction/BuilderSystem.cs), [WorkerGatherSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Economy/WorkerGatherSystem.cs): state công việc và tiếp tục sau xác minh. [Gameplay](Gameplay.md).

### Material Unknown

[Main](../../Assets/_Project/Scenes/Main.unity) gán [GhostUnknown.mat](../../Assets/_Project/Art/Materials/GhostUnknown.mat) vào BuildingPlacer.unknownGhostMaterial. BuildingPlacer chọn ba material theo BuildingPreviewState; scene/prefab mới phải gán đủ ba material vì runtime không tự tạo fallback.

## Giới hạn và bước tiếp theo

[CurrentState](../Maintenance/CurrentState.md) giữ các ca chưa nghiệm thu và luật còn mở. Khi sửa FOW, kiểm tra riêng visibility, last-seen memory, picking/command, UI/selection và vòng đời viewer/map; chỉ ẩn mesh không đủ bảo đảm thông tin gameplay.

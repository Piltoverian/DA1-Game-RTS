# Fog of War local: render và lệnh

Render local đã nối và kiểm tra trong Main ngày 17/10/2026. [Handoff](../Maintenance/FogOfWarHandoff.md) giữ bằng chứng lịch sử và giới hạn. Ngày 20/10, phần hình terrain được tách thành dữ liệu ô độc lập và nối thêm Ghost; xem [pipeline terrain và kiểm chứng mới](../Multiplayer/ServerMapLoading.md). Snapshot FOW entity local bên dưới vẫn chưa phải giao thức Netcode cho unit/building/resource.

## Luồng dữ liệu

```text
Fixed tick: UnitSpatial → NeutralSpatial → FogOfWarSpatial
    ↓ Presentation, hoàn tất jobs và lấy viewer local
LocalFogPresentationSystem
    → FogOfWarSnapshot.Collect(em, playerId, frame)
    → FogOfWarRenderView.Apply(frame)
    ├─ visible: dùng renderer gốc, capture hình tĩnh
    ├─ unit/đạn khuất: ẩn hình, không giữ ký ức
    └─ building/resource khuất: vẽ hình cuối thấy độc lập entity thật
    ↓ MapTerrainPresentationSystem
View.Terrain.Cells + Frame.VisibleCells + TerrainBaseLevel
    → dựng terrain đã biết + texture mask → terrain shader
```

[Adapter](../../Assets/_Project/Scripts/Runtime/Presentation/Components/LocalFogPresentationSystem.cs) chỉ chạy LocalSimulation, trước map/EntitiesGraphics. Viewer từ LocalTestPlayer, fallback nguồn SelectHelper sẵn có. Frame tái sử dụng và disposal thuộc adapter; Clear khi đổi trận là trách nhiệm consumer. WorldId/MapId/MapRevision đã bỏ khỏi frame, không bỏ revision nội bộ gen map.

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

Snapshot chỉ giữ ability đang thực thi, job production đầu queue; Health Current/MaxOverride và Resource Amount. Không clone gameplay hoặc gửi queue/danh sách khả năng. Quyết định cách encode Ghost còn ở [multiplayer](../Multiplayer/README.md).

## Tầm nhìn và terrain

Tầm nhìn theo ô với `dx² + dz² <= R²`, không xét occlusion. Hợp vision theo player/team mode; một ô footprint visible đủ nhận toàn entity, không tự reveal các ô footprint còn lại.

Collect trả full các record Explored mỗi lần; knownTerrain chỉ còn tham số tương thích, không lọc cache. Không gửi terrain chưa biết, GridNodeCost hay occupancy động; đạn không mở tầm nhìn.

[MapTerrainPresentationSystem](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/MapTerrainPresentationSystem.cs) dùng cache đã nhận trong local FOW. Nhánh không có adapter local vẫn giữ đường terrain sẵn có. [CanonicalIsometricSprites](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/CanonicalIsometricSprites.cs) không phát sprite ô unknown, ghi tâm ô sở hữu vào UV channel 2. TerrainChunkRenderer gắn mask lên material; khi số ô đã biết/scope thay đổi hiện rebuild terrain. Chưa có tối ưu cập nhật chunk tăng dần.

```text
M(cell) = 1       nếu Visible
          80/255  nếu Explored nhưng khuất
          0       nếu unknown
UV = ((x + 0.5) / width, (z + 0.5) / height)
M = 0 → clip; nền camera đen
M > 0 → terrain.rgb *= M
```

Mask lấy Point, biên cứng theo ô; đây không phải shader mask áp lên toàn mesh entity. Entity có luật visible/ký ức riêng.

## Chung mốc height

Terrain simulation và renderer cùng chuẩn hóa `(heightLevel - minimumMapLevel) × rise`. Frame gửi TerrainBaseLevel là minimumMapLevel. Ô placeholder unknown dùng đúng datum này nhưng không mang terrain thật hoặc được vẽ. Nếu để unknown=0, renderer có thể tự đổi minimum và che chân unit dù unit đã có Y đúng.

TerrainHeightCacheSystem đọc terrain simulation đầy đủ, lưu bốn góc mỗi ô. Khi PendingHeight bật, ApplyTerrainHeightJob đặt `LocalTransform.Position.y = Sample(XZ) + TerrainHeightOffset`; không cộng dồn. Transform propagation tạo LocalToWorld cho graphics. Bản sửa FOW chỉnh datum terrain, không nâng unit thêm lần nữa.

## Lệnh và thông tin đã biết

Đây là nguồn chính cho luật FOW của lệnh chốt ngày 17/10/2026. Multiplayer đang tạm gác.

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

- [FogOrderRules và FogTargetTravelSystem](../../Assets/_Project/Scripts/Runtime/Simulation/FogOfWar/FogOrderRules.cs): visibility, preview và tiếp cận snapshot.
- [FogCommandPicking](../../Assets/_Project/Scripts/Runtime/Presentation/Components/FogCommandPicking.cs), [UnitController](../../Assets/_Project/Scripts/Runtime/Presentation/Components/UnitController.cs): picking building/resource từ view, lấy đối tượng tĩnh gần ray nhất trước phân loại lệnh. Bounds hiện dùng footprint/hộp cao 2 đơn vị, chưa phải mesh raycast.
- [CommandQueue](../../Assets/_Project/Scripts/Runtime/Simulation/Commands/CommandQueue.cs): FogKnownTarget giữ thông tin lúc phát lệnh, kể cả khi Entity sau đó mất. ResourceTypes được adapter ghi khi thấy; không thêm Type vào FogResourceSnapshot chỉ có Amount.
- [BuilderSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Construction/BuilderSystem.cs), [WorkerGatherSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Economy/WorkerGatherSystem.cs): state công việc và tiếp tục sau xác minh. [Pipeline xây nhà](BuildingPipeline.md).

### Material Unknown

[GhostUnknown.mat](../../Assets/_Project/Art/Materials/GhostUnknown.mat) là asset riêng, màu RGBA `(0.8, 0.7, 0.3, 0.65)`, dùng cấu hình shader ghost hiện có. [Main](../../Assets/_Project/Scenes/Main.unity) gán `BuildingPlacer.unknownGhostMaterial`; [BuildingPlacer](../../Assets/_Project/Scripts/Runtime/Presentation/Input/BuildingPlacer.cs) chọn ba material theo BuildingPreviewState. Đã bỏ tạo material Unknown fallback lúc chạy; BuildingPlacer ở scene/prefab mới cần gán đủ ba material.

## Giới hạn và bước tiếp theo

Có regression ECS/picking và smoke Main; chưa nghiệm thu toàn bộ thao tác chuột, preview sau tách asset Unknown, nhiều worker/queue dài hoặc tải lớn. Panel UI/selection nói chung chưa được chứng minh chỉ dùng dữ liệu FOW. Luật Attack đang chạy rồi mất vision còn mở.

Client full Mono chưa hoàn tất: LocalFogPresentationSystem, MapTerrainPresentationSystem và LocalBaseCameraSnapSystem vẫn tồn tại. Khi người dùng mở lại multiplayer, dùng [hướng đã lưu](../Multiplayer/README.md); chưa tự cài Netcode. [Handoff](../Maintenance/FogOfWarHandoff.md) giữ bằng chứng và hành động tiếp theo.

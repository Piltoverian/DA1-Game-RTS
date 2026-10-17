# Render Fog of War local

Render local đã nối và kiểm tra trong Main ngày 17/10/2026. [Handoff](../Maintenance/FogOfWarHandoff.md) là nguồn chính về bằng chứng, source được kiểm tra và giới hạn. Đây là kiến trúc hiện tại; schema local chưa phải giao thức Netcode.

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

## Ranh giới tiếp theo

Render local đã được người dùng chốt để chuyển bước. Không suy thành mọi tính năng client đã full Mono hoặc mọi UI/picking đã tuân FOW. Chuyển presentation/input/camera sang Mono với adapter ECS/Netcode là yêu cầu kế tiếp, chưa triển khai. Giữ ký ức độc lập lifecycle Ghost; chỉ định scope/tick/identity mạng sau khi chốt package và Ghost schema. Xem [hướng multiplayer](../Multiplayer/README.md).

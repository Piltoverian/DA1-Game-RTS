# Hướng dẫn sử dụng terrain trong Unity

Cập nhật 2026-10-09. Main dùng SharedVertexSlopes, theme canonical, camera orthographic và mesh terrain phẳng. Cao độ là dữ liệu logic/presentation; movement và physics giữ Y=0.

## Chạy Main

1. Mở Assets/_Project/Scenes/Main.unity, chờ import/compile.
2. Kiểm tra Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset. Đây là config Main đang dùng dù nằm trong Tests.
3. TerrainMapRenderer dùng Theme/SO/XianxiaIsometricTheme.asset. Runtime chỉ có canonical sprites và stock URP Unlit; không cần chọn mode hoặc bật flag render.
4. Sau sửa config/generator, Stop Play và rebake subscene chứa GridAuthoring trước khi Play. Không kỳ vọng baked grid tự đổi trong phiên Play.
5. Renderer chờ ECS grid rồi dựng atlas/mesh; không gen map thứ hai. Camera pitch30°, yaw45°, orthographic. frameRampOnLoad tìm khu vực có ramp/cliff/núi khi có khu vực phù hợp.

## Thay đổi thường dùng

| Thay đổi | Nơi chỉnh | Bước tiếp theo |
|---|---|---|
| Seed, số ô, player, vùng spawn | MapGenConfig | Rebake, Play lại |
| Height/noise/cliff band/ramp width | Terrain settings | Rebake, validation |
| Màu đất/dốc/vách/núi | PNG/TerrainTheme | Import, Stop/Play lại |
| Zoom/showcase | IsometricTerrainCamera/renderer | Kiểm tra khung nhìn Main |

Cellsize = chiều rộng bounds grid chia số ô. Plane rộng1000, map512 cho 1.953125. MinimumCellSize là giới hạn kiểm tra, không thay công thức. Không đặt cellsize riêng trong art pack. Rise theo tỷ lệ sprite và camera; xem [schema](MapGen_TextureSchema.md).

Camera/art có hợp đồng30°/45° và128/32. Zoom orthographic được phép theo component; tự đổi pitch/yaw không phải thao tác đổi texture.

## Chọn bộ texture mới

Nhân bản theme/PNG, gán đủ slot, chọn theme trên TerrainMapRenderer và lưu scene. Làm theo [hướng dẫn texture](MapGen_TextureAuthoring.md). Không dùng menu Use Reference sau đó vì menu sẽ gán lại bộ Reference.

## Kiểm tra

| Công cụ/menu | Nội dung |
|---|---|
| RTS / Tests / Validate Mountain Integration | Hai thuật toán, núi, spawn, connectivity |
| Tools / MapGen / Test resource planner only | Snap footprint, khoảng cách núi, starting mines, seed |
| RTS / Tests / Validate Stock Reference Sprites | Theme Reference, sprite rectangles, batches, vertexY0 |
| RTS / Tests / Validate Selected Terrain Theme | Bộ theme được chọn, không cần ảnh nguồn offline |

Stock Reference validation kiểm tra bộ Reference. Với bộ tùy chỉnh, chọn TerrainTheme trong Project rồi chạy RTS / Tests / Validate Selected Terrain Theme; sau đó kiểm tra Main dùng theme mới. Khi sửa gen, kiểm tra spawn, crossing ramp và một island walkable.

## Chẩn đoán

| Triệu chứng | Kiểm tra |
|---|---|
| Terrain mất/null texture | Theme, đủ16/10 slot, Read/Write, canvas128×128 và stock Unlit shader |
| Nền vuông/xanh quanh ảnh | Alpha PNG, nền sheet tham khảo |
| Ramp hở/lệch mép | Canvas, mask, góc, crop, metrics128/32 |
| Map không đổi sau sửa config | Stop và rebake grid/subscene |
| Núi lặp trong cụm | Hiện một ảnh mỗi ô; chưa có autotile núi |
| Thiếu resource quota | Vị trí hợp lệ thiếu; đọc báo cáo, không ép spawn lên núi/ramp |
| Unit xuyên vách khi nhìn | Giới hạn occlusion, không sửa chỉ bằng PNG |

Xem [pipeline](MapGen_TerrainPresentation.md), [rules](MapGen_Unity_Rules.md), [config](MapGenConfig.md), [resource](MapGen_ResourceSpawn_Config.md) và [giới hạn](MapGen_CanonicalIsometricSprites.md).

Cập nhật loại sprite trống: giữ16top,10cliff(mask4,8,C,D,E mỗi side),1núi;22PNGcliff alpha0 và field tương ứng đã xóa. Baker không tái tạo mask trống, renderer không pack/vẽ chúng. [Manifest cleanup](../Reviews/TerrainEmptySpriteCleanup_2026-10-09.json).

# 05 — Pipeline vật liệu hiện hành

## Theme và hợp đồng

Main dùng JadeSlateTerrainTheme, sprite library Assets/_Project/Data/Terrain/Sprites/JadeSlate. ReferenceIsometricTheme giữ làm fixture hình học. Các bộ Meshy/Photoroom/Readability/Xianxia cũ đã nghỉ dùng.

78 PNG128×128:32 ground,16 top/ramp,10 cliff (side0/1×mask4,8,C,D,E),16 cap,4 decor. Pivot bottom-left(.5,.375), camera30°/45°, tier32px. RGBA/readable, Bilinear, Clamp, uncompressed, không mipmap. Alpha canonical giữ footprint; pack atlas max2048/padding4, reject auto-scale. Sheet là tài liệu, runtime dùng PNG riêng.

## Từ vật liệu đến mặt

[Source ImageGen](../../ArtSource/Terrain/JadeSlate/materials.png), [prompt chính xác](../../ArtSource/Terrain/JadeSlate/prompt.md), [provenance](../../ArtSource/Terrain/JadeSlate/README.md), [build metadata](../../ArtSource/Terrain/JadeSlate/build.json).

AI chỉ tạo bốn vật liệu phẳng: lush grass, planar slate, sparse grass, wall slate; không vẽ tile isometric hoặc khối núi. Các PNG hiện hành đã được tạo bằng rasterization offline (công cụ này đã loại bỏ). Hợp đồng hình học: đỉnh(64,112),(128,80),(64,48),(0,80), corner cao trừ32y. Wall dùng hai endpoint và bit lowA/lowB/highB/highA. Quad chia tam giác(0,1,2),(0,2,3); barycentric sinh UV, bỏ tam giác suy biến, gate mọi alpha pixel có UV.

Ground hai bank16 đoạn liên tục UV((column+u)/4,(row+v)/4), chọn theo world coordinates/hash/Perlin hiện hành. Dải11% sát cạnh blend về màu chung(108,126,70), giảm diamond khi bank đổi; có thể làm mềm chi tiết sát cạnh, không phải Wang tile. Lấy mẫu tuần hoàn bilinear và blend dải6% đối diện biên nguồn để giảm seam ảnh gen. Không khẳng định tất cả cạnh pixel-perfect.

Ramp/cap lấy vật liệu theo UV mặt và sáng theo normal từ heights, light(-.35,1,-.45). Wall hệ số.91/.74. Cap là mặt đá phẳng tạo hình theo shared corners, không dùng painted boulder lặp. Bốn decor giữ nguồn sạch người dùng, lưu riêng ArtSource/Terrain/JadeSlate/Decorations, không tạo resource entity.

## Build và đánh giá

[Quy trình Unity](../../Tools/README.md) mô tả importer và validator hiện hành. Các công cụ Python đã bị loại bỏ; dùng PNG đã lưu trong Assets, không chạy lại pipeline sinh ảnh offline.

Ramp mask1..E dùng đất nung cam nâu(185,112,66) phủ toàn mặt tới mép, giữ chi tiết độ sáng của vật liệu nguồn; dải giữa theo hướng lên dốc sáng thêm tối đa4%, bán rộng0.30UV. Ramp không blend về cỏ ở biên để tránh viền xanh. Mask0/F phẳng không đổi; alpha, pivot và hình học giữ nguyên. [Ảnh Main](../../Artifacts/TerrainVisual/main-current/ramp-8.png).

1. Dùng 78 PNG hiện hành đã lưu trong Assets; giữ canvas, alpha, pivot và metadata. Khi đổi artwork, chuẩn bị PNG tương thích bằng công cụ authoring bên ngoài.
2. Refresh Unity; RTS/Terrain/Import Jade Slate Material Library.
3. Importer [TerrainMaterialAuthoring](../../Assets/_Project/Tests/Editor/MapGeneration/TerrainMaterialAuthoring.cs) cấu hình texture và chạy fixture canonical.
4. Lab regression, Main capture theo chương06.
5. Chọn RTS/Terrain/Use Validated Jade Slate In Main để gán theme đã kiểm tra; backup Main và từ chối save nếu scene có sửa khác chưa lưu.

[Bộ mẫu](../../ArtSource/Terrain/AtlasTemplates/manifest.json) và [viewer](../../ArtSource/Terrain/TerrainReferenceKit/index.html) giữ ảnh JadeSlate hiện hành. ZIP và công cụ rebuild đã loại bỏ. Sample/mask dùng kiểm tra geometry.

Archive code/assets và lab/docs đã được loại khỏi repository ngày 2026-10-10; không còn bản phục hồi ZIP cục bộ.

# Map Generation: topology, grid, spawn và resource

Cập nhật 2026-10-10. Main dùng SharedVertexSlopes; generator quyết định topology, mountain mask, cliff/ramp, spawn và dữ liệu gameplay. Phần hiển thị có bộ nghiên cứu độc lập tại [Terrain Visualizer](../TerrainVisualizer/README.md). Các trang visual cũ bên dưới chỉ giữ redirect để tương thích liên kết.

## Bắt đầu

- Chạy/chỉnh Main: [hướng dẫn Unity](MapGen_Unity_ReadingGuide.md).
- Designer tạo bộ art mới: [atlas/art pipeline](../TerrainVisualizer/05_Atlas_ArtPipeline.md).
- Lập trình MapGen: [rules](MapGen_Unity_Rules.md), [dữ liệu map](MapGen_DataTexture_Specification.md), [config](MapGenConfig.md).
- Lập trình visual: [kiến trúc](../TerrainVisualizer/01_PhamVi_KienTruc.md), [thuật toán](../TerrainVisualizer/03_ThuatToan_Tile_Nui.md), [lab](../TerrainVisualizer/06_Lab_BangChung.md).

## Tra cứu

| Nội dung | Tài liệu |
|---|---|
| Camera, projection, batching, giới hạn | [Terrain Visualizer: projection](../TerrainVisualizer/02_PhepChieu_RenderOrder.md) |
| Metadata cliff/ramp | [Terrain Visualizer: tile algorithms](../TerrainVisualizer/03_ThuatToan_Tile_Nui.md) |
| Config và rebake | [MapGenConfig](MapGenConfig.md) |
| Catalog, quota, vị trí resource | [Resource config](MapGen_ResourceSpawn_Config.md) |
| Snap và footprint | [Grid snapper](GridSnapper_ResourceSpawn.md) |
| Player/starting economy | [Bootstrap](PlayerBootstrap.md) |
| Renderer lifecycle và danh sách file | [Visualizer architecture](../TerrainVisualizer/01_PhamVi_KienTruc.md) |
| Bộ nghiên cứu visual hiện hành | [Terrain Visualizer](../TerrainVisualizer/README.md) |
| Polish4–6, núi theo cụm, bố cục và kết quả | [Terrain polish remaining](TerrainPolish_Remaining.md) |
| Atlas sample/mask/guide và lệnh split | [Atlas authoring kit](../../ArtSource/Terrain/AtlasTemplates/README.md) |

CompetitiveTerrain/StartingSpawn và [ServerMapLoading](../Multiplayer/ServerMapLoading.md) chứa thiết kế mở rộng; không phải mọi đề xuất trong đó đã triển khai.

## Code cần đọc

| Lớp | Code |
|---|---|
| Sinh map | TerrainGeneration, SharedVertexTerrainGeneration |
| Grid/bake/cost | GridAuthoring, GridComponent, GridInitSystem, GridHelper |
| Resource | MapResourcePlanner, PlayerBootstrapSystem |
| Art/schema | TerrainTheme, CanonicalIsometricSpriteAuthoring |
| Render | TerrainMapRenderer, TerrainVisualCompiler, TerrainVisualSurface, TerrainChunkRenderer, CanonicalIsometricSprites |
| Camera/đối tượng | IsometricTerrainCamera, TerrainVisualOffsetSystem, TerrainVisualRestoreSystem |

Các class trên nằm trong Assets/_Project/Scripts hoặc Tests/Editor. Texture schema là hợp đồng art, không phải định dạng truyền map multiplayer.

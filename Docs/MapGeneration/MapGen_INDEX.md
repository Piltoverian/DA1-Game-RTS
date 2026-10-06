# MapGen Unity — mục lục các rule

Multiplayer đã chọn [server tạo map và truyền kết quả cho client](../Multiplayer/ServerMapLoading.md) ngày 2026-10-06. Đây là hướng triển khai tiếp theo, chưa có pipeline mạng trong code.

Bản gameplay C# hiện tại, cập nhật 2026-10-06. Chọn một nhóm để mở đúng phần trong file giải thích; bảng code bên dưới chỉ tới file triển khai.

| Nhóm rule | Nội dung tra cứu | File giải thích |
|---|---|---|
| 1. Grid và schema | Index, tọa độ, dữ liệu ô, walkable chung, kích thước | [Grid và dữ liệu một ô](MapGen_Unity_Rules.md#grid-schema) |
| Quy ước tính index | Dùng chung GridHelper; phân biệt width của cell, vertex và chunk | [GridHelper và chiều rộng hàng](MapGen_Unity_Rules.md#grid-helper) |
| 2. RNG và height | Seed, Perlin/fBm, wavelength, warp/ridge, quantize, smoothing | [Noise và tầng cao](MapGen_Unity_Rules.md#noise-height) |
| 3. Spawn | Sector/jitter, khoảng cách, core phẳng, vùng bảo vệ | [Spawn và vùng bảo vệ](MapGen_Unity_Rules.md#spawn) |
| 4. Núi | Noise mask, lấp khe, level khối núi, cấm đào ramp | [Núi](MapGen_Unity_Rules.md#mountain) |
| 5. Cliff | Đánh phía cao, bề dày band, góc lõm | [Cliff ở phía cao](MapGen_Unity_Rules.md#cliff) |
| 6. Ramp | Run thẳng, direction, portal hợp lệ, width, spacing, quota n/12 | [Các rule ramp](MapGen_Unity_Rules.md#ramp) |
| 7. Kết nối | Một đảo, nối component, lấp túi nhỏ | [Kết nối đảo](MapGen_Unity_Rules.md#connectivity) |
| 8. Nhận hoặc bỏ map | Giới hạn input, 16 candidate, fallback, validation | [Retry và validation](MapGen_Unity_Rules.md#retry-validation) |
| 9. Gameplay ECS | Bake grid, cost 1/255, init một lần, obstacle động, lệnh Move | [Grid và movement](MapGen_Unity_Rules.md#gameplay-grid) |
| 10. Visual | Tile ID, rotation, cliff/ramp/ground, layer, giới hạn art | [Compiler và sprite](MapGen_Unity_Rules.md#visual) |
| 12. Config và resource | Catalog ID/type/prefab, tier, main balance, quota/range và luồng spawn | [Resource config và spawn](MapGen_ResourceSpawn_Config.md) |
| 11. Theme và render | Sprite theo tên, thay texture, chunkSize, vòng đời mesh/material | [Theme và chunk](MapGen_Unity_Rules.md#theme-chunk) |

## Chỉ mục code

| Phần | File triển khai |
|---|---|
| Config và resource planner | [MapGenConfig.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/MapGenConfig.cs>), [MapResourcePlanner.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/MapResourcePlanner.cs>) |
| Settings, spawn, height, núi, cliff, ramp, connectivity, retry | [TerrainGeneration.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainGeneration.cs>) |
| Perlin/fBm và RNG | [MapGenerator.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/MapGenerator.cs>), [MapGenRNG.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/MapGenRNG.cs>) |
| Schema và direction reference | [GridComponent.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Grid/GridComponent.cs>) |
| Tọa độ và index dùng chung | [GridHelper.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Helpers/GridHelper.cs>) |
| Bake, init và cập nhật cost | [GridAuthoring.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Grid/GridAuthoring.cs>), [GridInitSystem.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Grid/GridInitSystem.cs>), [CostChangeSystem.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Grid/Cost/CostChangeSystem.cs>) |
| Ánh xạ terrain sang sprite | [TerrainVisualCompiler.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainVisualCompiler.cs>) |
| Theme và chunk renderer | [TerrainTheme.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainTheme.cs>), [TerrainMapRenderer.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainMapRenderer.cs>) |
| Lệnh Move và field | [MovementAgentAPI.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/CommandBridge/API/MovementAgentAPI.cs>), [FlowFieldAssignmentSystem.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/CommandBridge/FlowFieldAssignmentSystem.cs>), [TargetRequestCleanupSystem.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/CleanUpSystem/TargetRequestCleanupSystem.cs>) |
| Setup vị trí unit | [SetupUnitDefaultPositionSystem.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Overrides/SetupUnitDefaultPositionSystem.cs>) |

## Thao tác và tham khảo

- [MapGenConfig trong Inspector](MapGenConfig.md): entrypoint cấu hình map, bootstrap, AmountPerMine và thư mục test Assets/_Project/Tests/Fixtures/MapGeneration.
- [Catalog và resource spawn](MapGen_ResourceSpawn_Config.md): tài liệu đọc/góp ý về prefab mapping, cấp giá trị, main cố định và các range.
- [Kế hoạch spawn nhà và quân](MapGen_StartingSpawn_Plan.md): loadout, footprint, owner, population, production và authority multiplayer.
- [Player bootstrap chung](PlayerBootstrap.md): cấu hình roster/settings dùng chung và spawn đầu trận; kết quả test cũ và giới hạn bản hiện tại.
- [Grid snapper và resource spawn](GridSnapper_ResourceSpawn.md): footprint vuông N×N sau bake, mỏ đầu trận và smoke test Main.
- [Chạy Main và thay texture trong Inspector](MapGen_Unity_ReadingGuide.md).

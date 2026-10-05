# MapGen Unity — mục lục các rule

Bản gameplay C# hiện tại, cập nhật 2026-10-05. Chọn một nhóm để mở đúng phần trong file giải thích; bảng code bên dưới chỉ tới file triển khai.

| Nhóm rule | Nội dung tra cứu | File giải thích |
|---|---|---|
| 1. Grid và schema | Index, tọa độ, dữ liệu ô, walkable chung, kích thước | [Grid và dữ liệu một ô](MapGen_Unity_Rules.md#grid-schema) |
| 2. RNG và height | Seed, Perlin/fBm, wavelength, warp/ridge, quantize, smoothing | [Noise và tầng cao](MapGen_Unity_Rules.md#noise-height) |
| 3. Spawn | Sector/jitter, khoảng cách, core phẳng, vùng bảo vệ | [Spawn và vùng bảo vệ](MapGen_Unity_Rules.md#spawn) |
| 4. Núi | Noise mask, lấp khe, level khối núi, cấm đào ramp | [Núi](MapGen_Unity_Rules.md#mountain) |
| 5. Cliff | Đánh phía cao, bề dày band, góc lõm | [Cliff ở phía cao](MapGen_Unity_Rules.md#cliff) |
| 6. Ramp | Run thẳng, direction, portal hợp lệ, width, spacing, quota n/12 | [Các rule ramp](MapGen_Unity_Rules.md#ramp) |
| 7. Kết nối | Một đảo, nối component, lấp túi nhỏ | [Kết nối đảo](MapGen_Unity_Rules.md#connectivity) |
| 8. Nhận hoặc bỏ map | Giới hạn input, 16 candidate, fallback, validation | [Retry và validation](MapGen_Unity_Rules.md#retry-validation) |
| 9. Gameplay ECS | Bake grid, cost 1/255, init một lần, obstacle động, lệnh Move | [Grid và movement](MapGen_Unity_Rules.md#gameplay-grid) |
| 10. Visual | Tile ID, rotation, cliff/ramp/ground, layer, giới hạn art | [Compiler và sprite](MapGen_Unity_Rules.md#visual) |
| 11. Theme và render | Sprite theo tên, thay texture, chunkSize, vòng đời mesh/material | [Theme và chunk](MapGen_Unity_Rules.md#theme-chunk) |

## Chỉ mục code

| Phần | File triển khai |
|---|---|
| Settings, spawn, height, núi, cliff, ramp, connectivity, retry | [TerrainGeneration.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MapGen/TerrainGeneration.cs>) |
| Perlin/fBm và RNG | [MapGenerator.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MapGen/MapGenerator.cs>), [MapGenRNG.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MapGen/MapGenRNG.cs>) |
| Schema và direction reference | [GridComponent.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MovementAgent/Grid/GridComponent.cs>) |
| Bake, init và cập nhật cost | [GridAuthoring.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MovementAgent/Grid/GridAuthoring.cs>), [GridInitSystem.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MovementAgent/Grid/GridInitSystem.cs>), [CostChangeSystem.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MovementAgent/Grid/Cost/CostChangeSystem.cs>) |
| Ánh xạ terrain sang sprite | [TerrainVisualCompiler.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MapGen/TerrainVisualCompiler.cs>) |
| Theme và chunk renderer | [TerrainTheme.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MapGen/TerrainTheme.cs>), [TerrainMapRenderer.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MapGen/TerrainMapRenderer.cs>) |
| Lệnh Move và field | [MovementAgentAPI.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MovementAgent/CommandBridge/API/MovementAgentAPI.cs>), [FlowFieldAssignmentSystem.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MovementAgent/CommandBridge/FlowFieldAssignmentSystem.cs>), [TargetRequestCleanupSystem.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MovementAgent/CleanUpSystem/TargetRequestCleanupSystem.cs>) |
| Setup vị trí unit | [SetupUnitDefaultPositionSystem.cs](<../../Assets/Scripts/Game(New Simulation Logic)/Movement/SetupUnitDefaultPositionSystem.cs>) |

## Thao tác và tham khảo

- [Kế hoạch spawn nhà và quân](MapGen_StartingSpawn_Plan.md): loadout, footprint, owner, population, production và authority multiplayer; chưa triển khai.
- [Chạy Main và thay texture trong Inspector](MapGen_Unity_ReadingGuide.md).
- [Lab HTML kết hợp](Labs/Lab5_6/Lab5_6_Combined_Map_Texture.html): tham khảo trực quan thuật toán JS; không thay thế rule Unity hiện tại.
- [Ghi chú dọn dẹp](Archive/TerrainAssets-2026-10-05.md): các preview/test/ảnh thử nghiệm đã xóa.
- [Tài liệu thuật toán lab trước port](MapGen_Lab5_AcceptedAlgorithm.md), [kế hoạch port cũ](MapGen_Lab6_Unity_PortPlan.md): lịch sử quyết định. Khi khác code Unity hiện tại, đọc nhóm rule tương ứng ở bảng trên.

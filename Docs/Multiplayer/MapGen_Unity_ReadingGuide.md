# Terrain gameplay trong Unity

Pipeline hiện tại chỉ phục vụ gameplay; scene preview, menu preview/check, debug hook, Gizmos và harness Tests/TerrainPort đã được bỏ.

## Chạy trong Main

1. GridAuthoring trên plane trong EntitySubscene bật generateTerrain và có terrainSettings.
2. Baker gọi TerrainGeneration.Generate, ghi GridTerrain và GridSpawnCell vào entity grid. Plane gốc bị DisableRendering nhưng vẫn giữ collider.
3. GridInitSystem tạo cost/island từ walkable rồi tắt sau lần khởi tạo đầu tiên.
4. TerrainMapRenderer trong Main chờ subscene có grid đã bake, gọi TerrainVisualCompiler và tạo mesh theo chunk. Renderer không sinh một map riêng.
5. MovementAgent dùng grid cost/island và flowfield hiện có.

Cliff bị chặn ở ô cao có sprite cliff, kể cả góc lõm. Ramp mở đường xuyên dải cliff phía cao. Các kiểm tra bảo vệ spawn, núi, crossing, khoảng cách ramp và một đảo walkable vẫn nằm trong generator; chúng quyết định candidate có hợp lệ cho gameplay hay không.

## Thay texture

Tạo asset qua Create > RTS > Terrain Theme hoặc duplicate V17Theme.asset. Gán các Sprite theo tên trong nhóm Cliffs, Ramps và Ground, rồi gán theme vào TerrainMapRenderer trong Main. Theme có cliffStraight, cliffOuterCorner, cliffInnerCorner, rampSingle, rampLeft, rampMiddle, rampRight, groundGrass và groundSoil; không dùng array trong Inspector.

Dùng Sprite Single/Full Rect, giữ hướng gốc tương ứng của bộ V17. Material mặc định nằm ở Assets/MapGen/Terrain.mat. Đổi theme không đổi dữ liệu địa hình hoặc thuật toán di chuyển.

## Code đang dùng

- GridHelper.cs: nguồn chung cho cell ↔ index và world ↔ cell; xem [quy ước chiều rộng hàng](MapGen_Unity_Rules.md#grid-helper) khi dùng buffer vertex hoặc chunk.
- TerrainGeneration.cs: settings, spawn, noise/height, núi, cliff, ramp và kiểm tra candidate.
- MapGenerator.cs / MapGenRNG.cs: Perlin, fBm và RNG.
- TerrainVisualCompiler.cs: ánh xạ địa hình sang loại sprite và rotation.
- TerrainTheme.cs: các trường Sprite có tên và material.
- TerrainMapRenderer.cs: dựng mesh gameplay từ grid đã bake, giải phóng mesh/material khi component bị tắt hoặc hủy.
- GridAuthoring.cs / GridInitSystem.cs: bake grid và khởi tạo cost/island.

Các bộ ảnh cũ, output review và file archive đã được xóa khỏi project để tiết kiệm dung lượng; xem [ghi chú dọn dẹp](Archive/TerrainAssets-2026-10-05.md). Các lab HTML là tài liệu tham khảo ngoài Unity, không nằm trong pipeline gameplay.

# MapGenConfig: cấu hình map hiện hành

Cập nhật 2026-10-09. Asset: [MapGenConfig.asset](../../Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset).

GridAuthoring.Config và PlayerBootstrapAuthoring.MapConfig trong Main/EntitySubscene cùng tham chiếu asset này.
Đổi config rồi rebake subscene trước khi Play. Khi có config, các trường config ưu tiên hơn các trường authoring cũ.

| Phần | Điều chỉnh |
| --- | --- |
| MapSize | Số ô mỗi cạnh; cellsize = bounds plane / MapSize |
| MinimumCellSize | Cell size tối thiểu chấp nhận; plane phải đủ lớn và vuông |
| GenerateTerrain | Bật/tắt sinh terrain |
| Terrain | Seed, số spawn, protected radius, height/noise, cliff/ramp |
| PlayerBootstrap | Tham chiếu balance chung: worker, age, số dư đầu trận |
| Resources | Catalog prefab, tier, main cố định, quota/range/spacing các cluster |

Terrain.players cần đủ spawn slot của roster; roster/civ vẫn ở PlayerBootstrapAuthoring.

## Đọc phần tài nguyên

Xem **[Tài nguyên trên map: catalog, cluster và spawn](MapGen_ResourceSpawn_Config.md)** để đọc bảng mapping, ý nghĩa giá trị/trữ lượng, luật tier, main balance, cách tính Range, luồng instantiate và giới hạn kiểm tra hiện tại.

Resources tạm nằm chung trong MapGenConfig. Số dư PlayerBootstrap.StartingResources khác với trữ lượng các mỏ ngoài map.

## Trữ lượng mỏ và nơi đặt bộ test

Mở **Resources > Catalog > AmountPerMine** để nhập trữ lượng từng biến thể mỏ. Giá trị bắt buộc > 0; preset gold.common là 1500.
Không còn Amount trong ResourceAuthoring/prefab và không còn fallback về lượng prefab.
ResourceNodeData.Amount vẫn tồn tại ở runtime để lưu lượng còn lại khi khai thác.

Toàn bộ asset/config tạm và script Editor test nằm ở Assets/_Project/Tests/Fixtures/MapGeneration. Di chuyển kèm meta nên GUID scene/registry giữ nguyên.
Thay config rồi rebake và bắt đầu trận mới; không tự cập nhật lượng mỏ đã spawn trong trận đang chạy.

## Grid chỉ cập nhật khi cần

- GridIsland chỉ dựng lại khi islandGeneration khác generation; không ghi GridComponent lúc idle.
- CostChange chỉ đánh dirty khi cost thực sự đổi; request ngoài map bị bỏ, request cùng cost không tăng version.
- Heartbeat gom thay đổi trong 12 fixed ticks rồi tăng generation.
- FlowFieldInvalidation bỏ qua tick sau khi xử lý hết version hiện tại.
- BlockageGridBakeSystem bỏ qua khi query bake/cleanup đều rỗng.
- Movement/spatial vẫn cập nhật mỗi tick vì unit di chuyển không cần đổi grid version.

Xem [Grid snapper và resource spawn](GridSnapper_ResourceSpawn.md) cho footprint sau bake; xem [Player bootstrap](PlayerBootstrap.md) cho roster/population/Ready.

## Terrain và texture

Main dùng SharedVertexSlopes. Terrain settings đổi phân bố cao độ, spawn, cliff band và portal ramp; TerrainTheme đổi PNG hiển thị. Cellsize tính từ bounds/MapSize, không từ MinimumCellSize hoặc kích thước ảnh. Cao độ canonical theo metrics128/32 và camera30°; mesh và movement vẫnY=0.

Các thông số núi gồm mountainWavelength, mountainThreshold, mountainDensity, mountainSpawnBuffer và mountainMinimumClusterCells. Mặc định trong code giữ hành vi cũ; preset Main sau lab dùng26/0,20/0,85/8/6. layoutClearings bật năm arena với battlefieldRadius14, vẫn phải qua portal/connectivity/spawn validation. [Giải thích công thức, proxy đánh giá và kết quả](TerrainPolish_Remaining.md).

Sau đổi gen/config: Stop, rebake subscene và chạy validation. Sau đổi art: import, chọn theme và Play lại. [Hướng dẫn sử dụng](MapGen_Unity_ReadingGuide.md), [làm texture](MapGen_TextureAuthoring.md), [rules](MapGen_Unity_Rules.md).

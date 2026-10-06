# Bộ test MapGen và TempCiv

Thư mục này gom prefab, definition, config và script Editor dùng để thử Main. File được chuyển kèm meta, giữ GUID scene/registry.

## File cần mở

- MapGenConfig.asset: grid/terrain, tham chiếu bootstrap và Resources.Catalog.
- BootstrapSettings.asset: worker, age, số dư đầu trận; mỏ chỉ được cấu hình trong MapGenConfig.Resources.
- TempCiv.asset: civilization thử, dùng TempWorker và TempTownHall.
- Editor/BootstrapSpawnSmokeTest.cs: menu kiểm tra planner riêng và smoke Main.

## Trữ lượng mỏ

Config hiện có gold.common → Gold → TempGold.prefab (bản sao Crystal), AmountPerMine=1500, footprint 1×1.
ResourceAuthoring/prefab không chứa Amount. Baker tạo template Amount=0/PendingConfig; bootstrap gán trữ lượng từ config rồi bỏ PendingConfig khi spawn.
AmountPerMine phải >0, không có fallback về prefab. ResourceNodeData.Amount là lượng còn lại ở runtime.
Số dư StartingResources và giá mua unit/building không phải trữ lượng mỏ.

Main dùng DistanceCells/MinesPerCluster cố định và cùng recipe cho mọi player. Nhóm secondary/advantage/contested dùng ClusterMin/Max. TempWood/TempFood còn là asset tạm chưa được catalog sử dụng.

## Nhà, worker và registry

TempWorker dựa trên FriendUnit WithComand với mesh/material Worker cũ; TempTownHall là bản sao TownHall, GridScale 7 (7×7 ô).
GameReg đăng ký Worker, TownHall, Gather, Build, Storage, TrainWorker và TempCiv.
Roster Main: player 1–4, slot 0–3; bootstrap chung 5 worker/player, 300 Gold, 500 Wood, 200 Food; nhà full HP/capacity 20; worker gather/build, tech tree thử rỗng.

## Đọc docs và kiểm tra

- [MapGenConfig](../../../../../Docs/MapGeneration/MapGenConfig.md).
- [Resource catalog, tier, main balance và spawn](../../../../../Docs/MapGeneration/MapGen_ResourceSpawn_Config.md).
- Tools > MapGen > Test resource planner only: fixture riêng, không vào Play.
- Tools > MapGen > Test Main resource spawn and grid snapper: smoke Main.
- Báo cáo nằm trong Artifacts/SpawnSmoke. Build và fixture mới đã PASS; Play Mode Main chưa test lại sau thay đổi config-only stock.

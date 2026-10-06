# Player bootstrap chung cho tất cả player

Triển khai ngày 2026-10-06. Một roster và một `PlayerBootstrapSettings` cho toàn bộ trận. Civ chỉ chọn `TownHallPrefab` và `StartWorker`; số worker, tài nguyên và tuổi đầu trận lấy cùng settings, không cấu hình riêng từng player.

## Cấu hình trong Main

1. Tạo asset `Create > Game > Player Bootstrap Settings`. Điền `StartingWorkerCount`, `StartingAge`, `StartingResources` (mỗi loại một lần) và `PlacementRadiusCells`. Bán kính tính bằng cell, phải nằm trong vùng spawn được terrain bảo vệ.
2. Thêm đúng một `PlayerBootstrapAuthoring` vào SubScene gameplay. Trong Main, gán MapConfig để dùng PlayerBootstrap từ config; Settings riêng chỉ là fallback khi không có MapConfig. Điền toàn bộ player trong `Players`: `PlayerId`, `SpawnSlot`, `Civilization`. ID và slot không trùng; slot tra cứu `GridSpawnCell.playerId`, không mặc định bằng player ID.
3. Bỏ các `PlayerContextAuthoring` riêng lẻ trong SubScene dùng bootstrap. Baker tạo context, technology, pending tech, resource và cache cho mọi player từ settings chung. Không giữ đội hình đầu trận đặt tay cho cùng roster.
4. Dùng GenerateTerrain trong MapGenConfig (hoặc GridAuthoring.generateTerrain khi không có config) và số terrain spawn phù hợp roster. Registry cần bake civ (`BakeAll` trong gameplay) và prefab khởi đầu. Town hall cần `BuildingComponent`, `BuildingConstruction`, `MainBaseTag`, `BlockageData` cost 255; worker cần `UnitComponent`, `MovementAgentComponent`. Cả hai cần owner, health, selection và local transform trên root.
5. Chạy Main, xem `PlayerBootstrapState.Phase`: `Waiting → Finalizing → Ready`. `Failed` ghi một lỗi, không tạo một phần roster và không tự retry. Sửa cấu hình rồi bắt đầu trận mới.

## Luồng runtime

- Chờ cost/island và mọi request blockage hiện có xử lý xong.
- Resolve civ/prefab và mapping cho toàn bộ roster. Reserve tất cả footprint nhà trước, sau đó tìm đủ worker trên lưới tạm; ô unit riêng và nối tới vòng đi quanh nhà. Nhà cần footprint phẳng, cùng tầng, không cliff/ramp/núi và vòng đi được bên ngoài.
- Nếu có MapGenConfig.Resources, dùng Catalog và cluster planner sau nhà, trước worker. Main bắt buộc đủ và cùng recipe; nhóm optional báo thiếu quota. Đọc [resource config và luồng spawn](MapGen_ResourceSpawn_Config.md).
- Mỏ trên bản đồ chỉ lấy cấu hình từ MapGenConfig.Resources. Không có MapGenConfig hoặc Resources.Enabled=false thì bootstrap không tạo mỏ fallback. Mỏ là trung lập và không đăng ký population.
- Mỏ template có Amount=0/PendingConfig; khi instantiate, bootstrap gán AmountPerMine từ catalog và bỏ PendingConfig trước khi mỏ hoạt động. AmountPerMine bắt buộc > 0; prefab không lưu trữ lượng.
- Khi toàn bộ bố trí hợp lệ, tạo nhà Completed/full HP và worker/full HP, giữ rotation/scale/pivot Y prefab, gán owner/selection đồng nhất. Không trừ tài nguyên và không cộng population thủ công.
- Để blockage, population và setup movement xử lý. Chờ generation/island cập nhật và mọi entity đăng ký population trước Ready. `GridIslandSystem` tiếp tục theo dõi generation thay vì tự tắt.
- Command queue, building placement và production chờ Ready khi có bootstrap. Scene chưa dùng bootstrap giữ luồng hiện hành.

Bootstrap và ready system chỉ chạy trong local/server simulation world. Đây là điểm nối authority; chưa triển khai replication, lobby hoặc reset/regenerate trận trong cùng world.

## Kiểm tra nghiệm thu Main

So sánh ít nhất hai player với ID khác slot: cùng số worker/tài nguyên/age, đúng prefab theo civ, đúng owner/selection, full HP, capacity/used population đúng. Chạy nhiều frame không spawn thêm. Thử ID/slot trùng, prefab thiếu, footprint thiếu đất và loadout quá lớn: không player nào được spawn một phần. Thử chặn ô ưu tiên và xóa nhà: cost/island cập nhật, movement không đi xuyên footprint.

Đã thêm bộ asset test ở `Assets/_Project/Tests/Fixtures/MapGeneration` và cấu hình roster 4 player trong Main/EntitySubscene. Smoke test Main headless ở phiên bản resource cũ, với Burst compilation tắt, đã qua baking, spawn nhà/quân/resource, physics/blockage và population; xem [kết quả và giới hạn](GridSnapper_ResourceSpawn.md). Chưa thay đổi tìm vị trí xuất quân của production. Luồng này dùng mặt phẳng gameplay Y hiện hành; sampler cao độ 3D thuộc kế hoạch height riêng.

Kết quả headless cũ không chứng nhận catalog/tier/main policy hiện tại. Xem trạng thái kiểm tra và giới hạn trong [tài liệu resource](MapGen_ResourceSpawn_Config.md#8-đọc-báo-cáo-và-kiểm-tra).

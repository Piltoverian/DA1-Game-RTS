# Grid snapper và resource đầu trận

`GridSnapperAuthoring.GridScale` là **số ô mỗi cạnh**, không phải world scale. Đặt 7 nghĩa là footprint vuông 7×7 ô. Cạnh world bằng `7 * GridComponent.cellsize`.

## Sau baking

Gắn `GridSnapperAuthoring` và BoxCollider trên root prefab công trình/resource. Root cần không có parent, rotation identity và scale dương. `GridSnapperBakingSystem` chạy trong PostBakingSystemGroup khi grid và transform đã bake: tính scale từ collider nguồn, đưa tâm XZ collider về pivot root, scale art/child qua PostTransformMatrix, tạo physics box mới và sửa BlockageData cùng kích thước. Blob collider được đăng ký trong BlobAssetStore của baking; không mutate collider dùng chung.

Source baking giữ lại kích thước gốc, nên rebake không nhân scale nhiều lần. GridFootprint lưu Cells/WorldSize cho runtime. Thay cell size hoặc GridScale rồi rebake để cập nhật. Đây là thao tác trên entity sau bake; Transform của prefab GameObject nguồn không bị ghi đè.

Footprint lẻ đặt ở tâm cell; footprint chẵn đặt ở giao điểm grid. Bootstrap, preview position và building placement dùng cùng snapping. Vì vậy 7×7 chiếm đúng 49 ô, 8×8 đúng 64 ô, và snap lại vị trí không đẩy công trình sang ô khác.

Chi tiết config mới: [Catalog, main balance, range và resource spawn](MapGen_ResourceSpawn_Config.md).

## Resource đầu trận

MapGenConfig.Resources cấu hình prefab và cluster theo Lab5/6 cho map test. PlayerBootstrapSettings.StartingNodes chỉ còn là fallback khi không có MapGenConfig. Resource là entity trung lập, không tăng population; loại lấy từ ResourceAuthoring trên prefab; trữ lượng lấy AmountPerMine trong config spawn.

Bootstrap reserve tất cả nhà trước, lập bố trí resource trên lưới tạm, rồi mới chọn worker. Cluster member cần footprint trống, cùng tầng với tâm cluster, không cliff/ramp/núi và có ô tiếp cận sau toàn bộ occupancy. Main bắt buộc đủ cho mọi player; thiếu main thì fail trước instantiate. Optional cluster không đặt được sẽ bỏ nguyên cụm và báo thiếu quota. StartingNodes fallback đặt quanh spawn, yêu cầu vòng ô đi được và đủ gói. Ready chờ blockage/cost/island của cả nhà và resource.

Bộ assets/config/script test hiện nằm ở [Assets/Temp/MapGenTest](../../Assets/Temp/MapGenTest). Trữ lượng gán từ AmountPerMine khi instantiate; mỏ chưa cấp config giữ ResourceNodePendingConfig, không bị coi là mỏ cạn.

## Bộ test Main hiện tại

- TempTownHall: GridScale 7, capacity 20.
- TempGold: bản sao prefab Crystal/Gold hiện có, GridScale 1, trữ lượng config 1500/mỏ; prefab không lưu Amount.
- 4 player dùng cùng settings; mỗi player có 5 worker. Quota đọc trực tiếp MapGenConfig; main có số mỏ cố định, optional dùng ClusterMin/Max; báo placed/requested, không có mỏ Wood/Food. Xem MapGenConfig.md.
- 120 trường hợp geometry chạy trên code runtime đã compile: cell size 0.1/0.25/1/1.953125/4; cạnh 1/2/3/7/8/16; 4 origin khác nhau. Đã qua footprint N×N, square resize và snap idempotent.

Chạy kiểm tra Main bằng menu **Tools > MapGen > Test Main resource spawn and grid snapper** khi scene đã lưu và ngoài Play Mode. Test vào Main, chờ Ready tối đa 90 giây, kiểm tra kích thước footprint sau bake, cost 255, population 5/20, số entity và số spawn ổn định trong 3 giây. Kết quả ở `Artifacts/SpawnSmoke/report.txt`. Test tự thoát Play Mode sau khi kết thúc. Không lấy compile thành công làm kết quả Play Mode.

Kết quả trước khi chuyển sang MapGenConfig và chỉ Gold, ngày 2026-10-06: **PASS** trong Main headless với `-burst-disable-compilation`. Đúng 4 nhà 7×7, 20 worker, 24 mỏ 1×1; collider vật lý cũng vuông, nằm đúng tâm footprint; cost 255; population mỗi player 5/20; số spawn ổn định. Log chi tiết: `Artifacts/SpawnSmoke/unity-physics-check.log`.

Giới hạn: lượt Editor đầu crash với stack Burst compiler. Headless log còn lỗi UI cũ ở UnitController/CommandMenu/UnitImage và cảnh báo cleanup khi thoát; bài kiểm tra này xác nhận simulation/bootstrap/collider, không xác nhận tương tác UI, ảnh render hay Burst bật.

Sau thay đổi catalog/config-only stock: build và fixture planner riêng đã PASS (16/16 cluster, 41 mỏ). Đây không phải lượt test lại Play Mode Main; xem [trạng thái kiểm tra hiện tại](MapGen_ResourceSpawn_Config.md#8-đọc-báo-cáo-và-kiểm-tra).

# Lưới bộ căn lưới và tài nguyên đầu trận

`GridSnapperAuthoring.GridScale` là **số ô mỗi cạnh**, không phải thế giới ECS tỷ lệ. Đặt 7 nghĩa là vùng chiếm chỗ vuông 7 ×7 ô. Cạnh thế giới ECS bằng `7 * GridComponent.cellsize`.

## ECS mô hình căn lưới hiện hành — 2026-10-12

`GridSnapperSystem` chạy Initialization trước physics, xử lý request enableable `Snapping` cùng `ModelSnapSource`. Request được disable sau căn lưới thành công; không dùng Unity GameObject tag. mẫu đối tượng cũng được xử lý để instance thừa hưởng kích thước đã căn lưới.

- tác nhân: `MovementAgentAuthoring.diameterCells` là đường kính theo ô; 0,75 nghĩa là model/collider rộng 0,75 ô. Field serialized cũ `radius` giữ giá trị qua FormerlySerializedAs nhưng được hiểu theo đường kính; bỏ clamp bán kính tối thiểu0,75. Avoidance dùng bán kính thế giới ECS = diameterCells × cellsize / 2.
- Không có tác nhân: dùng GridScale của GridSnapperAuthoring, hoặc phương án dự phòng ModelSnapperAuthoring.GridScale.
- Model tỷ lệ từ kích thước khối va chạm nguồn; XZ khớp cạnh/đường kính, Y tỷ lệ theo hệ số nhỏ hơn của XZ. Không nhân dồn khi căn lưới lại; không di chuyển vị trí tác nhân.
- Hỗ trợ gốc box/sphere/capsule Y; compound chưa hỗ trợ. khối va chạm bộ nhớ đệm thuộc thế giới ECS; giữ collision filter, friction/restitution/combine và các trục inverse inertia bị khóa. Building cập nhật footprint/blockage và vị trí căn lưới.

Yêu cầu căn lưới lại bằng `EntityManager.SetComponentEnabled<Snapping>(entity, true)` trên thực thể đã có mã nguồn. `GridSnapperBakingSystem` dưới đây vẫn chuẩn bị vùng chiếm chỗ nhà cho validation/planner; khi chạy bộ căn lưới quản lý request mô hình. [Chi tiết luồng xử lý](EcsMapPipeline.md).

## Sau baking

Gắn `GridSnapperAuthoring` và BoxCollider trên gốc mẫu đối tượng công trình/resource. Root cần không có parent, rotation identity và tỷ lệ dương. `GridSnapperBakingSystem` chạy trong PostBakingSystemGroup khi lưới và transform đã chuyển dữ liệu sang ECS: tính tỷ lệ từ khối va chạm nguồn, đưa tâm XZ khối va chạm về điểm gốc gốc, tỷ lệ art/child qua PostTransformMatrix, tạo physics box mới và sửa BlockageData cùng kích thước. Blob khối va chạm được đăng ký trong BlobAssetStore của baking; không mutate khối va chạm dùng chung.

Source baking giữ lại kích thước gốc, nên chuyển dữ liệu sang ECS lại không nhân tỷ lệ nhiều lần. GridFootprint lưu Cells/WorldSize cho khi chạy. Thay ô size hoặc GridScale rồi chuyển dữ liệu sang ECS lại để cập nhật. Đây là thao tác trên thực thể sau chuyển dữ liệu sang ECS; Transform của mẫu đối tượng GameObject nguồn không bị ghi đè.

Footprint lẻ đặt ở tâm ô; vùng chiếm chỗ chẵn đặt ở giao điểm lưới. khởi tạo, xem trước vị trí và nhà đặt nhà dùng cùng snapping. Vì vậy 7 ×7 chiếm đúng 49 ô, 8 ×8 đúng 64 ô, và căn lưới lại vị trí không đẩy công trình sang ô khác.

Chi tiết cấu hình mới: [Catalog, chính số dư, khoảng và tài nguyên sinh đối tượng](MapGen_ResourceSpawn_Config.md).

## Tài nguyên đầu trận

`MapGenConfig.Resources` cấu hình mẫu đối tượng và cụm cho bản đồ. PlayerBootstrapSettings không cấu hình mỏ hoặc phương án dự phòng. tài nguyên là thực thể trung lập, không tăng dân số; loại lấy từ ResourceAuthoring trên mẫu đối tượng; trữ lượng lấy AmountPerMine trong cấu hình sinh đối tượng.

khởi tạo giữ chỗ tất cả nhà trước, lập bố trí tài nguyên trên lưới tạm, rồi mới chọn thợ. cụm thành viên cần vùng chiếm chỗ trống, cùng tầng với tâm cụm, không cliff/ramp/núi và có ô tiếp cận sau toàn bộ trạng thái chiếm ô. Main bắt buộc đủ cho mọi người chơi; thiếu chính thì không đạt trước tạo bản sao đối tượng. tùy chọn cụm không đặt được sẽ bỏ nguyên cụm và báo thiếu hạn ngạch. Ready chờ blockage/chi phí/vùng liên thông của cả nhà và tài nguyên.

Bộ assets/config/script kiểm thử hiện nằm ở [Assets/_Project/Tests/Fixtures/MapGeneration](../../Assets/_Project/Tests/Fixtures/MapGeneration). Trữ lượng gán từ AmountPerMine khi tạo bản sao đối tượng; mỏ chưa cấp cấu hình giữ ResourceNodePendingConfig, không bị coi là mỏ cạn.

## Bộ kiểm thử Main hiện tại

Lượt mới nhất: Main512 có 8 người chơi, 40 thợ, 8 nhà chính và 1.952 tài nguyên; hai lượt Burst bật hoàn thành 600 frame/lượt, sổ theo dõi 2.000 instance và render/camera đạt. Các số 4 người chơi và kết quả headless phía dưới thuộc fixture/lượt lịch sử.

- TempTownHall: GridScale 7, capacity 20.
- TempGold: bản sao mẫu đối tượng Crystal/Gold hiện có, GridScale 1, trữ lượng cấu hình 1500/mỏ; mẫu đối tượng không lưu Amount.
- 4 người chơi dùng cùng thiết lập; mỗi người chơi có 5 thợ. hạn ngạch đọc trực tiếp MapGenConfig; chính có số mỏ cố định, tùy chọn dùng ClusterMin/Max; báo placed/requested, không có mỏ Wood/Food. Xem MapGenConfig.md.
- 120 trường hợp hình học chạy trên mã khi chạy đã biên dịch: ô size 0.1/0.25/1/1.953125/4; cạnh 1/2/3/7/8/16; 4 origin khác nhau. Đã qua vùng chiếm chỗ N×N, square resize và căn lưới idempotent.

Chạy kiểm tra Main bằng trình đơn `Tools > MapGen > Test Main resource spawn and grid snapper` khi cảnh đã lưu và ngoài Play Mode. Test vào Main, chờ Ready tối đa 90 giây, kiểm tra kích thước vùng chiếm chỗ sau chuyển dữ liệu sang ECS, chi phí 255, dân số 5/20, số thực thể và số sinh đối tượng ổn định trong 3 giây. Kết quả ở `Artifacts/SpawnSmoke/report.txt`. Test tự thoát Play Mode sau khi kết thúc. Không lấy biên dịch thành công làm kết quả Play Mode.

Kết quả trước khi chuyển sang MapGenConfig và chỉ Gold, ngày 2026-10-06: **ĐẠT** trong Main headless với `-burst-disable-compilation`. Đúng 4 nhà 7 ×7, 20 thợ, 24 mỏ 1 ×1; khối va chạm vật lý cũng vuông, nằm đúng tâm vùng chiếm chỗ; chi phí 255; dân số mỗi người chơi 5/20; số sinh đối tượng ổn định. Log chi tiết: `Artifacts/SpawnSmoke/unity-physics-check.log`.

Giới hạn: lượt Editor đầu sự cố dừng đột ngột với stack Burst trình biên dịch. Headless nhật ký còn lỗi UI cũ ở UnitController/CommandMenu/UnitImage và cảnh báo dọn dẹp khi thoát; bài kiểm tra này xác nhận simulation/bootstrap/collider, không xác nhận tương tác UI, ảnh dựng hình hay Burst bật.

Sau thay đổi danh mục/chỉ cấu hình bộ mẫu cũ: bản dựng và dữ liệu kiểm thử bộ lập kế hoạch riêng đã ĐẠT (16/16 cụm, 41 mỏ). Đây không phải lượt kiểm thử lại Play Mode Main; xem [trạng thái kiểm tra hiện tại](MapGen_ResourceSpawn_Config.md#8-đọc-báo-cáo-và-kiểm-tra).

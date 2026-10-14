# Người chơi khởi tạo chung cho tất cả người chơi

Cập nhật 2026-10-12. Một roster và một `PlayerBootstrapSettings` cho toàn bộ trận. Civ chỉ chọn `TownHallPrefab` và `StartWorker`; số thợ, tài nguyên và tuổi đầu trận lấy cùng thiết lập, không cấu hình riêng từng người chơi. Thứ tự khi chạy nằm trong [luồng xử lý ECS](EcsMapPipeline.md).

## Cấu hình trong Main

1. Tạo tài nguyên `Create > Game > Player Bootstrap Settings`. Điền `StartingWorkerCount`, `StartingAge`, `StartingResources` (mỗi loại một lần) và `PlacementRadiusCells`. Bán kính tính bằng ô, phải nằm trong vùng sinh đối tượng được địa hình bảo vệ.
2. Thêm đúng một `PlayerBootstrapAuthoring` vào SubScene lối chơi. Trong Main, gán MapConfig để dùng PlayerBootstrap từ cấu hình; Settings riêng chỉ là phương án dự phòng khi không có MapConfig. Điền toàn bộ người chơi trong `Players`: `PlayerId`, `SpawnSlot`, `Civilization`. ID và vị trí không trùng; vị trí tra cứu `GridSpawnCell.playerId`, không mặc định bằng người chơi ID.
3. Bỏ các `PlayerContextAuthoring` riêng lẻ trong SubScene dùng khởi tạo. bộ chuyển dữ liệu sang ECS tạo context, technology, đang chờ tech, tài nguyên và bộ nhớ đệm cho mọi người chơi từ thiết lập chung. Không giữ đội hình đầu trận đặt tay cho cùng roster.
4. Dùng GenerateTerrain trong MapGenConfig (hoặc GridAuthoring.generateTerrain khi không có cấu hình) và số địa hình sinh đối tượng phù hợp roster. Registry cần chuyển dữ liệu sang ECS civ (`BakeAll` trong lối chơi) và mẫu đối tượng khởi đầu. Town hall cần `BuildingComponent`, `BuildingConstruction`, `MainBaseTag`, `BlockageData` chi phí 255; thợ cần `UnitComponent`, `MovementAgentComponent`. Cả hai cần chủ sở hữu, health, lựa chọn và cục bộ transform trên gốc.
5. Chạy Main, xem `PlayerBootstrapState.Phase`: `Waiting → Finalizing → Ready`. `Failed` ghi một lỗi, không tạo một phần roster và không tự thử lại. Sửa cấu hình rồi bắt đầu trận mới.

## Luồng khi chạy

- Chờ MapBuildState.NavigationReady, chi phí/vùng liên thông và mọi request chặn lối hiện có xử lý xong. sinh dữ liệu Requested/Failed không được đưa vào bộ lập kế hoạch.
- phân giải civ/prefab và mapping cho toàn bộ roster. giữ chỗ tất cả vùng chiếm chỗ nhà trước, sau đó tìm đủ thợ trên lưới tạm; ô lính riêng và nối tới vòng đi quanh nhà. Nhà cần vùng chiếm chỗ phẳng, cùng tầng, không cliff/ramp/núi và vòng đi được bên ngoài.
- Nếu có `MapGenConfig.Resources`, dùng Catalog và cụm bộ lập kế hoạch sau nhà, trước thợ. Main bắt buộc đủ và cùng công thức; nhóm tùy chọn báo thiếu hạn ngạch. Đọc [tài nguyên cấu hình và luồng sinh đối tượng](MapGen_ResourceSpawn_Config.md).
- Mỏ trên bản đồ chỉ lấy cấu hình từ `MapGenConfig.Resources`. Không có MapGenConfig hoặc `Resources.Enabled=false` thì khởi tạo không tạo mỏ phương án dự phòng. Mỏ là trung lập và không đăng ký dân số.
- Mỏ mẫu có Amount=0/PendingConfig; khi tạo bản sao đối tượng, khởi tạo gán AmountPerMine từ danh mục và bỏ PendingConfig trước khi mỏ hoạt động. AmountPerMine bắt buộc > 0; mẫu đối tượng không lưu trữ lượng.
- Khi toàn bộ bố trí hợp lệ, ghi lệnh ECB rồi Playback một lần: tạo nhà Completed/full HP và worker/full HP, giữ rotation/scale/pivot Y mẫu đối tượng, gán owner/selection đồng nhất. Ghi sổ theo dõi bằng AppendToBuffer với deferred thực thể; ECB remap tham chiếu khi Playback. Không gọi GetBuffer xen kẽ Instantiate/Add/Remove cho từng nút. Không trừ tài nguyên và không cộng dân số thủ công. Xem [bản vá sự cố dừng đột ngột](BootstrapCrashFix.md).
- Để chặn lối, dân số và thiết lập chuyển động xử lý. Chờ generation/island cập nhật và mọi thực thể đăng ký dân số trước Ready. `GridIslandSystem` tiếp tục theo dõi sinh dữ liệu thay vì tự tắt.
- lệnh hàng đợi, nhà đặt nhà và sản xuất chờ Ready khi có khởi tạo. Scene chưa dùng khởi tạo giữ luồng hiện hành.

khởi tạo và ready hệ thống chỉ chạy trong local/server mô phỏng thế giới ECS. Đây là điểm nối quyền điều khiển; chưa triển khai đồng bộ trạng thái, lobby hoặc reset/regenerate trận trong cùng thế giới ECS.

## Kiểm tra nghiệm thu Main

Mới nhất 2026-10-12: Main512, 8 người chơi, 5 worker/player, 1.952 tài nguyên; đủ 2.000 instance duy nhất trong sổ theo dõi. Hai lượt Burst bật (đồng bộ/mặc định) chạy 600 frame/lượt, GameplayReady, bộ dựng hình và camera cục bộ đạt; không tái hiện sự cố dừng đột ngột safety handle. Đây là kiểm tra lô Editor; các kết quả headless cũ phía dưới được giữ làm lịch sử.

So sánh ít nhất hai người chơi với ID khác vị trí: cùng số worker/tài nguyên/thời đại, đúng mẫu đối tượng theo civ, đúng owner/selection, full HP, capacity/used dân số đúng. Chạy nhiều khung hình không sinh đối tượng thêm. Thử ID/slot trùng, mẫu đối tượng thiếu, vùng chiếm chỗ thiếu đất và nhóm đối tượng khởi đầu quá lớn: không người chơi nào được sinh đối tượng một phần. Thử chặn ô ưu tiên và xóa nhà: chi phí/vùng liên thông cập nhật, chuyển động không đi xuyên vùng chiếm chỗ.

Đã thêm bộ tài nguyên kiểm thử ở `Assets/_Project/Tests/Fixtures/MapGeneration` và cấu hình roster 4 người chơi trong Main/EntitySubscene. Smoke kiểm thử Main headless ở phiên bản tài nguyên cũ, với Burst compilation tắt, đã qua baking, sinh đối tượng nhà/quân/resource, physics/blockage và dân số; xem [kết quả và giới hạn](GridSnapper_ResourceSpawn.md). Chưa thay đổi tìm vị trí xuất quân của sản xuất. Luồng này dùng mặt phẳng lối chơi Y hiện hành; sampler cao độ 3D thuộc kế hoạch độ cao riêng.

Kết quả headless cũ không chứng nhận catalog/tier/main quy tắc hiện tại. Xem trạng thái kiểm tra và giới hạn trong [tài liệu tài nguyên](MapGen_ResourceSpawn_Config.md#8-đọc-báo-cáo-và-kiểm-tra).

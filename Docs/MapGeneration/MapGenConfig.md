# MapGenConfig: cấu hình bản đồ hiện hành

Cập nhật 2026-10-12. Asset: [MapGenConfig.asset](../../Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset).

`GridAuthoring.Config` và PlayerBootstrapAuthoring.MapConfig trong Main/EntitySubscene cùng tham chiếu tài nguyên này.
Đổi cấu hình rồi chuyển dữ liệu sang ECS lại subscene trước khi Play. Khi có cấu hình, các trường cấu hình ưu tiên hơn các trường thiết lập dữ liệu cũ.

bộ chuyển dữ liệu sang ECS chỉ ghi GridComponent, MapGenerationRecipe và bộ đệm rỗng; địa hình sinh ở khi chạy ECS một lần khi Requested. Ready của bản đồ gồm điều hướng và khởi tạo; renderer/camera có trạng thái hiển thị riêng. [luồng xử lý và tệp](EcsMapPipeline.md), [số đo mới nhất](../Maintenance/VerificationHistory.md).

| Phần | Điều chỉnh |
| --- | --- |
| MapSize | Số ô mỗi cạnh; cellsize = giới hạn plane / MapSize |
| MinimumCellSize | Cell size tối thiểu chấp nhận; plane phải đủ lớn và vuông |
| GenerateTerrain | Bật/tắt sinh địa hình |
| địa hình | hạt giống, số sinh đối tượng, protected bán kính, height/noise, cliff/ramp |
| PlayerBootstrap | Tham chiếu số dư chung: thợ, thời đại, số dư đầu trận |
| tài nguyên | Catalog mẫu đối tượng, tầng, chính cố định, quota/range/spacing các cụm |

địa hình.players cần đủ sinh đối tượng vị trí của roster; roster/civ vẫn ở PlayerBootstrapAuthoring.

## Đọc phần tài nguyên

Xem **[Tài nguyên trên bản đồ: danh mục, cụm và sinh đối tượng](MapGen_ResourceSpawn_Config.md)** để đọc bảng mapping, ý nghĩa giá trị/trữ lượng, luật tầng, chính số dư, cách tính khoảng, luồng tạo bản sao đối tượng và giới hạn kiểm tra hiện tại.

tài nguyên tạm nằm chung trong MapGenConfig. Số dư PlayerBootstrap.StartingResources khác với trữ lượng các mỏ ngoài bản đồ.

## Trữ lượng mỏ và nơi đặt bộ kiểm thử

Mở **tài nguyên > Catalog > AmountPerMine** để nhập trữ lượng từng biến thể mỏ. Giá trị bắt buộc > 0; preset gold.common là 1500.
Không còn Amount trong ResourceAuthoring/prefab và không còn phương án dự phòng về lượng mẫu đối tượng.
ResourceNodeData.Amount vẫn tồn tại ở khi chạy để lưu lượng còn lại khi khai thác.

Toàn bộ asset/config tạm và script Editor kiểm thử nằm ở Assets/_Project/Tests/Fixtures/MapGeneration. Di chuyển kèm meta nên GUID scene/registry giữ nguyên.
Thay cấu hình rồi chuyển dữ liệu sang ECS lại và bắt đầu trận mới; không tự cập nhật lượng mỏ đã sinh đối tượng trong trận đang chạy.

## Lưới chỉ cập nhật khi cần

- GridIsland chỉ dựng lại khi islandGeneration khác sinh dữ liệu; không ghi GridComponent lúc đứng yên.
- CostChange chỉ đánh dirty khi chi phí thực sự đổi; request ngoài bản đồ bị bỏ, request cùng chi phí không tăng version.
- Heartbeat gom thay đổi trong 12 bước cố định bước mô phỏng rồi tăng sinh dữ liệu.
- FlowFieldInvalidation bỏ qua bước mô phỏng sau khi xử lý hết version hiện tại.
- BlockageGridBakeSystem bỏ qua khi query bake/cleanup đều rỗng.
- Movement/spatial vẫn cập nhật mỗi bước mô phỏng vì lính di chuyển không cần đổi lưới version.

Xem [lưới bộ căn lưới và tài nguyên sinh đối tượng](GridSnapper_ResourceSpawn.md) cho vùng chiếm chỗ sau chuyển dữ liệu sang ECS; xem [người chơi khởi tạo](PlayerBootstrap.md) cho roster/population/Ready.

## Địa hình và ảnh vật liệu

Main dùng SharedVertexSlopes. địa hình thiết lập đổi phân bố cao độ, sinh đối tượng, vách band và cổng dốc nối tầng; TerrainTheme đổi PNG hiển thị. Cellsize tính từ bounds/MapSize, không từ MinimumCellSize hoặc kích thước ảnh. Cao độ chuẩn theo metrics128/32 và camera30°; lưới hình học và chuyển động vẫnY=0.

Các thông số núi gồm mountainWavelength, mountainThreshold, mountainDensity, mountainSpawnBuffer và mountainMinimumClusterCells. Mặc định trong mã giữ hành vi cũ; preset Main sau bản thử nghiệm dùng26/0,20/0,85/8/6. layoutClearings bật năm arena với battlefieldRadius14, vẫn phải qua portal/connectivity/spawn kiểm tra hợp lệ. [Giải thích công thức, proxy đánh giá và kết quả](../TerrainVisualizer/README.md).

Sau đổi gen/config: Stop, chuyển dữ liệu sang ECS lại subscene và chạy kiểm tra hợp lệ. Sau đổi hình ảnh mỹ thuật: nhập, chọn bộ giao diện và Play lại. [Hướng dẫn sử dụng](MapGen_Unity_ReadingGuide.md), [làm ảnh vật liệu](../TerrainVisualizer/05_Atlas_ArtPipeline.md), [rules](MapGen_Unity_Rules.md).

# Sinh bản đồ — chọn đúng tài liệu

**Mở [hướng dẫn Main](MapGen_Unity_ReadingGuide.md) để chạy/chỉnh bản đồ — khoảng 3 phút để bắt đầu.**

| Bạn cần | Trang |
|---|---|
| Input/output,cấu hình và luật bản đồ | [Rules](MapGen_Unity_Rules.md) · [dữ liệu](MapGen_DataTexture_Specification.md) · [cấu hình](MapGenConfig.md) |
| ECS sinh dữ liệu,ready,phiên bản và ảnh trạng thái | [luồng xử lý](EcsMapPipeline.md) · [Tối ưu Unity](UnityOptimizationPort.md) |
| khởi tạo,tài nguyên và vùng chiếm chỗ | [người chơi](PlayerBootstrap.md) · [tài nguyên cấu hình](MapGen_ResourceSpawn_Config.md) · [lưới bộ căn lưới](GridSnapper_ResourceSpawn.md) |
| Height,chuyển động và dựng hình | [Height hợp đồng](PendingTerrainHeight.md) · [Obstacle điều hướng](GridObstacleSteering.md) · [Atlas địa hình](../TerrainVisualizer/README.md) |
| Đo hiệu năng và kiểm chứng | [Main hiệu năng](../Maintenance/VerificationHistory.md) · [luồng xử lý kiểm tra](../Maintenance/VerificationHistory.md) |

## Mã cần tìm

Simulation/MapGeneration giữ công thức,sinh dữ liệu,địa hình dữ liệu,dốc nối tầng trình biên dịch và căn lưới. Simulation/Players khởi tạo player/nhóm đối tượng khởi đầu/resources. Presentation/Terrain giữ renderer/surface/picking; Presentation/Camera căn lưới camera cục bộ. Movement/Height đọc dữ liệu lưới độc lập bộ dựng hình.

Runtime dùng TerrainRampCompiler; TerrainVisualCompiler cũ đã đổi tên. Renderer chỉ là luồng xử lý vật liệu atlas/độ sâu. ảnh vật liệu lược đồ dữ liệu là hợp đồng hình ảnh mỹ thuật, không phải wire format chơi nhiều người.

## Kết quả lịch sử và phạm vi còn mở

hạt giống audit/benchmark trong folder này chỉ chứng minh cấu hình và phiên bản ghi trong báo cáo. Các báo cáo đã gom vào lịch sử hợp nhất, giữ phân biệt khối lượng công việc/thuật toán; không suy số đo bản thử nghiệm hoặc Editor thành FPS người chơi bản dựng.

[Trạng thái mới nhất](../Maintenance/CurrentState.md) · [lộ trình](../Maintenance/Roadmap.md) · [Thiết kế sản xuất sinh đối tượng](MapGen_StartingSpawn_Plan.md) · [máy chủ bản đồ loading](../Multiplayer/ServerMapLoading.md)

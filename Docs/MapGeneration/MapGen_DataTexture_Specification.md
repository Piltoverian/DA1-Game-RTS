# Dữ liệu bản đồ và ranh giới với ảnh vật liệu

Cập nhật2026-10-12. GridTerrain ECS là dữ liệu bản đồ khi chạy hiện tại; bộ chuyển dữ liệu sang ECS ghi công thức. MapTerrainSnapshot có API publish terrain/spawn với revision/hash; chưa có lớp truyền dữ liệu chơi nhiều người hoặc luồng xử lý PNG/RGBA. [luồng xử lý ECS](EcsMapPipeline.md), [ServerMapLoading](../Multiplayer/ServerMapLoading.md) là hướng nối authority/transport.

## Lưới authoritative

| Dữ liệu | Vai trò |
|---|---|
| GridComponent | width,độ cao,cellsize,origin |
| GridTerrain | heightLevel,đi được,isMountain,isCliff,RampId,RampDirection |
| GridSpawnCell | Spawn đã tạo và bảo vệ |
| Vertex/góc khi được chuyển dữ liệu sang ECS | Mô tả mặt cho luồng xử lý dùng góc |
| Cost/island buffers | điều hướng, khởi tạo/tái tính từ trạng thái lưới |

Đổi cell/index/world qua GridHelper, đúng stride từng bộ đệm. lưới x,y ánh xạ worldX,Z; logicY=0. HeightLevel không tự quyết định chi phí: đi được là nguồn quyết định1/255.

## Điều kiện bản đồ

Gen giữ seed/noise/quantize, bảo vệ sinh đối tượng, núi, cliff/ramp, kết nối và thử lại. Ngoài núi, các ô kề8hướng chênh tối đa một tầng; cổng dốc nối tầng nối một tầng. Núi bị chặn; bản đồ nhận phải có một vùng liên thông đi được. Chi tiết tại [rules](MapGen_Unity_Rules.md).

## Art không phải dữ liệu lối chơi

PNG top/ramp/cliff/mountain chỉ phục vụ dựng hình. TerrainTheme không thay chi phí hoặc gen thêm núi. Không mã hóa trữ lượng mỏ trong màu đất. [ảnh sprite lược đồ dữ liệu](../TerrainVisualizer/05_Atlas_ArtPipeline.md) mô tả vị trí,canvas,điểm gốc; không phải wire protocol bản đồ.

tài nguyên catalog/quota/range nằm trong [cấu hình tài nguyên](MapGen_ResourceSpawn_Config.md); lượng còn lại lưu khi chạy tài nguyên nút. [khởi tạo](PlayerBootstrap.md) dùng sinh đối tượng và starting economy. Thay hình ảnh mỹ thuật không bắt buộc regen; thay config/gen phải chuyển dữ liệu sang ECS lại. [Sử dụng](MapGen_Unity_ReadingGuide.md), [luồng xử lý](EcsMapPipeline.md).

Lịch sử loại ảnh sprite trống: giữ16top,10cliff(mask4,8,C,D,E mỗi side),1núi;22PNGcliff alpha0 và trường tương ứng đã xóa. Renderer không pack/vẽ các vị trí đã loại. Manifest JSON cũ không còn trong repository; xem [dọn dẹp và trạng thái artwork](../TerrainVisualizer/14_SuDung_Atlas_Implementation.md).

# Dữ liệu map và ranh giới với texture

Cập nhật2026-10-09. GridTerrain ECS là dữ liệu map cục bộ hiện tại; chưa có pipeline PNG/RGBA transport multiplayer. [ServerMapLoading](../Multiplayer/ServerMapLoading.md) là hướng mở rộng.

## Grid authoritative

| Dữ liệu | Vai trò |
|---|---|
| GridComponent | width,height,cellsize,origin |
| GridTerrain | heightLevel,walkable,isMountain,isCliff,RampId,RampDirection |
| GridSpawnCell | Spawn đã tạo và bảo vệ |
| Vertex/corner khi được bake | Mô tả mặt cho pipeline dùng corner |
| Cost/island buffers | Navigation, khởi tạo/tái tính từ trạng thái grid |

Đổi cell/index/world qua GridHelper, đúng stride từng buffer. Grid x,y ánh xạ worldX,Z; logicY=0. HeightLevel không tự quyết định cost: walkable là nguồn quyết định1/255.

## Điều kiện map

Gen giữ seed/noise/quantize, bảo vệ spawn, núi, cliff/ramp, connectivity và retry. Ngoài núi, các ô kề8hướng chênh tối đa một tầng; portal ramp nối một tầng. Núi bị chặn; map nhận phải có một island walkable. Chi tiết tại [rules](MapGen_Unity_Rules.md).

## Art không phải dữ liệu gameplay

PNG top/ramp/cliff/mountain chỉ phục vụ render. TerrainTheme không thay cost hoặc gen thêm núi. Không mã hóa trữ lượng mỏ trong màu đất. [Sprite schema](MapGen_TextureSchema.md) mô tả slot,canvas,pivot; không phải wire protocol map.

Resource catalog/quota/range nằm trong [config resource](MapGen_ResourceSpawn_Config.md); lượng còn lại lưu runtime resource node. [Bootstrap](PlayerBootstrap.md) dùng spawn và starting economy. Thay art không bắt buộc regen; thay config/gen phải rebake. [Sử dụng](MapGen_Unity_ReadingGuide.md), [pipeline](MapGen_TerrainPresentation.md).

Cập nhật loại sprite trống: giữ16top,10cliff(mask4,8,C,D,E mỗi side),1núi;22PNGcliff alpha0 và field tương ứng đã xóa. Baker không tái tạo mask trống, renderer không pack/vẽ chúng. [Manifest cleanup](../Reviews/TerrainEmptySpriteCleanup_2026-10-09.json).

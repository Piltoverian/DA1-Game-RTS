# Chạy và chỉnh địa hình trong Unity

**Mở scene Main rồi bấm Play — khoảng2phút để bắt đầu nếu Unity đã nhập xong dữ liệu.**

Main dùng SharedVertexSlopes,MoonTerrain/LunarTheme.asset và camera chiếu song song. Gameplay/tầm/điều hướng dùng XZ;Y lấy độ cao địa hình và offset pivot. Thử nghiệm ép phẳng đã bỏ. [Luồng ECS](EcsMapPipeline.md).

## Chạy Main

1. Mở `Assets/_Project/Scenes/Main.unity`,chờ Unity nhập/biên dịch.
2. Kiểm tra `Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset`. Đây vẫn là cấu hình Main dù nằm trong Tests.
3. TerrainMapRenderer dùng theme `Assets/_Project/Data/Terrain/Generated/MoonTerrain/LunarTheme.asset`. Camera chỉ thu phóng/di chuyển,không xoay.
4. Sau sửa cấu hình/bộ sinh map,dừng Play,chờ SubScene bake lại rồi Play. Baker ghi công thức;MapTerrainGenerationSystem sinh địa hình khi chạy. Không thay địa hình của trận đang diễn ra.
5. MapTerrainPresentationSystem chờ TerrainReady,không sinh map thứ hai. LocalBaseCameraSnapSystem chờ renderer/BootstrapReady rồi hướng tới nhà chính cục bộ một lần. Khi tắt snapOnReady,frameRampOnLoad mới dùng trình bày ramp.

## Chỉnh thông số

| Bạn muốn đổi | Nơi chỉnh | Sau đó |
|---|---|---|
| Seed,cỡ map,player,vùng sinh | MapGenConfig | Bake lại và bắt đầu trận mới |
| Height,noise,cliff band,ramp width | Cấu hình terrain | Bake lại,kiểm tra hợp lệ |
| Màu đất/dốc/vách/núi | PNG/TerrainTheme | Nhập lại,Stop/Play |
| Thu phóng/khung nhìn | IsometricTerrainCamera | Kiểm tra Main |
| Cỡ lính | MovementAgentAuthoring.diameterCells | Đơn vị đường kính ô;bake/Play lại |

Cellsize=chiều rộng lưới/số ô. Plane rộng1000,map512cho1.953125. MinimumCellSize là giới hạn kiểm tra,không thay công thức. Camera chuẩn pitch30°,yaw45°;đổi PNG không yêu cầu đổi camera. [Art/atlas](../TerrainVisualizer/05_Atlas_ArtPipeline.md).

## Nhập bộ ảnh mới

Làm theo [workflow atlas Unity](../TerrainVisualizer/14_SuDung_Atlas_Implementation.md):atlas nguồn→importer→cập nhật cùng SO→preview/Main. Không chạy menu renderer stock đã nghỉ dùng.

## Kiểm tra

| Menu giữ tên đúng trong Unity | Nội dung |
|---|---|
| `Tools/MapGen/Test resource planner only` | Dữ liệu riêng:footprint,khoảng cách núi,mỏ khởi đầu,seed |
| `Tools/MapGen/Test Main resource spawn and grid snapper` | Scene Main:sinh resource/căn lưới |
| `RTS/Tests/Validate Current Terrain Atlas` | Atlas hiện tại |
| `RTS/Tests/Validate Selected Terrain Theme` | Theme được chọn |
| `RTS/Tests/Validate Mountain Integration` | Generation,núi,kết nối/spawn |

[Danh mục kiểm thử](../../Assets/_Project/Tests/README.md) phân biệt harness hiện hành và các harness gameplay đã xóa. Compile thành công không thay kết quả Play Mode hoặc benchmark tải.

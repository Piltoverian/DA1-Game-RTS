# Bề mặt và chọn bằng tia, độ cao ECS và làm mờ

Hiện hành2026-10-13. [Height hợp đồng](../MapGeneration/PendingTerrainHeight.md), [quy trình](14_SuDung_Atlas_Implementation.md).

## Height ECS

TerrainSurfaceData không có Camera/Texture/renderer. TerrainHeightCacheSystem đọc GridComponent/GridTerrain và `MapBuildState.Revision`, sao chép bốn góc vào NativeArray persistent khi phiên bản đổi. Khi bản đồ mất/không ready, bộ nhớ đệm Ready=false và gốc tiếp tục đang chờ. Không còn phương án dự phòng tới TerrainVisualSurface.Active.

Interpolation khớp hai top triangle00-10-11 và00-11-01; dốc nối tầng hướng vẫn được compiler/data hợp đồng xử lý. PendingHeight là enableable và TerrainHeightOffset giữ điểm gốc Y authored. Chỉ gốc bật được mẫu; assign Y=độ cao+độ lệch rồi tắt đang chờ. bước cố định đạt sau chuyển động trước physics; late-sinh đối tượng đạt trước biến đổi tọa độ. chuyển động, chiến đấu acquisition và đến đích dùng XZ; projectile hình học vẫn3D.

Không còn hệ thống viết/khôi phục LocalToWorld chỉ để hiển thị. Rendering và khối va chạm cùng dùng LocalTransform; drag lựa chọn không cộng địa hình độ cao lần nữa.

## Ground chọn bằng tia

TerrainVisualSurface là adapter máy khách đọc cùng TerrainSurfaceData. Raycast slab giới hạn vào lưới, DDA qua ô chạm tia và thử hai top triangle chính xác. Trả logical point với Y0 và visible point đúng đã lấy mẫu độ cao. Núi trang trí không có khối va chạm; chiều cao hình ảnh mỹ thuật không đổi mặt đất point hoặc nav độ cao.

lính/tài nguyên khối va chạm chọn bằng tia dùng nguyên camera tia vì khối va chạm ở cao độ địa hình. Adapter LogicalRay truyền tia nguyên trạng đã xóa. SelectManager/UnitController dùng tia camera trực tiếp và bề mặt raycast cho mặt đất phương án dự phòng.

## Fade vùng thao tác

TerrainMountainFade giữ một vật liệu cho địa hình. Con trỏ và giới hạn lính đã chọn là điểm neo; đã chọn gốc đọc10Hz, gom8vùng, tổng tối đa16anchor. Không tạo GameObject/material từng ô hoặc kiểm tra2.000 lính×mọi mặt mỗi khung hình.

Shader chỉ làm mờ mặt núi cap/ground/wall ở trước focus trong corridor screen-space. Transition khoảng0,1s, độ đục mặc định0.25. Ordered dithering bỏ pixel và nhả độ sâu tương ứng; tắt điểm neo khôi phục che khuất. Camera pan/zoom và giới hạn ảnh hưởng corridor, không đổi dữ liệu bản đồ.

Fade không làm lính đường bao/x-tia và không bypass quyền sương mù. Chọn/khai thác tài nguyên và box-select phải tuân theo quyền lối chơi. Pixel oracle kiểm tra che→hiện→che, không thay thế đầy đủ kiểm thử bằng thao tác thật lối chơi.

## Test

Core dùng grid/revision thật để kiểm tra vòng đời, delayed sinh đối tượng, dirty movement/separation, áp lại không đổi kết quả, đứng yên filtering và XZ chiến đấu. Graphics kiểm tra độ sâu hình học, làm mờ/restore, chọn bằng tia và Clear. Main kiểm tra nhanh kiểm tra40 tác nhân/1960static gốc qua90 khung hình; tải2.000 lính chưa nghiệm thu.

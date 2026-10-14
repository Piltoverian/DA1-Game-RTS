# Dọn luồng xử lý địa hình dựng hình

Ngày2026-10-13, theo yêu cầu tăng núi ở góc nhìn thực tế và loại bỏ thuật toán dựng hình cũ. [Hướng dẫn hiện hành](14_SuDung_Atlas_Implementation.md) là điểm gọi.

## Đã loại khỏi mã

- TerrainTileDraw và xử lý logic phân loại vách straight/outer/inner, dốc nối tầng single/left/middle/right. Compiler chỉ giữ rampFaceDirections cho exact height/camera.
- bộ mẫu cũ URP/Unlit phương án dự phòng và toggle chọn bộ dựng hình; chỉ còn vật liệu atlas chương trình tô bóng ghi độ sâu hình học.
- Ground ảnh sprite variation/hash/pattern32tile; mã nguồn atlas hòa trộn theo thế giới ECS coordinate.
- tấm hướng camera núi, rìa trang trí thuộc bộ dựng hình cũ và tùy chọn chỉ dựa vào khoảng cách núi mặt đỉnh. biên khoảng cách vẫn là bước nội bộ để tính sống núi mới.
- TerrainVisualOffsetSystem/TerrainVisualRestoreSystem và kiểm thử dựng hình-ma trận phục vụ riêng chúng.
- TerrainMaterialAuthoring, CanonicalIsometricSpriteAuthoring, StockIsometricSpriteValidation và các trình đơn cũ.
- Offline artwork/góc-UV trường trong SO; adapter LogicalRay đạt-xuyên; độ cao bộ nhớ đệm phương án dự phòng dùng TerrainVisualSurface.Active.

## Giữ lại

- TerrainChunkRenderer, CanonicalIsometricSprites và các top/wall/cap mặt nạ: đây là phần tạo ảnh sprite quads của luồng xử lý hiện hành, không phải bộ dựng hình legacy.
- TerrainSurfaceData/grid heights, TerrainRampCompiler, sinh dữ liệu, điều hướng và chi phí. dọn dẹp dựng hình không được thay lối chơi kết nối lưới.
- TerrainVisualSurface.Active cho chọn bằng tia phía máy khách, không phục vụ bộ nhớ đệm mô phỏng.
- ArtSource nguyên liệu/prompt và Artifacts bằng chứng lịch sử. Các tài liệu lịch sử được đánh dấu, không hướng dẫn chạy trình đơn đã xóa.

## Asset và migration

Main giữ cùng SO/GUID. Import42cặp color/geometry và serialize lược đồ dữ liệu mới. Các SO stock/reference và ảnh sprite library cũ chỉ xóa sau kiểm tra không còn incoming tham chiếu scene/prefab/asset. Danh sách file/reference nằm trong `Artifacts/TerrainUpgrade/retired-assets.json`; dọn dẹp biên nhận kết quả ghi1.250file đã xóa sau kiểm tra incoming=0.

hướng dẫn ghi đè giữ actual theo cặp hình học path; cờ theo mặt hiển thị màu chỉnh tay. Incomplete pair tạo lỗi có thể sửa trong SO/importer, không khôi phục một nhánh dựng hình cũ.

## Núi cao hơn

mặc định hình ảnh scale4, tối đa6 tầng, cap128 ×224. So scale2 trước dọn dẹp, độ cao núi tăng gấp đôi ở cùng camera30°/45°. biên/cạnh chung/hạt giống vẫn ổn định và làm mờ thao tác còn hoạt động. Các ảnh thu phóng chơi và lính tham chiếu là bằng chứng hình ảnh; không dùng chiều cao hình ảnh mỹ thuật để điều khiển chuyển động.

## Bằng chứng

Kết quả nằm ở Artifacts/TerrainUpgrade và nhật ký ``terrain-cleanup-*.log``. RunFullGraphics sau bỏ vẽ-danh sách ĐẠT/mã thoát0: lưới/dốc nối tầng điểm giữa, bộ nhập quyền sở hữu và chỉ hình học ghi đè, hoàn tác, độ cao/chuyển động/luồng xử lý, dọn dẹp hợp đồng, các khoảng, chương trình tô bóng/mờ/chỉnh tay màu, chọn bằng tia/xem trước và capture512. Main kiểm tra nhanh cuối sau đổi tên trình biên dịch ĐẠT/mã thoát0:40 tác nhân/1960static roots90 khung hình, trường hướng lấy mẫu/obstacle né vật cản và ảnh camera Play.

Pixel oracle fade0→195→0; hướng dẫn mặt đất đỏ/cap xanh28275/54669pixel. Range64 ×32 đạt max6/high spine37 ô. Capture512 build575.31ms là một mẫu chẩn đoán trên i9-13900HX/RTX4060Laptop, không p95 hoặc FPS. rà soát độc lập không còn chắc chắn P1/P2 vật chặn. Mục tiêu2.000 lính/30FPS vẫn cần khối lượng công việc thực; thay đổi giữ chưa commit.

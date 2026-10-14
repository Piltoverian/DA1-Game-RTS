# Sử dụng địa hình atlas trong Unity

Trạng thái ngày 15/10/2026: dự án dùng một luồng xử lý vật liệu atlas + độ sâu hình học. Main hiện gán `Assets/_Project/Data/Terrain/Generated/MoonTerrain/LunarTheme.asset`; tool tạo ví dụ sinh `JadeSlateAtlas/Theme.asset`, giữ GUID khi nhập lại. Các renderer/authoring/asset bộ mẫu cũ cũ đã nghỉ dùng; xem [dọn dẹp](16_Render_Cleanup.md).

## Designer: từ prompt đến chạy

1. Gen atlas theo [hướng dẫn prompt](11_Manual_Prompt_ThietKe_Render.md): ảnh3:2, layout3 ×2 gồm sáu ô ảnh vuông16..1024px. Panel512px cho atlas1536 ×1024. Không nhãn, khe, bóng đổ hoặc hình dáng bao ngoài núi trong pixel.
2. Unity → `RTS → Terrain → Material Atlas`; chọn mã nguồn và bật Read/Write. Chọn folder riêng cho bộ giao diện.
3. Khi nhập lại, chọn SO hiện có, kể cả SO đã đổi tên; bấm `Import / cập nhật cùng SO`.
4. Bấm `Preview terrain atlas`; gán SO cho TerrainMapRenderer đã chọn rồi chạy Main. Scene không tự lưu.

| Hàng trên | | |
|---|---|---|
| mặt đất-lush | mặt đất-sparse | dốc nối tầng-earth |
| núi-đá phiến | vách-đá phiến | mặt đất-chi tiết |

Importer tạo42 color PNG +42 hình học PNG:16top,16cap,10wall. Hòa trộn mặt đất lấy mã nguồn atlas theo tọa độ thế giới ECS; không cần32 ảnh sprite nền cũ. Source nguyên bản giữ nguyên; đầu ra derived blend6% ở mép đối diện để giảm seam. hình học ảnh vật liệu tuyến tính, dễ đọc, không mipmap/compression, NPOT không resize. Top/wall128 ×128; mặt đỉnh mặc định128 ×224.

Tạo mẫu từ nguyên liệu JadeSlate đã có: `RTS → Terrain → Create JadeSlate Atlas Example`. Đây là repack vật liệu trong ArtSource, không phải lượt gen AI mới.

## Núi và góc camera chơi

Camera chiếu song song pitch30°, yaw45°, zoom/pan, không xoay. Sống núi bám lõi vùng chiếm chỗ, có đỉnh/yên theo hạt giống. Mặc định tối đa6 tầng; `mountainHeightScale=4` tăng riêng tầng núi so với tầng nền. So với bản scale2, cùng vùng chiếm chỗ có núi cao gấp đôi. Vùng hẹp giới hạn số tầng theo khoảng cách tới biên; vùng lớn tạo dãy nối liền.

Thay mountainHeightScale1..4 rồi nhập lại cùng SO để cập nhật cả16cap. Tất cả mặt đỉnh phải cùng chiều cao canvas. Cao độ lối chơi, vùng chặn, chuyển động XZ và bản đồ mã băm không đổi vì điều chỉnh này. Unit/collider Y tiếp tục lấy đã lấy mẫu địa hình độ cao + điểm gốc độ lệch, không lấy đỉnh núi trang trí.

Fade dùng dithering có chuyển tiếp khoảng0,1s và nhả độ sâu ở pixel bị bỏ. Con trỏ và giới hạn lính đã chọn tạo vùng thao tác; đã chọn gốc gom8vùng, đọc10Hz, tối đa16anchor. Radius/độ đục chỉnh trong SO. Ground chọn bằng tia dùng mặt xử lý logic; sprites núi không có khối va chạm để nuốt click. Fade không cấp quyền nhìn qua sương mù.

## Import lại và chỉnh tay

SO giữ hạt giống, tỷ lệ, tầng, làm mờ thiết lập và GUID. Receipt GUID/hash xác định tài nguyên do bộ nhập sở hữu; đầu ra có sẵn mà không thuộc bộ nhập bị từ chối trước ghi. Importer tạo làm việc sao chép, xuất biên nhận kết quả rồi mới cập nhật SO; lỗi tài nguyên-write khôi phục PNG/meta và SO.

vị trí đổi tham chiếu hoặc chỉnh pixel được bảo vệ cùng **đường dẫn hình học thực tế** trong spriteDepths. Nếu một mặt đỉnh chỉnh tay, giữ nguyên cả bộ16cap để tránh trộn chiều cao canvas. Nhóm đã trộn chiều cao bị từ chối. `conflicts.txt` liệt kê những vị trí được giữ.

Color và hình học chỉnh tay phải dễ đọc và cùng kích thước. Có đủ cặp tương thích thì bộ dựng hình dùng pixel màu của người thiết kế, không ghi đè bằng thế giới ECS-space vật liệu hòa trộn. Thiếu hình học hoặc nhóm sai kích thước thì bộ dựng hình báo rõ và không dựng địa hình; không còn bộ mẫu cũ phương án dự phòng. `manualColorOverrides` do bộ nhập quản lý. Không dùng generated vật liệu-mã nguồn làm đầu vào; dùng atlas nguyên bản.

## Coder: đường dữ liệu

`GridComponent/GridTerrain + MapBuildState.Revision → TerrainSurfaceData → TerrainHeightCacheSystem → PendingHeight → LocalTransform.Y`.

`TerrainTheme → TerrainChunkRenderer → CanonicalIsometricSprites → RTS/Terrain Material Atlas shader`.

địa hình ảnh sprite quads vẫn nằm Y0. UV1 chứa cơ sở độ cao, tầng rise, loại mặt và cờ màu chỉnh tay; chương trình tô bóng tái dựng mặt ảo để ghi độ sâu. MountainVisualField tạo một trường shared đỉnh; cạnh liền kề chênh≤1 tầng. TerrainVisualSurface chỉ phục vụ chọn bằng tia phía máy khách; bộ nhớ đệm lối chơi không tham chiếu Active bề mặt.

## Kiểm tra sau import

1. Bấm `Preview terrain atlas` trong cửa sổ Material Atlas để xem ground, ramp, cliff và mountain. Preview tạo ảnh trong bộ nhớ, không dùng helper test hoặc artifact.
2. Gán SO cho TerrainMapRenderer, mở Main và kiểm tra seam, độ cao, picking và fade trong Play Mode. Scene không tự lưu.
3. Khi đổi atlas, nhập lại cùng SO và kiểm tra các override được giữ. Không xem preview là nghiệm thu gameplay hoặc hiệu năng.

Các lệnh TerrainUpgradeValidation, TerrainHeightMainSmoke và RunRangeCapture đã bị loại; không còn runner để chạy lại. Bằng chứng cũ nằm trong [lịch sử hợp nhất](../Maintenance/VerificationHistory.md), không chứng minh source hiện tại đã được kiểm thử.

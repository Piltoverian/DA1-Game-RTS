# Phép chiếu, mặt chuẩn và độ sâu hình học

Hiện hành2026-10-13. Camera chiếu song song pitch30°, yaw45°, roll0. Đây là dimetric2:1; lưới lối chơi dùng XZ.

## Tier và canvas

Cho ô size c, tầng nền `rise = c × (32/128) × sqrt(2) / cos(30°)`. Top/wall có canvas128 ×128, điểm neo chuẩn tại pixel(64,48), width hiển thị `c×sqrt(2)`.

mặt đỉnh núi mặc định dùng128pixel/tier, canvas128 ×224. Chiều cao canvas `max(128,96+tierPixels)` và điểm gốc `(0.5,48/canvasHeight)`. Từ canvas suy ra `capRise=rise×max(1,(canvasHeight-96)/32)`; scale4 cho capRise=4 ×rise. Toàn bộ16cap phải cùng canvas độ cao, color và hình học cùng kích thước.

Importer raster hai triangle00-10-11 và00-11-01. hình học.R là độ cao góc nội suy chuẩn hóa0..1; ảnh vật liệu hình học tuyến tính. góc mặt nạ và wall endpoint bits dùng cùng quy ước trong baker/builder.

## Giữ quads ở Y0

Với camera hướng trước F có F.y=-0.5 và điểm neo A ở cao độ h, projected điểm neo là `A - F×(h/F.y)`, rồi Y=0. Screen-right và projected screen-up tạo rectangle đúng aspect của PNG. Runtime không warp artwork và không dựng khối va chạm ảnh sprite.

UV0 trỏ color/geometry atlas có packing giống nhau. UV1=(baseHeight,tierRise,faceKind,manualColor). Các mặt:wall0, ground1, ramp2, mountainCap3, mountainGround5, mountainWall6. ManualColor1 dùng pixel màu đã chỉnh tay;0 dùng thế giới ECS-vật liệu hòa trộn cho ground/cap.

## Độ sâu và thứ tự

Fragment đọc hình học.R, tính altitude=baseHeight+R×tierRise. Với plane pixel P ở Y0: `world = P + F×(altitude/F.y)`. Shader chiếu thế giới ECS bằng camera để xuất SV_Depth, ZWrite On và ZTest LEqual. Không dùng độ sâu phẳng của quads để che lính.

Builder giữ global thứ tự vẽ thứ tự xuyên lô và sortingOrder tăng theo lô, mỗi lô tối đa2048sprite. hình học độ sâu vẫn giải quyết mặt ảo/unit trong luồng xử lý duy nhất. Atlas packing không được tỷ lệ PNG; phát hiện resize sẽ dừng bản dựng.

Camera không được rotate sau bản dựng; bộ dựng hình giữ lại góc, vẫn cho pan/zoom. Muốn hỗ trợ góc khác cần contract/baker khác, không điều chỉnh bằng tỷ lệ tùy ý. Các capture zoom8/18/40 và Main playing-camera thuộc kiểm chứng lịch sử; artifact đó không còn trên đĩa.

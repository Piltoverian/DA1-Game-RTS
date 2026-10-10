# 02 — Phép chiếu, camera và painter order

## 1. Ba không gian phải phân biệt

| Không gian | Cao độ có ý nghĩa gì? |
|---|---|
| Grid logic | heightLevel/corner metadata; navigation vẫn trên mặt phẳng |
| Mặt ảo | Điểm `(x,h,z)` dùng để chọn silhouette, sort và picking |
| Mesh terrain thực | Quad với mọi vertex Y=0, UV mang PNG đã vẽ hình cao/thấp |

Nói “terrain Y=0” mô tả mesh và grid vật lý. Nó không có nghĩa dữ liệu `heightLevel`, surface ảo hoặc offset hiển thị đã bị xóa.

## 2. Metrics canonical

Camera orthographic có pitch 30°, yaw 45°, roll 0°. Texture canonical 128×128; footprint diamond 128×64; mỗi tầng dịch 32 pixel. Các constants nằm trong `TerrainTheme` và được `ResolveTierRise` kiểm tra cùng camera forward.

Ký hiệu `s=cellsize`, `f=camera.forward`, `R=rise`:

```text
spriteWidth = s * sqrt(2)
R = s * (32 / 128) * sqrt(2) / cos(30°)
right = normalize(cross(worldUp, f))
screenUpOnPlane = normalize((f.x, 0, f.z)) / abs(f.y)
```

Đây là metric renderer của dự án, không phải một “chuẩn isometric” áp dụng cho mọi engine. Đổi yaw/pitch hoặc tier pixels đòi hỏi tạo lại silhouette và cập nhật hợp đồng.

## 3. Chiếu anchor ảo xuống mặt phẳng

Với anchor `a=(x,h,z)`, renderer tính:

```text
aFlat = a - f * (a.y / f.y)
aFlat.y = 0
```

Hai điểm khác nhau đúng một vector song song hướng nhìn `f`, nên có cùng vị trí ảnh dưới phép chiếu orthographic. Đây là suy luận hình học của dự án. Mục tiêu là bảo toàn hình chiếu, không bảo toàn độ sâu 3D.

Quad sau đó được dựng trên mặt phẳng:

```text
width  = s * sqrt(2)
height = width * texture.height / texture.width
p(u,v) = aFlat
       + right * ((u - pivot.x) * width)
       + screenUpOnPlane * ((v - pivot.y) * height)
p.y = 0
```

Top, wall và mountain cap dùng pivot `(0.5,0.375)` theo gốc dưới trái. Fallback artwork núi riêng có pivot theme. Runtime đọc Texture2D, không lấy pivot từ Sprite Editor và không warp silhouette bằng mesh dốc.

## 4. Global painter order

Mỗi face nhận key `dot(sortPosition, f)`. Top sử dụng chiều cao trung bình bốn góc; wall dùng tâm cạnh và trung bình endpoint; cap/decor mặc định dùng anchor. Sort giảm dần depth, rồi tăng dần sequence để phá hòa ổn định theo thứ tự thêm face.

Sau sort toàn map mới chia các đoạn 2.048 face. Mỗi batch tạo 4 vertex và 2 triangle cho một face, dùng UInt32 index. `sortingOrder` tăng theo batch. “Chunk” trong tên hiện tại là batch nối tiếp trong danh sách painter, không phải spatial chunk hay hệ streaming.

Chi phí suy luận: tạo face O(F), sort O(F log F), emit O(F). Mảng/list/mesh toàn map tồn tại trong lúc build; batch không làm tổng bộ nhớ build trở thành hằng số. Frame steady-state không sort lại terrain mỗi frame.

## 5. Material và hệ quả

Stock `Universal Render Pipeline/Unlit`; alpha clip 0.1; Clamp; queue 2900; Cull off; ZWrite=0. ShadowCaster/DepthOnly/DepthNormalsOnly bị tắt. Đây là cấu hình quan sát từ mã, không phải khẳng định mọi draw call của URP đã được đếm.

Painter order xử lý nhiều quan hệ che phủ giữa sprite terrain nhưng không bảo đảm đúng mọi giao cắt. Hai face có footprint chồng phức tạp có thể không có một thứ tự scalar đúng. Đặc biệt, unit 3D và terrain có cơ chế depth khác nhau: không được suy từ quad Y=0 rằng unit sau cliff đã được che khuất đúng.

## 6. Thí nghiệm cần giữ

| Fixture | Tín hiệu sai | Tiêu chí |
|---|---|---|
| Ramp chung cạnh, nhiều mask | Nứt, lệch một tier hoặc pivot | Không hở ở zoom 8/18/40 |
| Wall nhiều tầng | Mặt bị thiếu/đảo order | Segment không chồng sai ở điểm nối |
| Face trước/sau ranh batch 2.048 | Seam theo batch | Ảnh không đổi khi đổi batch size trong lab |
| Unit đi sau cliff/núi | Unit xuyên hoặc bị che sai | Đo occlusion riêng; hiện chưa nghiệm thu |
| Đổi orthographic size | Silhouette nhòe/bleed | So sánh Point/Bilinear và padding cùng camera |

Camera hiện cho pan và orthographic zoom; xoay tự do, perspective hoặc roll không thuộc hợp đồng. Xem [hướng nghiên cứu](08_GioiHan_HuongNghienCuu.md) trước khi mở rộng.

# Hòa trộn mặt đất và dãy núi

Hiện hành2026-10-13; chỉ còn vật liệu atlas bộ dựng hình. [mã trường](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/MountainVisualField.cs), [chương trình tô bóng](../../Assets/_Project/Art/Design/Terrain/TerrainMaterialAtlas.shader).

## Ground và dốc nối tầng

Ground dùng top mask0; chương trình tô bóng lấy lưới coordinate từ virtual thế giới ECS vị trí. nhiễu liên tục ở tỷ lệ groundRegionCells tạo weight lush/sparse; chi tiết ô ảnh trộn6% ở scale7.37 ô. Hai ô ảnh nền mẫu tại grid/4, không chọn một trong32sprite theo mã băm nữa. hướng dẫn song tuyến tính wrap từng ô ảnh ngăn bleed sang vị trí kế bên.

Ramp giữ top mặt nạ theo góc cao và vật liệu đất nung, wall đá phiến tối. Shared đỉnh và mặt đất độ cao lấy dữ liệu lối chơi; không tự biến mọi đường đồng mức thành dốc nối tầng. Source ô ảnh không có hình dáng bao ngoài hoặc bóng đổ. vật liệu noise/seed không tiêu thụ RNG generation/resource.

## MountainVisualField

1. đỉnh là interior nếu cả bốn ô chung đỉnh đều thuộc vùng chiếm chỗ núi; đỉnh biên/ngoài có distance0.
2. BFS tám hướng cho khoảng cách tới biên. Đây là dữ liệu trung gian tìm lõi, không phải một renderer/tùy chọn legacy.
3. cục bộ maxima theo trục X hoặc Z với quy tắc phá hòa nghiêm tạo medial spine, thay cho đỉnh cone rải ngẫu nhiên. Hash coarse coordinate x/12,z/12 và hình ảnh hạt giống quyết định crest cao tối đa, có chênh đỉnh/yên0..2 tầng.
4. Peak bị chặn bởi biên khoảng cách và mountainVisualTiers1..6. MaxTiers1 vẫn cho mọi đỉnh≤1.
5. Lan từ toàn sống núi tới tám hàng xóm, giá trị không vượt khoảng cách hoặc độ cao nguồn-1. Kết quả liên tục, shared adjacent đỉnh chênh≤1, boundary0 và không nở vùng chiếm chỗ.

Mỗi ô lấy tối thiểu của bốn mức, mặt nạ đánh dấu góc cao hơn một tầng, rồi vẽ mặt đỉnh tại cơ sở địa hình độ cao + tối thiểu×capRise. mặc định scale4/tối đa6 tầng cho núi cao gấp đôi bản scale2 tại cùng góc camera; vùng hẹp vẫn bị khoảng cách giới hạn. tối đa độ cao phụ thuộc vùng chiếm chỗ, không phải cứ tăng setting là mọi đỉnh đều đạt6 tầng.

## Vật liệu và ghi đè

mặt đỉnh sinh tự động lấy đá phiến theo thế giới ECS coordinate và ánh sáng từ virtual bề mặt normal; không đảo ảnh/tint ngẫu nhiên từng tile. Designer thay cả mã nguồn ô ảnh để đổi phong cách dãy.

vị trí màu/geometry chỉnh tay được bộ nhập đánh dấu manualColorOverrides. Shader dùng BaseMap.RGB cho vị trí đó; giữ hình dáng bao ngoài và độ sâu hình học. Nếu mặt đỉnh được chỉnh, giữ cả bộ16cap cùng canvas độ cao khi nhập lại. Thiếu hình học hoặc mixed canvas gây lỗi rõ, không phương án dự phòng tấm quay về camera/stock.

## Bằng chứng

Fixture64 ×32 elongated vùng chiếm chỗ: max6, một đoạn cao spine37 ô, hạt giống ổn định, boundary0 và tám hướng chênh≤1. Render thực kiểm tra làm mờ/restore và hướng dẫn-color pixels. Đây không phải bằng chứng30FPS/2.000 lính.

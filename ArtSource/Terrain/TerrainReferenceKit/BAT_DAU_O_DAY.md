# Bộ mẫu gen terrain tu tiên

1. Mở `00_Tong_quan.png` để xem toàn bộ bộ mẫu, hoặc mở `index.html` bằng trình duyệt.
2. Sample/mask trong 01–05 là hợp đồng hình học; không yêu cầu AI tự bố trí 78 hình.
3. Gen vật liệu phẳng theo ArtSource/Terrain/JadeSlate/prompt.md; công cụ Python sinh tile đã loại bỏ. Giữ PNG hiện hành và dùng Unity importer để kiểm tra.
4. `*-guide.png` có nhãn, lưới và pivot: chỉ xem tham khảo, không gửi làm ảnh đầu vào.
5. `06_Phong_cach` chứa concept tham khảo màu sắc. Concept và ảnh tổng quan không phải atlas chuẩn để import vào game.

Mọi sheet có bốn cột, ô 128×128, thứ tự từ trái sang phải rồi từ trên xuống dưới. Giữ đúng canvas và alpha của mask. Góc camera chuẩn 30°/45°, ánh sáng từ trên trái; manifest.json ghi vị trí từng ô.

Các ảnh sample/mask/guide được sao chép nguyên vẹn từ bộ canonical hiện tại; SHA256.json ghi hash để đối chiếu.

Sau gen bằng công cụ bên ngoài: kiểm tra đúng kích thước, mask, nối cạnh và tính dễ đọc. Giữ PNG thử trong theme riêng; cần qua lab trước khi đưa vào theme chính. Không còn script cắt sheet trong repository.

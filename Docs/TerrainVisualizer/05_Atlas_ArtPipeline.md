# Atlas và hình ảnh mỹ thuật luồng xử lý

Hiện hành2026-10-13. [hướng dẫn prompt](11_Manual_Prompt_ThietKe_Render.md), [Unity quy trình](14_SuDung_Atlas_Implementation.md).

## Nguồn và phong cách

JadeSlate phong cách vẽ tay: nền xanh xám/xanh olive dịu, dốc nối tầng đất nung ấm, núi đá phiến xanh xám. Gen **vật liệu phẳng**, không gen từng núi, diamond hoặc bóng đổ. Unity tạo hình khối từ footprint/góc masks.

Layout3 ×2: trên cỏ dày/cỏ thưa/đất dốc, dưới núi/vách/chi tiết. Sáu ô ảnh vuông bằng nhau16..1024pixel; source1536 ×1024 cho panel512. Không viền/khe/text trong ảnh; Editor hiển thị nhãn overlay. Source ảnh gốc giữ trong ArtSource hoặc một tài nguyên đầu vào riêng, không dùng generated vật liệu-mã nguồn làm đầu vào.

## Importer

TerrainAtlasAuthoring crop theo top-left ô ảnh thứ tự. hòa trộn đối diện mép6% ở đầu ra derived, không sửa mã nguồn. TerrainCanonicalBaker raster exact triangles, sinh42color và42geometry:16top+16cap+10wall. Top/wall128 ×128; mặt đỉnh default128 ×224, tầng núi128pixel. hình học tuyến tính, NPOT giữ đúng size, dễ đọc, song tuyến tính, clamp, uncompressed, không mipmap.

Runtime chỉ đóng gói mặt vào atlas đồng bộ color/geometry; ground/cap vật liệu sinh tự động lấy mã nguồn qua thế giới ECS UV. Không còn32ground variant PNG hoặc tấm quay về camera/decor library cũ.

## SO và chỉnh tay

Chọn cùng TerrainTheme để nhập lại; GUID/reference/name/thiết lập giữ, kể cả SO đã đổi tên. Receipt ghi GUID/hash/path hợp đồng; tệp không thuộc bộ nhập được từ chối trước ghi. Lỗi ghi khôi phục PNG/meta/SO. Undo không thay thế hoàn tác toàn bộ ảnh vật liệu bytes.

Override color **hoặc hình học pairing/pixels** được bảo vệ cùng actual độ sâu tài nguyên path. mặt đỉnh ghi đè giữ toàn bank16 cùng chiều cao. hướng dẫn vị trí dùng màu riêng qua UV1 flag; complete pair bắt buộc trước dựng hình. Không có bộ dựng hình phương án dự phòng để che lỗi tài nguyên.

## Entry point

- `RTS → Terrain → Material Atlas`: nhập cùng SO, xem trước, gán GameObject đã chọn.
- `RTS → Terrain → Create JadeSlate Atlas Example`: repack nguyên liệu JadeSlate hiện có.
- `RTS → Tests → Validate Current Terrain Atlas`: xem trước current bộ giao diện.

Các trình đơn Build chuẩn Library/Import Jade Slate legacy và SO/reference ảnh sprite folders đã xóa sau kiểm tra tham chiếu. ArtSource và báo cáo lịch sử giữ để tra cứu; không đưa chúng vào khi chạy bằng trình đơn cũ.

rà soát mã nguồn repeat, seam ground/ramp, hình dáng bao ngoài núi và lính độ dễ đọc ở zoom8/18/40 và camera Main Play. Source đẹp không thay thế dựng hình đẹp; GPU/load nghiệm thu cần khối lượng công việc thực.

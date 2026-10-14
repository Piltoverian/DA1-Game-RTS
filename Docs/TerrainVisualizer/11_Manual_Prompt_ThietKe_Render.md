# Mô tả tạo ảnh địa hình

Hiện hành2026-10-13. Quy trình đã chốt: định phong cách → gen một atlas vật liệu phẳng3 ×2 → Unity cắt/sinh mặt → cập nhật cùng SO → preview/Main Play. [Hướng dẫn Unity](14_SuDung_Atlas_Implementation.md), [thuật toán](03_ThuatToan_Tile_Nui.md), [dọn dẹp](16_Render_Cleanup.md).

## Mô tả yêu cầu trước khi gen

JadeSlate phong cách vẽ tay, phù hợp RTS tu tiên/võ hiệp: nền xanh xám/xanh olive dịu và ít nhiễu, dốc nối tầng đất nung ấm, núi đá phiến xanh xám. Ưu tiên lính/tài nguyên dễ đọc ở thu phóng chơi. Núi có hình khối cao và dãy nối liền do Unity tạo; không vẽ sẵn hình dáng bao ngoài hoặc bóng núi lên vật liệu.

Sáu ô ảnh vuông cùng kích thước,3cột×2hàng: trên cỏ dày/cỏ thưa/đất dốc; dưới núi/vách/chi tiết. vị trí cuối là chi tiết nền, không trang trí hoặc tài nguyên. Source1536 ×1024 cho mỗi panel512px. Không chữ, lưới, khe, viền hoặc khoảng trống. Không kéo giãn ảnh sai tỷ lệ; kiểm tra kích thước/vùng cắt trước nhập.

## Prompt dùng ngay

```text
Tạo một atlas vật liệu màu bề mặt địa hình nguyên bản cho RTS tu tiên,
phong cách JadeSlate vẽ tay tiết chế: cỏ xanh xám/olive,
đất dốc nung dịu,đá phiến núi xanh xám lạnh,vách đá phiến tối hơn.
Đúng sáu ô vuông bằng nhau,xếp ba cột và hai hàng,
không khe,nhãn,khung,viền. Mỗi ô là vật liệu phẳng nhìn vuông góc,
lấp kín ô;không phải cảnh hoặc tile đẳng trục.
Hàng trên từ trái:cỏ dày;cỏ thưa/đất dịu khớp sắc độ và độ sáng
trung bình ô cỏ dày;đất dốc ấm tiết chế.
Hàng dưới từ trái:đá phiến phẳng lạnh cho mặt núi;đá phiến tối
phân lớp cho vách;chi tiết nền cỏ nhỏ tinh tế.
Mảng lớn yên,ít nhiễu,bảng màu thống nhất,dễ đọc dưới lính nhỏ.
Mỗi ô tự lặp liền với mép đối diện khớp.
Không cây,nhà,nhân vật,ký hiệu phép thuật,núi/vách nổi,đường bao,
bóng theo hướng hoặc tối viền. Không sao chép tài nguyên game.
Ảnh cuối1536×1024,mỗi ô512×512.
```

Gen variant bằng thay vật liệu description/palette trong mô tả yêu cầu, giữ layout và lighting hợp đồng. Cùng mã nguồn phải cho ground/ramp/cliff kết nối hợp lý; đừng tăng nhiễu để che seam. Repeat3 ×3 từng ô ảnh và kiểm tra average hue/brightness ở ranh lush/sparse trước dùng.

## Đưa vào Unity

`RTS → Terrain → Material Atlas` → chọn atlas nguyên bản, đầu ra folder riêng và SO nếu nhập lại → nhập → xem trước → gán TerrainMapRenderer đã chọn → Main Play. Chỉ còn luồng xử lý vật liệu atlas/geometric độ sâu; không có trình đơn nhập library bộ mẫu cũ cũ.

Importer tạo42cặp color/geometry:16top+16cap+10wall. Top/wall128 ×128; mặt đỉnh mặc định128 ×224. thế giới ECS-space mặt đất hòa trộn dùng atlas mã nguồn trực tiếp, không32sprite variant. Nguồn gốc giữ nguyên; seam blend6% chỉ áp dụng đầu ra derived.

## Điều chỉnh núi và độ dễ đọc

- `mountainVisualTiers`:1..6; vùng rộng tạo sống dài, đỉnh/yên theo hình ảnh hạt giống. Vùng hẹp tự giới hạn theo biên khoảng cách.
- `mountainHeightScale`:1..4, mặc định4; tăng riêng chiều cao mặt đỉnh, không đổi độ cao điều hướng/vùng chiếm chỗ. Thay tỷ lệ rồi nhập lại cùng SO.
- Camera chiếu song song pitch30/yaw45, zoom8/18/40 để duyệt. Xem núi cạnh lính/tài nguyên ở camera Main Play; ảnh mã nguồn phóng lớn chưa đủ.
- `mountainFadeOpacity`/radius: làm mờ vùng che con trỏ hoặc lính đã chọn, khôi phục che khuất khi rời vùng. Không lính đường bao/x-tia hoặc bypass sương mù.

## Designer chỉnh tay

Giữ cùng SO/GUID/name/thiết lập khi nhập. Color hoặc hình học ghi đè được bảo vệ trước ghi, kể cả hình học ở một đầu ra khác. mặt đỉnh ghi đè giữ cả16cap cùng canvas độ cao; mixed độ cao bị từ chối. Color/geometry cần cùng kích thước, dễ đọc và mặt nạ tương thích.

vị trí chỉnh tay dùng pixel màu riêng qua chương trình tô bóng flag; mã nguồn hòa trộn chỉ dùng cho vị trí sinh tự động. Thiếu pair thì báo lỗi, không đổi bộ dựng hình. Kiểm tra conflicts.txt và xem trước trước bật Play. Không dùng generated vật liệu-mã nguồn làm đầu vào.

## Checklist duyệt dựng hình

| Hiện tượng | Việc cần xem |
|---|---|
| Diamond/seam trên nền | Mép mã nguồn, lấy mẫu ô ảnh và thu phóng chơi |
| Ramp viền xanh hoặc khó đọc | Crop/mask và bảng màu dốc nối tầng; không sửa nhiễu chung |
| Núi thấp ở vùng rộng | Tier/scale, nhập lại và hình dáng bao ngoài camera30/45 |
| Dãy thành đá rời | Shared ridge trường, mã nguồn không có boulder/shadow |
| Unit bị che khó thao tác | Fade điểm neo/depth và chọn bằng tia; kiểm tra quyền sương mù |
| Màu chỉnh tay không hiện | manualColorOverrides và color/geometry pair |
| khởi động chậm | BuildProfile atlasPack/mesh/total, khối lượng công việc thực |

Mục tiêu30FPS/~2.000 lính trên cấu hình i5/1050 chưa nghiệm thu. Máy kiểm thử hiện tại i9/RTX4060; core/graphics/Main kiểm tra nhanh không đại diện tải2.000 lính. địa hình kết nối lưới cố định, động vật chặn vẫn thay đổi; server/network nghiệm thu theo lộ trình riêng.

# Áp dụng cách tổ chức thông tin AOE I

Ngày cập nhật: 05/11/2026. Giữ mỹ thuật tu tiên, uGUI và gameplay hiện có.

Các đợt đầu hoàn thiện cụm bảng lựa chọn/lệnh/tooltip. Theo yêu cầu mới ngày
05/11/2026, bố cục hiện chuyển sang khung chung bốn vùng, minimap bên phải.
Kế hoạch mới nhất: [CommandDockImplementation.md](CommandDockImplementation.md).

## Đã sửa

- Quân có một vũ khí hiện damage/range và cooldown trực tiếp trên hai dòng
  thông tin. Công trình hoàn tất dùng hai dòng này thay chữ Completed khi cần;
  công trình đang xây vẫn ưu tiên tiến độ. Chi tiết nhiều vũ khí ở tooltip.
- Kho không còn ghi đè thông tin dân số đóng góp bằng danh sách tài nguyên nhận.
- Tên đối tượng và tiêu đề tooltip dùng vàng ngà; nội dung chính ngà sáng,
  nội dung phụ xanh xám; Available/Completed xanh ngọc, lý do khóa hổ phách.
  Dấu !/OK và chữ lý do vẫn được giữ để không phụ thuộc hoàn toàn vào màu.
- Giữ texture sơn ngọc/viền đồng, kích thước card và vị trí các vùng HUD đã có.
  Không thêm armor hoặc tính năng gameplay chưa có dữ liệu.

## Nghiệm thu trong Main

1. Chọn quân có attack definition: thấy DMG/RNG/CD; chọn nhóm có dấu Rep.
2. Chọn công trình hoàn tất: thông tin chức năng không vượt hai dòng.
3. Chọn mỏ/worker/nhà đang xây: thông tin tương ứng vẫn được ưu tiên.
4. Hover nút khả dụng/khóa: tiêu đề và lý do có màu đúng, không hiện thẻ markup.
5. Kiểm tra đọc chữ ở 1080p/720p và tooltip sát mép; click không xuyên UI.

Compiler và kiểm tra tham chiếu không thay thế nghiệm thu Play Mode.
Các bước tiếp theo và giới hạn dữ liệu công trình xem UIReportReadiness.md.

## Đợt tiếp theo — minimap và queue

Chủ dự án đã nghiệm thu cụm bảng lựa chọn/lệnh/tooltip của đợt trước.
Đợt này giữ bố cục/mỹ thuật đó và bổ sung:

- Minimap có chữ Waiting for map / Waiting for player thay vì để người chơi
  đoán ô trống có lỗi hay không. Chỉ điều hướng khi có map và local player.
- Marker mỏ phân màu Gold/Wood/Food; mỏ đang xem có dấu chọn trắng. Khi đạt
  giới hạn 2048 marker, chú giải báo Marker limit reached.
- Queue phân biệt số công việc đang chờ với job đang làm; thời gian ghi rõ là
  của job hiện tại, không phải tổng thời gian cả hàng.
- Job chưa hoàn thành hiển thị tối đa 99%; work về 0 có trạng thái finalizing
  hoặc chờ dân số. Thanh tiến độ hổ phách khi chờ/paused/đang xây, ngọc khi chạy;
  dữ liệu tiến độ không hợp lệ thì ẩn fill thay vì báo 0% giả.
- Không thêm hủy queue, lệnh chuột phải trên minimap hoặc thay simulation.

Kiểm tra trong Main:

1. Vào Play thấy chữ chờ thích hợp, rồi chú giải bình thường khi map/player sẵn sàng.
2. Xem một mỏ: marker đúng mỏ nổi bật; đổi mỏ/mỏ cạn không để dấu chọn cũ.
3. Click/kéo minimap điều hướng camera, không đổi nhóm hoặc phát lệnh world.
4. Xếp nhiều job: thấy số đang chờ, thứ tự và thời gian job hiện tại; khi kết
   thúc một job, queue đổi đúng. Kiểm tra chờ dân số nếu gameplay tạo được trạng thái đó.
5. Kiểm tra màu/chữ ở 1080p/720p; đổi lựa chọn và vào/thoát Play không giữ nội dung cũ.

Mã đã được kiểm tra biên dịch; nghiệm thu Play Mode cho đợt này còn chờ xác nhận.

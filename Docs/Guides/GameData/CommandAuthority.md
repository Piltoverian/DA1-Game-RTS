# Quyền sở hữu: ai được điều khiển thực thể?

**Chỉ cần nhớ:** chỉ chủ sở hữu được điều khiển; cùng đội không tự được điều khiển lính của nhau.

## Một lệnh hợp lệ cần gì?

1. Issuer không âm.
2. Source thực thể tồn tại và không phải mẫu đối tượng.
3. Source có `EntityOwner`, PlayerID khớp người phát lệnh.
4. Source có khả năng phù hợp với lệnh.

Gate kiểm tra chạy trước các nhánh lệnh làm thay đổi lối chơi. `TargetTo` còn từ chối mục tiêu đã xóa thực thể.

## Ví dụ

Unit có chủ sở hữu 2. người chơi 1 gửi Move hoặc Train cho lính đó → bị từ chối. người chơi 2 gửi lệnh → qua quyền sở hữu điều kiện thông qua; vẫn cần đáp ứng khả năng và điều kiện lối chơi khác.

## Vì sao ảnh trạng thái lệnh hàng đợi?

đặt nhà có thể tạo thực thể, khiến `DynamicBuffer` đang duyệt mất hiệu lực. bên xử lý ảnh trạng thái và clear hàng đợi trước khi xử lý; lệnh thêm trong lúc xử lý dành cho update sau.

Production yêu cầu playerId rõ ràng; đường Train cũ chuyển qua hàng đợi có người phát lệnh. Cancel thiếu chủ sở hữu cũng bị từ chối.

## Bằng chứng và giới hạn

Nguồn ghi kiểm tra hồi quy quyền sở hữu ĐẠT và đối chứng bỏ so sánh chủ sở hữu thất bại đúng case. Đây là bằng chứng của lượt kiểm thử đã ghi.

quyền sở hữu điều kiện thông qua chưa phải xác thực kết nối mạng. Khi thêm lớp truyền dữ liệu, người phát lệnh phải lấy từ kết nối đã xác thực, không tin PlayerID do gói tự khai.

**tệp gốc:** [OwnershipFix_2026-10-13.md](../../Maintenance/VerificationHistory.md).

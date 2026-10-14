# Thanh toán tài nguyên: chuẩn hóa chi phí và trả tiền

**Chỉ cần nhớ:** Editor chuẩn hóa chi phí; khi chạy kiểm tra số dư và thanh toán nguyên tử.

## Ba nơi, ba trách nhiệm

| Nơi | Trách nhiệm |
|---|---|
| Registry Validate | Gộp chi phí trùng ResourceType, lưu SO; báo dữ liệu sai |
| bộ chuyển dữ liệu sang ECS | sao chép chi phí đã chuẩn hóa; từ chối chi phí sai/chưa chuẩn hóa |
| `ResourcePayment` | Kiểm tra số dư, trừ tiền và hoàn tiền trong trận |

Runtime thanh toán không gộp lại dữ liệu chi phí tĩnh của trò chơi.

## Ví dụ

Cost cấu hình có hai dòng wood: 20 và 30. Validate gộp thành wood 50 và lưu SO. Runtime dùng chi phí 50 để kiểm tra/thanh toán.

**Thanh toán nguyên tử** nghĩa là không trừ một phần rồi để lệnh thất bại vì thiếu tài nguyên khác.

## Khi thực hiện lối chơi

1. đặt nhà, sửa và sản xuất dùng thanh toán chung.
2. Nếu đặt nhà đã trừ tiền nhưng không publish được building/command, hoàn tác.
3. Khi bên sản xuất chết, dọn dẹp tác vụ trước khi destroy bên sản xuất để xử lý vòng đời đúng.

Refund cụ thể phụ thuộc hợp đồng lối chơi; tệp này không thay thế luật hoàn tiền ở các hệ thống hoặc sổ theo dõi mới hơn.

## Trạng thái đã ghi

Các kiểm tra hồi quy backend có biên nhận kết quả ĐẠT lịch sử. Construction partial/full completion được dữ liệu kiểm thử Task3 kiểm tra work25/100→refund75 và completed→0. **GUI Validate/Undo và SubScene chuyển dữ liệu sang ECS độc lập chưa được nghiệm thu.**

Xem [trạng thái hiện tại](../../Maintenance/CurrentState.md): thợ đã được chủ dự án xác nhận sửa; bộ kiểm thử lối chơi đã xóa.

**Chi tiết hiện hành:** [Trạng thái gameplay/payment](../../Maintenance/CurrentState.md).

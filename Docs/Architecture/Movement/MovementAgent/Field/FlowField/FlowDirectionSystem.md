# FlowDirection: hướng sau Integration

Cập nhật 2026-10-12. Chạy bước cố định sau Integration, chỉ xử lý CalculatingDirection.

Ô đi được có bestcost hữu hạn xét 8 hàng xóm. Chọn hướng tới ô có bestcost thấp hơn và nhỏ nhất trong hướng hợp lệ. Bỏ ngoài biên, blocked và chéo có một cạnh theo bốn hướng chính blocked. chuẩn hóa hướng; ô seed/blocked hoặc không có hàng xóm tốt hơn cho zero.

Pass xong chuyển Ready. UnitMovementMath đọc hướng ô chứa lính, không song tuyến tính.

IJobChunk chia theo khối; trường cùng khối chạy lần lượt. Direction vẫn quét toàn bộ đệm, tối đa 8 hàng xóm mỗi ô. ngăn chỉ thay phần Integration, không bảo đảm Direction hết đột biến thời gian.

bộ đệm sai kích thước hiện bị bỏ đạt hướng nhưng vẫn chuyển Ready; cần lưu ý khi chẩn đoán dữ liệu.

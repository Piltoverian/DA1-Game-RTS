# Trạng thái FlowField

Cập nhật 2026-10-12. FlowFieldStatus.Value dùng enum byte.

| Trạng thái | Ý nghĩa |
|---|---|
| Requested | Field mới yêu cầu tính |
| PendingRecalculation | Field cũ cần tính lại |
| CalculatingCost | Integration tính bằng ngăn |
| CalculatingDirection | Direction tạo vector từ chi phí |
| Ready | Pass tạo trường đã kết thúc |

Integration nhận Requested/PendingRecalculation, chuyển CalculatingDirection khi xong chi phí. Direction chuyển Ready sau đạt.

Trạng thái không tự tạo incremental processing: Integration hiện tính hết hàng đợi trong tác vụ. Ready không chứng minh mọi ô reachable hoặc mọi bán kính đi được.

Invalidation chọn trường Ready cũ theo lưới sinh dữ liệu. [Giới hạn dựng lại](../../Maintenance_Guide.md) giải thích vì sao 12 không phải trần tổng trường.

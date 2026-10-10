# 06 — Lab hiện hành và giới hạn bằng chứng

## Harness và protocol

Harness TerrainVisualLab và cơ chế request file đã được xóa trong đợt dọn repository. Chạy các validation terrain qua menu RTS/Terrain trong Unity; nhập và gán JadeSlate qua menu TerrainMaterialAuthoring. Các số liệu bên dưới là bằng chứng lịch sử, không phải kết quả của lần chạy hiện tại.

## Câu hỏi và bằng chứng

Geometry: alpha/pivot, Y0, aspect, painter order. Gameplay separation: compiler không đổi cells, picking logical/visible và offset/restore. Integration: Main Ready, resource ramp margin, hash/entity count. Art: seam/readability/pattern tại cùng camera. Performance: Editor samples, không suy ra standalone GPU.

[Render report](../../Artifacts/TerrainVisual/render.txt): fixture64²,20520Y0 vertices,3batches,1860wall/516ramp vertices,568mountain cells, offline sources absent, picking/ECS và Clear/rebuild PASS.

[So sánh trước/sau](../../Artifacts/TerrainVisual/Comparison/index.html) giữ camera giống nhau. Main512²,8players,2040resources,140batches,atlas2048×1024RGBA8MiB; footprint ngoài one-cell ramp margin. Report/frame samples đi kèm từng capture. Palette mới giảm patch đất/cỏ và painted mountain block lặp; pattern và độ mềm biên vẫn tồn tại. Đây là đánh giá từ ảnh đã xem, chưa thay nghiệm thu người dùng.

## Confounder và thí nghiệm tiếp theo

Giữ seed/config/camera/zoom, ghi theme/source hash/build method. Hash cells không bao gồm toàn bộ trận đấu; cần assertions LocalTransform/cost/entities/resources. Geometry PASS không xác nhận all-seed occlusion hoặc cảm nhận độ cao.

Frame samples chịu Editor/UI scheduling, import/Burst warmup và scene load. Muốn performance kết luận: standalone reproducible scenario, ABBA runs, warmup, đủ p95 và tách build stall/frame update. Chưa có long-session certification.

## Lịch sử đã lọc

Archive code/assets và lab/docs đã được loại bỏ ngày 2026-10-10. Không dùng report theme cũ cho trạng thái hiện hành; URL cũ chỉ trỏ tới tài liệu hiện hành.

NRE offset vẫn chưa xác định root cause; giữ [điều tra](../../Artifacts/TerrainVisual/History/offset-cause-investigation.md), [IL](../../Artifacts/TerrainVisual/History/offset-original-il.txt). Source probe/command đã xóa; không tuyên bố chứng minh nguyên nhân.

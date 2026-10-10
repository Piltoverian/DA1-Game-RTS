# 06 — Lab hiện hành và giới hạn bằng chứng

## Harness và protocol

[TerrainVisualLab](../../Assets/_Project/Tests/Editor/MapGeneration/TerrainVisualLab.cs) đọc Artifacts/TerrainVisual/request.txt sau compile/import. Chỉ hai command:

- regression: stock scale/named slots, offset/restore, mountain integration, resource planner, canonical reference và JadeSlate render.
- main-current: Main ở Edit mode và dùng JadeSlate; Play, đợi Ready,60warmup+180sample, capture spawn/flat/ramp/mountain/cliff zoom8/18/40 tại1280×720, Stop. Không save Main/camera Play.

Gate fixture hash77764F5FF38714982D0B5D6E71B39364FA95D032B01ADBBE4044C924623A60B2,2040resources. Nếu config MapGen đổi có chủ ý phải review/update fixture, không bỏ gate. Một request file duy nhất; không ghi đè command đang chạy. Failure.txt và timestamp dùng phân biệt lượt mới với PASS cũ.

Authoring vật liệu thuộc command file riêng, xem chương05. Đã bỏ harness/command của old trial, layout promotion, old baseline/candidate và offset-cause.

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

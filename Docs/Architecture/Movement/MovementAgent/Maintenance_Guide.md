# MovementAgent: bảo trì và hiệu năng

Cập nhật 2026-10-12. Chưa ghi nhận kết quả Unity/Burst sau thay ngăn.

## 12 không phải giới hạn tối đa

FlowFieldInvalidationSystem dùng MinRecalcPerFrame=12. Đầu đợt invalidation, RecalcPerframe=tối đa(12,ceil(số trường cũ / HEARTBEAT_INTERVAL)); interval hiện là 12. Mỗi bước mô phỏng đánh dấu tối thiểu(RecalcPerframe,số trường cũ) trường Ready thành PendingRecalculation, ưu tiên refCount cao.

Đây là mức tối thiểu khi đủ trường, không phải trần tổng Integration. Field mới Requested vẫn được xử lý riêng. Một trường chạy hết hàng đợi trong tác vụ; chưa chia theo ngân sách nút.

IJobChunk chia theo khối, trường cùng khối chạy lần lượt. Một khung hình dựng hình có thể có nhiều bước cố định bước mô phỏng.

## Ngăn và giới hạn

Cost cạnh = chi phí ô +10/14, tối đa 268 với chi phí ô 0..254. Có 269 ngăn vòng; ưu tiên chi phí thấp, bỏ queuedCost cũ và tái sử dụng vị trí. [Ví dụ](Field/IntegrationField/IntegrationFieldSystem.md).

Nếu đổi chi phí, kiểm tra lại tính không âm và giới hạn cạnh. ngăn không loại quét hạt giống toàn bản đồ, Direction hoặc chính-luồng chờ. Standalone có lợi ở trọng số cao nhưng FIFO tốt hơn trên bản đồ trống/cost 1.

MaxIslands=10000, nhưng vòng hạt giống vẫn khởi tạo/nạp dưới 1000: vùng liên thông >=1000 có thể thiếu hạt giống. Chưa có bằng chứng đây là nguyên nhân crash/spike.

Chưa có điều hướng khoảng trống class cho lính lớn. Endpoint điều kiện chặn không phải depenetration đầy đủ; tách hàng xóm không bảo đảm tuyệt đối hết chồng lấn. Far-stall vẫn có thể giữ lệnh mà chưa recovery.

## Thử nghiệm tiếp sau phản hồi khi chạy

Người dùng báo riêng Integration khoảng 25 ms trong các khung hình đột biến thời gian, có một số đột biến thời gian khoảng 50 ms trong chuyển động kiểm thử, không phải mọi khung hình đều đột biến thời gian. Chưa có marker/khối lượng công việc artifact của lượt này, nên đây là phản hồi khi chạy, không phải đo hiệu năng được kiểm soát.

mặt nạ 8 hướng hợp lệ đã thử độc lập trên 1000 trường 512 ×512 ×3 lần: mọi bestcost khớp FIFO, nhanh hơn ngăn thường khoảng 7–20% tùy bản đồ. mặt nạ cần 256 KiB/map; tạo 5 mặt nạ mất tổng 15,865 ms ở lượt được quản lý .NET này. Chi phí tạo mặt nạ đo riêng, phải chia sẻ qua nhiều trường mới có lợi. [kết quả đã lưu](../../../Maintenance/VerificationHistory.md).

Đã apply mặt nạ bằng [GridNeighborMaskSystem](Grid/GridNeighborMaskSystem.md) chạy trước Integration. Bản hiện tại dựng lại toàn mặt nạ một lần mỗi thay đổi kết nối lưới; không quét lại khi version giữ nguyên. Chưa có số Unity/Burst cho thay đổi này. Writer GridNodeCost mới phải tăng topologyVersion khi walkability đổi, không chỉ isDirty/generation.

Combat hiện gọi SetTarget mỗi update khi mục tiêu ngoài tầm; API luôn ghi request và đặt lại mục tiêu resolution. bộ nhớ đệm có thể tránh trường mới cho cùng targetCell, nên không kết luận mỗi request đều dựng lại. Cần phân biệt tải resolve/assignment với số trường mới khi mục tiêu di chuyển sang ô khác.

Ưu tiên tiếp: bộ nhớ đệm hướng hợp lệ theo phiên bản kết nối lưới thật; giảm quét hạt giống lặp; tránh request chase trùng mà vẫn cập nhật đích XZ; sau đó mới xét trường cục bộ/chia vùng. Field cục bộ phải có phương án dự phòng cho đường vòng ngoài vùng, không được cắt cứng theo khoảng cách Euclidean.

## Tham số và kiểm tra

Phản hồi tiếp theo sau mặt nạ: riêng Integration trong đột biến thời gian giảm khoảng 25 xuống 20 ms trên máy mạnh của người dùng; vẫn chưa có artifact Profiler. Đã thêm hạt giống trực tiếp cho một vùng liên thông có metadata kết nối lưới hiện hành và mục tiêu đi được, cùng marker Integration.Reset/Seeds/Propagate để tách chi phí. Nhánh phương án dự phòng giữ nguyên cho blocked/multi-island/stale kết nối lưới. [kết quả đã lưu](../../../Maintenance/VerificationHistory.md) khớp 1000 mục tiêu; chưa có số Unity/Burst cho nhánh hạt giống mới.

diameterCells là đường kính theo ô, bán kính khi chạy là bán kính thế giới ECS sau setup/snap. arrivalRadius hãm tốc; stoppingDistance kiểm tra đến đích. Không hủy lệnh xa chỉ vì stuckTime tăng.

Đo cùng khối lượng công việc: số trường thật sự dựng lại, Integration/Direction, thợ và JobHandle.Complete, số lệnh/quãng đường, timed retarget, stall, penetration/overlap, p95/p99/worst khung hình. FPS trung bình không đủ.

MovementSoakValidation chạy 6 phút; MovementSpikeProfile chụp giới hạn. Harness/Editor có thể tạo tải riêng. Không chạy thêm Editor vào dự án đang mở.

[Bàn giao](../../../Maintenance/VerificationHistory.md) lưu bằng chứng và giới hạn. Ghi kết quả Unity mới kèm thời điểm, khối lượng công việc và artifact trước khi đánh dấu đã xác nhận.

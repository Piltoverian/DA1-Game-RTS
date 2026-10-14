# Việc tiếp theo — 14/10/2026

**Bắt đầu bằng đề xuất pipeline nghiệm thu builder clamp trong Play Mode. Trang này khoảng 2 phút.**

[Trạng thái đã làm](CurrentState.md) · [Quy tắc lối chơi đã chốt](VerificationHistory.md)

## 1. Builder/combat correctness sau unit tests

Suite chốt 51/51 EditMode tests pass trước dọn; test/helper session đã xóa; xem [CurrentState](CurrentState.md) và [pipeline xây nhà](../Architecture/BuildingPipeline.md). Trước làm thêm, dùng [brainstorm-before-change](C:/Users/Legion/.codex/skills/brainstorm-before-change/SKILL.md), trình pipeline và chờ duyệt. Ưu tiên PlanBuild nhiều worker → Build → work tăng → completed trên bản clamp cuối, rồi queue/cancel/tiếp tục nhà dở.

Kiểm chứng AOE/team và các edge case Move/attack chase trong Play Mode. Đối chiếu nhóm authoritative, FixedStep và ECB; reproduce trước khi sửa. Không mặc định rà soát tĩnh là lỗi khi chạy hoặc unit PASS là Play Mode PASS.

## 2. Dogfood và tải thật

Chạy Main bằng đầu vào thật: select/box-select, Move, Gather, Build, ghost trên plateau/ramp/mountain, đặt nhà trùng, thợ giữ/bỏ hàng đang mang, hủy vật chặn khi đang đi. GUI Registry Validate/Undo và SubScene chuyển dữ liệu sang ECS độc lập còn cần nghiệm thu.

Sau correctness mới đo workload2.000 lính trên bộ dựng hình hiện hành: CPU/GPU khung hình tails, tick20Hz, Integration/field cache/native memory và dọn dẹp vật chặn. Ghi hardware, cấu hình, đã làm nóng-up, số lần chạy và p95; smoke40 tác nhân không thay đo hiệu năng này.

## 3. máy chủ rồi mạng

Tách authoritative mô phỏng khỏi hiển thị và đầu vào cục bộ. Chọn phiên bản Netcode/Transport sau khi kiểm tra compatibility chính thức tại thời điểm triển khai. Không serialize Entity handles; người phát lệnh phải lấy từ kết nối đã xác thực.

Pilot máy chủ+2client: bản đồ snapshot/revision/hash và Ready bắt tay; lệnh sequence/ack/dedup; ảnh trạng thái/phần thay đổi, nội suy phía máy khách, reconnect/full đồng bộ lại. Server20Hz, ảnh trạng thái cadence cấu hình được. Kiểm tra duplicate/out-of-thứ tự, mất gói, disconnect và foreign lệnh trước tăng bot/client.

[Kiến trúc và tải mục tiêu](../Multiplayer/ArchitectureChoice_2026-10-12.md) · [bản đồ loading hợp đồng](../Multiplayer/ServerMapLoading.md)

## Giới hạn phạm vi

Không tạo lại bản thử nghiệm, formation/hierarchy hoặc bộ dựng hình cũ. Không tự mua hosting/deploy, stage/commit/push/PR/merge. Không mở lại ownership/payment/worker chỉ vì kế hoạch cũ ghi chưa làm. Các bộ kiểm thử lối chơi đã xóa theo yêu cầu; muốn kiểm chứng mới cần mô phỏng hoặc phương pháp kiểm tra được chọn cho tác vụ đó.

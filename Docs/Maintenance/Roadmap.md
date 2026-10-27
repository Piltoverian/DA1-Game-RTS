# Việc tiếp theo — 17/10/2026

**Ưu tiên hiện tại là test terrain local rồi host + client trong Main. AI làm pipeline terrain; người dùng làm unit. Xem [terrain và checklist Play Mode](../Multiplayer/ServerMapLoading.md). Nghiệm thu input FOW local bên dưới vẫn còn cần.**

[Trạng thái](CurrentState.md) · [Handoff FOW](FogOfWarHandoff.md) · [Luật FOW hiện hành](../Architecture/FogOfWarRendering.md)

## FOW local sau triển khai bộ lệnh

Việc đầu tiên: trong Main kiểm tra preview Unknown bằng GhostUnknown.mat riêng → đặt kế hoạch → worker tới thử → tạo nhà hoặc chuyển FIFO khi thất bại. Tiếp theo mới nghiệm thu nhiều worker/queue dài, repair/gather snapshot mất, depot/cancel và selection/panel UI theo FOW. 20/20 regression và smoke Move không thay nghiệm thu chuột toàn gameplay. Attack đang chạy rồi mất vision còn chưa chốt; không tự thêm chase theo đề xuất cũ.

Multiplayer đã tiếp tục và đã cài Netcode; xem [hiện trạng](../Multiplayer/README.md). Pipeline terrain đã nối source, chưa nghiệm thu transport thật/Play Mode và chưa refactor full Mono.

## 1. Builder/combat correctness sau unit tests

Suite chốt 51/51 EditMode tests pass trước dọn; test/helper session đã xóa; xem [CurrentState](CurrentState.md) và [pipeline xây nhà](../Architecture/BuildingPipeline.md). Trước làm thêm, dùng [brainstorm-before-change](C:/Users/Legion/.codex/skills/brainstorm-before-change/SKILL.md), trình pipeline và chờ duyệt. Ưu tiên PlanBuild nhiều worker → Build → work tăng → completed trên bản clamp cuối, rồi queue/cancel/tiếp tục nhà dở.

Kiểm chứng AOE/team và các edge case Move/attack chase trong Play Mode. Đối chiếu nhóm authoritative, FixedStep và ECB; reproduce trước khi sửa. Không mặc định rà soát tĩnh là lỗi khi chạy hoặc unit PASS là Play Mode PASS.

## 2. Dogfood và tải thật

Chạy Main bằng đầu vào thật: select/box-select, Move, Gather, Build, ghost trên plateau/ramp/mountain, đặt nhà trùng, thợ giữ/bỏ hàng đang mang, hủy vật chặn khi đang đi. GUI Registry Validate/Undo và SubScene chuyển dữ liệu sang ECS độc lập còn cần nghiệm thu.

Sau correctness mới đo workload2.000 lính trên bộ dựng hình hiện hành: CPU/GPU khung hình tails, tick20Hz, Integration/field cache/native memory và dọn dẹp vật chặn. Ghi hardware, cấu hình, đã làm nóng-up, số lần chạy và p95; smoke40 tác nhân không thay đo hiệu năng này.

## 3. máy chủ rồi mạng

Tách authoritative mô phỏng khỏi hiển thị và đầu vào cục bộ. Chọn phiên bản Netcode/Transport sau khi kiểm tra compatibility chính thức tại thời điểm triển khai. Không serialize Entity handles; người phát lệnh phải lấy từ kết nối đã xác thực.

Nghiệm thu server+2client theo [hướng hiện hành](../Multiplayer/README.md): Ghost/relevancy và ký ức FOW, terrain full Explored, client Mono qua adapter, RPC bọc payload command/request. Không thêm lại WorldId/MapId/MapRevision vào frame hoặc tự dựng cơ chế ack/nội suy trùng Netcode. Kiểm tra mọi chức năng hiện có, quyền điều khiển, mất gói, disconnect/reconnect và đổi trận trước tăng tải.

[Kiến trúc và tải mục tiêu](../Multiplayer/ArchitectureChoice_2026-10-12.md) · [bản đồ loading hợp đồng](../Multiplayer/ServerMapLoading.md)

## Giới hạn phạm vi

Không tạo lại bản thử nghiệm, formation/hierarchy hoặc bộ dựng hình cũ. Không tự mua hosting/deploy, stage/commit/push/PR/merge. Không mở lại ownership/payment/worker chỉ vì kế hoạch cũ ghi chưa làm. Các bộ kiểm thử lối chơi đã xóa theo yêu cầu; muốn kiểm chứng mới cần mô phỏng hoặc phương pháp kiểm tra được chọn cho tác vụ đó.

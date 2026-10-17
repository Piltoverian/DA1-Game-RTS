# Việc tiếp theo — 17/10/2026

**Bước tiếp theo là chuẩn bị multiplayer; render FOW local đã được người dùng chốt. Các mục builder/combat dưới đây vẫn là backlog độc lập.**

[Trạng thái đã làm](CurrentState.md) · [Quy tắc lối chơi đã chốt](VerificationHistory.md)

## Chuẩn bị multiplayer sau local

Đọc [handoff FOW](FogOfWarHandoff.md) và [quyết định multiplayer](../Multiplayer/README.md). Việc đầu tiên là bảng phân chia component/system/prefab/caller thành server ECS / Ghost data / client Mono / adapter, bao phủ chức năng hiện có. Sau đó chốt package compatibility, pipeline và cách triển khai. Chưa cài Netcode hoặc refactor client Mono; không coi bước local là protocol mạng. Người dùng không yêu cầu kiểm tra thêm điều kiện drag trước bước này.

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

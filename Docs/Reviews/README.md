# Review và lịch sử kiểm tra

- [Review tĩnh 06/10/2026](ProjectAudit_2026-10-06.md): phát hiện từ code/cấu hình/log và hướng xử lý. Chưa phải kết quả test Play Mode/Player.
- `BuildHistory/build_results.txt`, `BuildHistory/build_results_v2.txt`: log build cũ chuyển từ root; không có metadata đủ để dùng làm bằng chứng cho trạng thái hiện tại.

Chủ project xác nhận code chạy được tới tạo map; input/UI/SelectManager từng hoạt động nhưng lần test đã lâu. Ưu tiên hiện tại là sắp xếp và dọn gọn, sau đó mới tiếp tục logic và kiểm chứng các rủi ro review.

Không dùng nhãn ưu tiên trong review tĩnh để tự động kết luận chức năng đang hỏng. Khi kiểm chứng, bổ sung scene, config, Unity version, bước tái hiện và kết quả.

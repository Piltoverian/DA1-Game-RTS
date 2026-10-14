# Chơi nhiều người — kiến trúc và phạm vi

**Đọc [quyết định kiến trúc](ArchitectureChoice_2026-10-12.md) trước khi triển khai mạng — khoảng 3 phút để nắm phạm vi.**

máy chủ authoritative với ảnh trạng thái/phần thay đổi và nội suy phía máy khách. Dedicated máy chủ là mốc so sánh nghiệm thu; máy chủ kiêm máy chơi kiêm máy khách dùng development. Mục tiêu: simulation20Hz,2.000 lính tổng,map512 ×512,2–10player. Chưa có nghiệm thu máy chủ+2client hoặc đo hiệu năng tải này.

1. [Architecture choice](ArchitectureChoice_2026-10-12.md): lý do chọn mô hình, ranh giới thế giới ECS và tải mục tiêu.
2. [máy chủ bản đồ loading](ServerMapLoading.md): máy chủ tạo bản đồ một lần, gửi kết quả, máy khách verify và Ready.
3. [lộ trình](../Maintenance/Roadmap.md): combat/dogfood/load trước máy chủ và mạng lượt thử nhỏ.

API bản đồ ảnh trạng thái ECS đã có; transport/mã hóa dữ liệu/session và đồng bộ trạng thái end-to-end chưa được tài liệu nghiệm thu. Các mô tả máy chủ kiêm máy chơi-Relay,500 lính hoặc snapshot10–12Hz trong lộ trình cũ đã được bỏ để tránh lẫn với quyết định hiện tại.

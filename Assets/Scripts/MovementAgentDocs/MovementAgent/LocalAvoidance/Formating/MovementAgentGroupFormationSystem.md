# MovementAgentGroupFormationSystem.cs

Hệ thống này chịu trách nhiệm tổ chức quân đội thành các khối đội hình (Formation) khi họ nhận được lệnh di chuyển. Nó đảm bảo rằng các Unit không chỉ đứng tụm lại một điểm mà sẽ dàn hàng một cách có trật tự tại đích đến.

---

## 1. Cơ chế Gom nhóm (Grouping)
Khi người chơi click chuột, hàng loạt Unit sẽ nhận được `TargetChangeRequest`. Hệ thống thực hiện:
1. **Gom nhóm theo đích**: Sử dụng `NativeParallelMultiHashMap` để nhóm các Unit có cùng tọa độ đích đến (được làm tròn để tránh sai số nhỏ).
2. **Phân loại theo đảo**: Trong mỗi nhóm đích, các Unit lại được chia nhỏ theo `IslandID`. Điều này cực kỳ quan trọng: Nếu bạn chọn quân ở hai bờ sông khác nhau và click vào một điểm, quân bên bờ nào sẽ dàn hàng theo "điểm chuẩn" của bờ đó.

---

## 2. Tìm kiếm vị trí (Slot Generation)
Hệ thống hỗ trợ 2 loại đội hình chính:
- **`Box` (Hình hộp)**: Tìm các vị trí theo các lớp hình vuông lan tỏa từ tâm.
- **`Circle` (Hình tròn)**: Tìm các vị trí theo các vòng tròn đồng tâm.

**Quy tắc hợp lệ (`IsValidSlot`)**:
Mỗi vị trí Slot ứng viên phải thỏa mãn:
- Nằm trong vùng đi bộ được (`Cost < 250`).
- Phải cùng `IslandID` với nhóm Unit đang xét (Không để lính xếp hàng dưới nước hoặc trên vách đá).

---

## 3. Phân bổ vị trí (Assignment)
Sau khi đã có danh sách các Slot hợp lệ, hệ thống sẽ gán mỗi Unit vào một Slot gần nó nhất:
- Sử dụng thuật toán so sánh khoảng cách đơn giản để tối ưu hiệu năng.
- Cập nhật `slotTarget` cho Unit.
- Đặt `lookAtPoint` dựa trên hướng từ tâm đội hình đến đích, giúp Unit quay mặt về đúng hướng sau khi đứng vào hàng.

---

## 4. Tương tác với các hệ thống khác
Hệ thống này chạy **SAU** `IntegrationFieldSystem` để có dữ liệu hòn đảo mới nhất, nhưng chạy **TRƯỚC** `MovementAgentTargetSystem` để `TargetSystem` có thể biết được Slot nào đã được gãn.

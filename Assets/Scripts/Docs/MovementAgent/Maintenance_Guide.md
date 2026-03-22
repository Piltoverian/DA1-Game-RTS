# Movement Agent - Hướng dẫn Bảo trì & Tối ưu

Tài liệu này chứa các lưu ý quan trọng để bảo trì, mở rộng và tinh chỉnh hiệu năng cho hệ thống di chuyển.

---

## 1. Các giới hạn hệ thống (Known Limits)

### Giới hạn số lượng Đảo (Island Limit)
Hiện tại, trong file `IntegrationFieldSystem.cs`, số lượng "hạt giống" (seeds) cho mỗi đảo đang bị giới hạn cứng là **1000**.
- **Vấn đề**: Nếu bản đồ của bạn có quá nhiều hòn đảo vụn vặt (> 1000), Game có thể bị crash khi Unit cố gắng tìm đường.
- **Giải pháp**: Cần nâng cấp mảng này sang `NativeParallelHashMap` để co giãn linh hoạt theo quy mô bản đồ.

### Độ phân giải Context Steering
Trong `MovementAgentAvoidanceSystem.cs`, chúng ta đang dùng 16 hướng (Resolution = 16).
- **Tối ưu**: Nếu bạn thấy CPU quá tải khi có hàng nghìn Unit, hãy cân nhắc giảm xuống 8 hoặc 12 hướng.
- **Độ mượt**: Nếu Unit rẽ hướng bị giật, hãy tăng Resolution lên 24 hoặc 32 (tuy nhiên sẽ tốn CPU hơn).

---

## 2. Tinh chỉnh tham số (Tuning)

Các tham số trong `ContextSteeringConfig` ảnh hưởng trực tiếp đến "tính cách" của Unit:

| Tham số | Ý nghĩa | Ảnh hưởng |
| :--- | :--- | :--- |
| **DangerThreshold** | Ngưỡng nguy hiểm | Giá trị càng thấp, Unit càng "nhát" và né vật cản từ xa. |
| **H_Alpha** | Độ trễ bám đuổi | Dùng để làm mượt hướng đi. Giá trị nhỏ sẽ mượt hơn nhưng phản ứng chậm hơn. |
| **AvoidRadius** | Bán kính né | Khoảng cách Unit bắt đầu cảm nhận được hàng xóm. |

---

## 3. Quản lý Thực thi (Execution Groups)

Toàn bộ logic di chuyển được đặt trong **`FixedStepSimulationSystemGroup`**.
- **Lý do**: Đảm bảo tính nhất quán của vật lý và né tránh bất kể FPS (Frame-rate) là bao nhiêu.
- **Lưu ý**: Nếu bạn muốn di chuyển mượt hơn nữa ở FPS cao, có thể sử dụng `TransformSystemGroup` để nội suy vị trí cuối cùng trong `Actuation` layer.

---

## 4. Troubleshooting (Gỡ lỗi)

Nếu Unit không di chuyển hoặc đứng im:
1.  **Kiểm tra Grid**: Đảm bảo Grid đã được khởi tạo và ô lưới tại vị trí Unit không phải là vật cản (Cost < 250).
2.  **Kiểm tra Flow Field**: Dùng `MovementAgentDebugSystem` để xem có mũi tên hướng nào chỉ về đích không.
3.  **Kiểm tra Island ID**: Xem `IslandID` của Unit và của Đích đến có khớp nhau không. Nếu khác đảo, Unit sẽ chỉ đi đến điểm gần nhất trên "đảo nhà" của nó.

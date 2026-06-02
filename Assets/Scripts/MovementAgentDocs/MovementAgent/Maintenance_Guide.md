# Movement Agent - Hướng dẫn Bảo trì & Tối ưu

Tài liệu này chứa các lưu ý quan trọng để bảo trì, mở rộng và tinh chỉnh hiệu năng cho hệ thống di chuyển.

---

## 1. Các giới hạn hệ thống (Known Limits)

### Giới hạn số lượng Đảo (Island Limit)
Hiện tại, trong file `IntegrationFieldSystem.cs`, số lượng "hạt giống" (seeds) cho mỗi đảo đang bị giới hạn cứng là **1000**.
- **Vấn đề**: Nếu bản đồ của bạn có quá nhiều hòn đảo vụn vặt (> 1000), Game có thể bị crash khi Unit cố gắng tìm đường.
- **Giải pháp**: Cần nâng cấp mảng này sang `NativeParallelHashMap` để co giãn linh hoạt theo quy mô bản đồ.

### Tối ưu chi phí ORCA (Time Horizon)
Thuật toán ORCA dùng giá trị `timeHorizon` để quyết định xem nó cần nhìn trước bao nhiêu giây để né.
- **Tối ưu hóa**: Tránh đặt `timeHorizon` quá lớn (>3 giây) khi có quá đông Unit, bởi vì số lượng Velocity Obstacle (VO) sẽ quét rất rộng và Unit có thể bị đứng hình vì không tìm thấy vận tốc an toàn chung.
- **Độ linh hoạt**: Nếu Unit né nhau quá sát nút (sắp đụng tới nơi mới lách), hãy tăng nhẹ `timeHorizon` lên để chúng bắt đầu thay đổi vận tốc từ xa.

---

## 2. Tinh chỉnh tham số (Tuning)

Các tham số mới (hoặc các component cấu hình Agent) ảnh hưởng cốt lõi đến sự mượt mà của bầy đàn:

| Tham số/Hệ số | Ý nghĩa | Ảnh hưởng |
| :--- | :--- | :--- |
| **Radius (Bán kính)** | Kích thước vật lý thực của Unit. | RADIUS cực kỳ quan trọng đối với ORCA. Quá to sẽ gây tắc đường hẹp, quá nhỏ sẽ làm model Unit bị xuyên thấu. |
| **Separation Multiplier** | Hệ số dạt ra khi bị nén (chồng chéo). | Nếu bầy đàn tụ lại và bị jitter (run bần bật), hãy giảm điểm Separation xuống. Hệ số này quyết định lực đẩy `Hard Collision`. |
| **First-come-first-settled Threshold** | Ngưỡng khoảng cách và vận tốc để một Unit được tính là đã "neo đậu". | Nếu giá trị này quá nhỏ, Unit sẽ lách liên tục quanh điểm đến mà không chịu dừng lại làm vật cản neo. |

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

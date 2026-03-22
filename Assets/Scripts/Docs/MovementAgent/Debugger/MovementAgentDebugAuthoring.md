# MovementAgentDebugAuthoring.cs

File này cung cấp giao diện điều khiển (UI) trong Unity Inspector để bật/tắt các tính năng gỡ lỗi trực quan của hệ thống Movement Agent.

---

## 1. Các tùy chọn gỡ lỗi (Flags)
Bạn có thể bật tắt các thành phần sau trên GameObject gỡ lỗi:

- **`showVelocity`**: Bật/tắt hiển thị vận tốc thực tế (Tia trắng).
- **`showDesiredVelocity`**: Bật/tắt hiển thị hướng mong muốn (Tia xanh lá).
- **`showContextSteer`**: Bật/tắt hiển thị các tia Interest và Danger (Tia xanh dương và đỏ).
- **`showTargetLines`**: Bật/tắt hiển thị đường nối tới đích và vị trí đội hình.
- **`showProximity`**: Bật/tắt hiển thị vòng tròn bán kính của Unit.

---

## 2. Cơ chế Bake
Hệ thống Bake sẽ chuyển các lựa chọn này vào một thành phần **`MovementAgentDebugConfig`**.
- Thành phần này được thiết kế như một Singleton config, ảnh hưởng đến toàn bộ các Unit trong game.

---

## 3. Cách thiết lập
Để sử dụng hệ thống gỡ lỗi này trong Scene:
1. Tạo một GameObject mới (ví dụ tên là `MovementAgentDebugger`).
2. Gắn script `MovementAgentDebugAuthoring` vào GameObject đó.
3. Tích chọn các ô mà bạn muốn quan sát.
4. Khi chạy game, `MovementAgentDebugSystem` sẽ tự động tìm thấy các thiết lập này và thực hiện việc vẽ lên màn hình.

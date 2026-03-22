# FlowFieldCleanUp.cs

Hệ thống này đóng vai trò là bộ dọn rác (Garbage Collector) cho các Flow Field, đảm bảo bộ nhớ của game không bị phình to sau một thời gian chơi dài.

---

## 1. Cơ chế hoạt động
Hệ thống quét qua tất cả các thực thể có thành phần `FlowFieldRefCount` và kiểm tra hai điều kiện để quyết định xem có xóa nó hay không:

### Điều kiện 1: Không có Unit nào sử dụng
Kiểm tra `refCount.value == 0`. Nếu vẫn còn lính đang đi trên đường này, hệ thống sẽ bỏ qua.

### Điều kiện 2: Không nằm trong Cache
Đây là điều kiện quan trọng nhất (`inCache == false`).
- Nếu một Flow Field không có Unit nào sử dụng nhưng vẫn đang nằm trong danh sách **Cache** (160 mục gần nhất), hệ thống **SẼ KHÔNG XÓA**.
- Lý do: Để nếu người chơi click lại vào đúng điểm đó ngay sau đó, chúng ta có thể tái sử dụng ngay lập tức mà không cần tính toán lại từ đầu.

---

## 2. Khi nào thực thể bị xóa thực sự?
Một thực thể Flow Field sẽ thực sự bị `DestroyEntity` khi:
1. Nó không còn Unit nào bám theo.
2. Nó đã bị đẩy ra khỏi danh sách Cache (bởi thuật toán LRU trong `FlowFieldCacheHelper`).

---

## 3. Thứ tự thực thi (Execution Order)
Hệ thống này chạy ở cuối cùng của nhóm `FixedStepSimulationSystemGroup`, sau khi quân lính đã thực hiện xong các bước di chuyển (`ActuatorSystem`).
- **Lý do**: Để đảm bảo mọi thay đổi về gán đường đi trong khung hình hiện tại đã được ghi nhận trước khi thực hiện dọn dẹp.

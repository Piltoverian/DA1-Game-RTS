# IntegrationFieldSystem.cs

Đây là hệ thống phức tạp nhất trong nhóm `Field`, chịu trách nhiệm tính toán bản đồ khoảng cách (Integration Field) từ mọi điểm trên bản đồ về tới đích.

---

## 1. Cơ chế Đa đảo (Multi-Island Support)
Thay vì chỉ tính toán từ 1 điểm đích duy nhất, hệ thống này hỗ trợ việc di chuyển trên nhiều hòn đảo tách biệt.

### Thuật toán tìm Seed:
1. Duyệt qua toàn bộ bản đồ để xác định có bao nhiêu hòn đảo (`IslandID`).
2. Với mỗi hòn đảo, tìm ra một ô lưới nằm trên đảo đó mà **gần với mục tiêu thực tế nhất**.
3. Ô lưới này được gọi là **Seed** (Hạt giống).
4. Tất cả các Seed của các đảo khác nhau sẽ được coi là "điểm đích phụ" và được đưa vào hàng đợi BFS cùng một lúc với `bestcost = 0`.

> [!NOTE]
> Hiện tại hệ thống đang sử dụng mảng tĩnh 1000 phần tử cho các đảo. Đây là giới hạn cần lưu ý (sẽ được nâng cấp lên HashMap ở bước tiếp theo).

---

## 2. Thuật toán BFS (Breadth-First Search)
Hệ thống sử dụng một biến thể của BFS (giống thuật toán Dijkstra) để lan tỏa chi phí từ các Seed ra toàn bộ bản đồ.

- **Chi phí bước đi**: 
  - Đi ngang/dọc: +10.
  - Đi chéo: +14 ($\approx 10 \times \sqrt{2}$).
- **Né vật cản**: Các ô có `GridNodeCost >= 250` sẽ bị bỏ qua hoàn toàn.
- **An toàn góc chéo**: Khi đi chéo, hệ thống kiểm tra 2 ô bên cạnh để đảm bảo Unit không "cắt góc" qua tường.

---

## 3. Quy trình thực thi
1. **Reset**: Đặt toàn bộ `bestcost` của các ô về `int.MaxValue`.
2. **Seed Injection**: Tìm Seed cho từng đảo, đặt `bestcost = 0` và đưa vào Queue.
3. **Propagation**: Lan tỏa chi phí cho đến khi Queue rỗng.
4. **Chuyển trạng thái**: Sau khi xong, chuyển trạng thái Flow Field sang `CalculatingDirection`.

---

## 4. Hiệu suất
Hệ thống sử dụng `IJobChunk` để có thể tính toán song song nhiều bản đồ Flow Field cùng lúc. Việc sử dụng `NativeQueue` trong Job đảm bảo tốc độ xử lý nhanh nhất có thể trên nền Burst Compiler.

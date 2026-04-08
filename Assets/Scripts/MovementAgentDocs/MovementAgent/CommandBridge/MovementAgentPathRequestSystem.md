# MovementAgentPathRequestSystem.cs

Hệ thống này đóng vai trò là "người giám sát" các yêu cầu di chuyển. Nó liên tục kiểm tra xem Unit có cần một đường đi mới (Flow Field) hay không.

---

## 1. Chức năng chính
Hệ thống quét qua tất cả các Unit có `MovementAgentComponent` và xác định xem mục tiêu hiện tại của Unit đã có đường đi tương ứng chưa.

---

## 2. IdentifyPathRequestJob
Đây là một `IJobEntity` chạy song song để tối ưu hiệu suất.

### Điều kiện kích hoạt yêu cầu mới:
Hệ thống sẽ thêm một thành phần `TargetChangeRequest` vào Unit trong các trường hợp sau:
1. **Chưa có đường đi**: Unit có mục tiêu nhưng `FieldEntity` đang là `Null`.
2. **Thay đổi mục tiêu**: Mục tiêu hiện tại (`currentworldtarget`) của Unit nằm ở một ô lưới khác với đích đến của Flow Field mà nó đang bám theo.
3. **Mất liên kết**: Unit đang trỏ vào một FieldEntity nhưng thực thể đó không còn tồn tại hoặc không chứa component `FlowField`.

---

## 3. Vai trò của TargetChangeRequest
`TargetChangeRequest` hoạt động như một tín hiệu (Signal) để:
- Kích hoạt `MovementAgentGroupFormationSystem` tính toán lại vị trí đội hình.
- Kích hoạt `FlowFieldAssignmentSystem` tìm kiếm hoặc tạo mới một Flow Field phù hợp.

---

## 4. Thứ tự thực thi (Execution Order)
Hệ thống này chạy **TRƯỚC** `IntegrationFieldSystem`.
- **Lý do**: Để các yêu cầu di chuyển được ghi nhận ngay trong cùng một khung hình tính toán lưới, giúp giảm độ trễ (latency) khi người chơi ra lệnh.
```csharp
[UpdateBefore(typeof(IntegrationFieldSystem))]
```

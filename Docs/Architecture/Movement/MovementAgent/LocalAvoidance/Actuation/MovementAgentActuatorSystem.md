# Actuator: di chuyển và giữ lệnh

Cập nhật 2026-10-12. Chạy bước cố định sau ORCA.

stuckTime tăng khi độ dời XZ <10% speed×deltaTime, đặt lại khi có tiến triển. Kẹt xa đích vẫn giữ lệnh.

CrowdedDestination chỉ xảy ra khi có hàng xóm, closestDistance<bán kính×2,5; cách realTarget<=tối đa(stoppingDistance,bán kính×2); stuckTime>0,5 s và ClearSegment tới đích clear. Khi đó hastarget=false, velocity/preferredVelocity zero, isSettled=true và ghi stopReason.

Separation correction giới hạn 1,5 bán kính mỗi bước mô phỏng, Y=0, đi qua MoveAndSlide. Sau đó velocity×deltaTime cũng qua MoveAndSlide; velocity cập nhật theo độ dời thực tế.

Separation không bảo đảm 100% hết chồng lấn. Endpoint điều kiện chặn không phải depenetration và không bảo vệ transform update sau bộ thực thi chuyển động.

XZ đổi thì bật PendingHeight. Actuator giữ Y; độ cao đạt mẫu lại sau đó. Quay theo velocity hoặc currentworldtarget khi đứng.

isSettled có thể true khi vận tốc zero nhưng hastarget vẫn true. Đọc stopReason/hastarget để phân biệt đứng chờ với kết thúc lệnh. arrivalRadius không phải điều kiện bỏ lệnh.

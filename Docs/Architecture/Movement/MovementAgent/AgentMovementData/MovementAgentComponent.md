# Dữ liệu MovementAgent

Cập nhật 2026-10-12. chuyển động đo khoảng cách trên XZ.

| MovementAgentComponent | Ý nghĩa |
|---|---|
| speed, hastarget | Tốc độ và lệnh còn mục tiêu |
| stopReason | None, Arrived, CrowdedDestination, InvalidTarget, Released, Paused |
| FieldEntity | Field đang dùng |
| currentworldtarget | Đích yêu cầu |
| realTarget | Đích điều hướng thực tế |
| navigationTargetCell | Ô đích cho trường |
| targetResolutionGeneration | lưới sinh dữ liệu khi phân giải |
| targetResolutionKind | None, Direct, BuildingBlockage, NaturalResolved |
| preferredVelocity | mục tiêu tạo |
| velocity | Avoidance/actuator cập nhật |
| lookAtPoint | Dữ liệu điểm nhìn |

Struct không có slotTarget/useSlotTarget.

Avoidance thành phần gồm bán kính, gridIndex, separationForce, IsStatic, closestDistance, neighborCount. Không có avoidanceForce/lastAvoidDir/avoidTimer trong struct hiện tại.

Steering gồm arrivalRadius, stoppingDistance, isSettled, rotationSpeed, stuckTime, lastPosition, minDistanceToTarget. arrivalRadius là vùng hãm. isSettled có thể true dù hastarget vẫn true; lính đứng chờ chưa chắc đã xong lệnh.

TargetChangeRequest chứa newWorldTarget. MovementAgentFieldCleanUpData giữ FieldEntity cho dọn dẹp.

PendingHeight là enableable thành phần riêng. Khi XZ đổi, bộ thực thi chuyển động bật đang chờ; độ cao hệ thống đặt Y theo địa hình cộng điểm gốc độ lệch rồi tắt. Y trong LocalTransform không được dùng để tăng khoảng cách lối chơi.

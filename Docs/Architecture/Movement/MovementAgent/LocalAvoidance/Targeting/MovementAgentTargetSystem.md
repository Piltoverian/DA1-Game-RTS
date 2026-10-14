# TargetSystem: chọn đích và vận tốc mong muốn

Cập nhật 2026-10-12 theo UnitTargetJob hiện tại. Chạy bước cố định sau FlowDirectionSystem.

Mỗi bước mô phỏng đặt preferredVelocity=zero trước. Nếu không có lệnh, chưa phân giải hoặc thiếu dữ liệu trường thì không tạo vận tốc mới.

## Chọn realTarget

- Building: quét viền ngoài bounding box, chọn ô đi được cùng vùng liên thông gần lính nhất bằng khoảng cách XZ. Không dùng công thức distToAgent + distToClick×0,25 của tài liệu cũ.
- NaturalResolved: dùng tâm navigationTargetCell.
- Direct: dùng currentworldtarget.
- Khác vùng liên thông: tìm hạt giống của vùng liên thông lính trong IslandSeed bộ đệm.

navigationTargetCell ngoài biên hiện return, không tự đặt hastarget=false trong nhánh này.

## Hướng và đến đích

Cách realTarget < stoppingDistance thì hastarget=false, stopReason=Arrived, isSettled=true.

CalculateSafeFlowVelocity đọc hướng của ô chứa lính và xử lý nguy cơ đâm góc. Không song tuyến tính bốn ô. Gần đích, hòa trộn với hướng trực tiếp theo blendRange=tối đa(arrivalRadius×1,5,cellsize×2). Chỉ hòa trộn nếu ClearSegment tới đích clear.

Trong arrivalRadius, giảm vận tốc theo khoảng cách, clamp hệ số 0,1..1. Đây là vùng hãm, không phải vùng kết thúc lệnh. Tiến triển đủ lớn cập nhật minDistanceToTarget và đặt lại stuckTime.

TargetSystem hiện không đọc slotTarget/formationRange; các đoạn về vị trí và dot threshold trong tài liệu cũ không thuộc luồng xử lý này.

## Điểm cần đo

GridIslands/GridCosts được lấy bằng GetBuffer mặc định quyền RW dù tác vụ chỉ đọc. Việc lấy bộ đệm có thể làm chính luồng chờ phụ thuộc. ngăn hàng đợi không tự sửa điểm chờ này. Khi đọc bộ đo hiệu năng, phân biệt thời gian chạy tác vụ với thời gian chờ Complete.

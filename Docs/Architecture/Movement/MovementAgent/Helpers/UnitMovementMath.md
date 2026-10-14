# UnitMovementMath: hướng trường hướng và tránh góc

Cập nhật 2026-10-12. Dùng discrete lấy mẫu, không song tuyến tính bốn ô.

GetRawDirection đọc hướng của ô chứa lính. Dữ liệu sai, ngoài biên hoặc bestcost=int.MaxValue trả zero. CalculateFlowVelocity đổi XZ sang ô, nhân hướng với speed và đặt velocity.y=0.

CalculateSafeFlowVelocity kiểm tra đoạn nhìn trước bằng vùng chiếm chỗ bán kính. Nếu hướng trường hướng nguy cơ đâm góc, thử tới tâm ô kế, rồi tâm ô hiện tại nếu clear. Không hòa hướng qua vật cản.

CalculateGridGradient đo tới diện tích ô blocked chi phí>=255, không chỉ tâm. Nếu ở trong ô, chọn phía gần nhất để tạo hướng ra; đây không phải bảo đảm depenetration hoàn chỉnh.

ClearSegment, swept disc và MoveAndSlide nằm trong GridObstacleAvoidance. Actuator áp MoveAndSlide cho tách hàng xóm và velocity.

CalculateDanger, CalculateQuadraticOffset và Context Steering 16 tia trong tài liệu cũ không thuộc tệp hiện tại. ORCA dùng ORCAMath riêng.

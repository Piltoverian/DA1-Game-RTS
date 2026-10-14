# Tránh vật cản trên XZ và trượt dọc tường có quét va chạm

`GridObstacleAvoidance` là hàm hỗ trợ Burst không cấp phát bộ nhớ. Đĩa quét kiểm tra các ô bị chặn (chi phí >=255) và biên bản đồ. Bước kiểm tra chính xác dùng mặt đã dịch theo bán kính và góc tròn, thay cho vùng chiếm chỗ vuông. Tối đa ba lượt xử lý tiếp xúc giữ dịch chuyển tiếp tuyến và loại phần đi vào vật cản. Vật thể đang chồng lấn có thể thoát ra ngoài; cơ chế này không giải quyết đầy đủ việc sinh ở vị trí sai hoặc đặt nhà đè lên lính.

ORCA nhận vận tốc mong muốn đã được làm mềm bằng khoảng nhìn trước0.25giây, tối đa0.75 ô. Điều hướng loại dần thành phần đi vào tường và giữ tiếp tuyến. Nó dùng bán kính vật lý với một khoảng đệm số học nhỏ, tránh bán kính mềm lớn làm đóng nhầm lối hẹp. Lực đẩy khỏi tường cố định cũ đã bỏ, nên lính đứng yên không tự trôi chỉ vì ở gần tường.

Bước thực thi chuyển động áp dụng quét và trượt cho cả lực tách hàng xóm và vận tốc cuối. Vận tốc lưu lại phản ánh chuyển động thực sau giới hạn va chạm. Thay đổi XZ vẫn bật PendingHeight và không sửa Y. Chuyển động XZ có tiến triển đặt lại bộ đếm kẹt thay vì tăng nó khi đang trượt. Chỉ trộn hướng đi thẳng gần đích khi đoạn tới đích, có xét bán kính, không bị chặn; nếu không, trường hướng vẫn chọn đường. Không nội suy song tuyến tính trường hướng.

Trường hướng toàn cục chọn đường trên lưới chung, chưa có trường khoảng trống riêng cho từng cỡ lính. Lính lớn có thể dừng an toàn trước lối quá hẹp nhưng không tự tìm đường rộng hơn. Phân lớp khoảng trống theo bán kính là cải tiến điều hướng riêng. Giới hạn va chạm tường cũng có thể đổi vận tốc ORCA đã chọn để tránh hàng xóm; vì vậy đây chưa phải bằng chứng đám đông hoàn toàn không va chạm.

Kiểm tra `GridObstacleAvoidanceValidation.Run` bao gồm tránh sớm, ổn định khi vận tốc0, tiếp xúc tường/góc tròn/biên bản đồ, bước nhanh, lực tách, hành lang vừa/quá hẹp, đích đi thẳng bị chặn và bộ thực thi trượt30 bước mô phỏng không nhận nhầm kẹt. `TerrainHeightMainSmoke.Run` đã qua biên dịch/kiểm tra hồi quy và chạy Main với1,960gốc tĩnh,40 tác nhân trong90 khung hình theo các chế độ nâng/phẳng/nâng. Báo cáo: `Artifacts/TerrainHeight/obstacle-avoidance.txt`, `Artifacts/TerrainHeight/main-smoke.txt`, `Artifacts/TerrainHeight-obstacle-smoke.log`.

## Kích thước hiện tại và khoảng khởi đầu đề xuất

Main báo cellsize1.953125, bán kính agent0.732422=0.375 ô, đường kính0.75 ô. MovementAgentAuthoring chuyển tên trường lưu cũ `radius` sang `diameterCells`; GridSnapperSystem đặt bán kính bằng diameterCells×cellsize/2 và đổi kích thước model/collider cùng nhau. Không coi trường mẫu đối tượng cũ là bán kính theo đơn vị thế giới. Lượt triển khai này không đổi kích thước mẫu đối tượng.

Với lính mặt đất nhỏ, bán kính0.3–0.4 ô (0.586–0.781 đơn vị thế giới ở đây) là khoảng khởi đầu thiết kế, chưa phải kích thước đẹp nhất đã đo. Giá trị0.375 hiện tại là mốc so sánh hữu ích. Bán kính lớn cần đường rộng và dữ liệu khoảng trống phù hợp; đường kính3 ô của xe tăng là bán kính1.5 ô, không vừa hành lang1 ô.

## Giữ lệnh sau lượt chạy dài kiểm tra chuyển động

Bộ thực thi không còn bỏ lệnh sau0.2–0.7giây ít tiến triển khi còn xa đích. Tắc đường giữ `hastarget` bật. Khi đích còn chờ phân giải, không tích lũy thời gian kẹt. Dừng tại đích đông cần có hàng xóm, ít nhất0.5giây không tiến triển, khoảng cách trong tối đa(stoppingDistance,hai bán kính) và đoạn tới đích đã phân giải không có vật cản. `arrivalRadius` dùng giảm tốc, không quyết định đã đến đích.

`MovementStopReason` ghi đã đến, đích đông, đích sai, chủ động giải phóng lệnh và tạm dừng. Nhật ký chạy dài ghi khoảng cách đã phân giải, độ sẵn sàng trường hướng và khoảng cách yêu cầu: đến cạnh nhà không được ghi thành lỗi không rõ nguyên nhân vì chưa tới tâm nhà. Lực tách có thể đẩy tác nhân đã đến ra ngoài khoảng dừng. Kẹt lâu do tường/đám đông vẫn cần phục hồi điều hướng; giữ lệnh không bảo đảm cuối cùng tới đích.

Lính lệch tâm kiểm tra khoảng nhìn trước trước khi dùng trường hướng. Nếu bị chặn, nó hướng tới tâm ô kế khi đoạn quét thông thoáng, hoặc về tâm ô hiện tại. Không lấy trung bình hướng các ô cạnh nhau. MoveAndSlide từ chối đích cuối làm vùng chiếm chỗ đang trống thành xuyên vật cản. Cơ chế không bao phủ thay đổi về sau do hệ thống khác hoặc vị trí bắt đầu mới bị chặn. Xem `Artifacts/MovementSoak/20261012-085545/report.md` cho lượt chạy6phút và lỗi còn lại.

## Đề xuất địa hình tiếp theo — chưa triển khai trong lượt này

bộ giao diện ở thời điểm báo cáo có các vùng cỏ dày/thưa, biến thể ảnh sprite, đỉnh núi và trang trí rìa. Nên cải thiện độ liền màu/hình giữa nhóm vật liệu và vẽ bóng tiếp xúc/vách nhẹ vào hình ảnh mỹ thuật trước khi thêm ảnh vật liệu; giữ đường và dáng lính dễ đọc.

Renderer ở thời điểm báo cáo đóng gói atlas khi dựng và gom các mặt đã sắp độ sâu toàn cục thành lô2,048mặt, không phải khối không gian. Có thể cân nhắc atlas đóng gói trước/dùng chung và phân vùng hiển thị, nhưng phải giữ thứ tự vẽ: sắp chunk32 ×32 tùy ý có thể gây che khuất sai. Biểu diễn đơn giản khi thu phóng xa giảm chi tiết mà không dùng một ảnh độ phân giải thấp cho mọi mức thu phóng. Chiều sâu vách thật vẫn là việc hình học3D/độ sâu riêng. Cần đo trước khi nhận lợi ích thời gian khung hình. Những đề xuất này thuộc thời điểm báo cáo; xem tài liệu bộ dựng hình hiện hành trước khi áp dụng.

# Transform mặt đất: lối chơi XZ và cập nhật độ cao khi cần

Các gốc mặt đất dùng chung LocalTransform với render/collider. Điều hướng, đến đích, đo kẹt/tiến triển, chọn resource/depot, tầm tìm mục tiêu và AOE dùng XZ. Quỹ đạo đạn và hình học điểm trúng vẫn là3D.

`PendingHeight` là thành phần bật/tắt được, được chuyển dữ liệu sang ECS ở trạng thái bật vào mẫu đối tượng lính/nhà/resource cùng `TerrainHeightOffset` để giữ Y của điểm gốc đã thiết kế. Instance kế thừa chúng; nhánh sinh thực thể không cần lấy mẫu bản đồ. Truy vấn thành phần còn thiếu xử lý các gốc tạo bằng thủ tục. Khi kiểm tra đã đăng ký, truy vấn khi chạy bỏ qua trạng thái bật/tắt.

ORCA bật cờ chờ khi vận tốc cuối khác0. Bộ thực thi cũng bật sau khi lực tách đổi XZ, kể cả tác nhân vận tốc0. Thiết lập vị trí mặc định và căn lưới model/lưới đánh dấu thay đổi vị trí. hệ thống teleport hoặc ghi vị trí mới sau này phải bật cờ khi đổi XZ.

Lượt độ cao bước cố định chạy sau thực thi/setup và trước PhysicsSystemGroup. Lượt mô phỏng bắt các thực thể sinh từ production/building trước TransformSystemGroup. Cả hai lên lịch cùng Burst tác vụ, chỉ trên tag bật. BlockageGridBakeSystem chạy trước transform vì đọc LocalTransform.XZ; phụ thuộc cũ chạy sau transform sẽ tạo vòng thứ tự chạy production/height. Job đặt `Y = sampledHeight + pivotOffset` rồi tắt cờ chờ. Gốc đứng yên không lấy mẫu lại; áp lại không cộng dồn độ cao. Loại mẫu đối tượng khỏi truy vấn.

Từ2026-10-13, thế giới ECS bản đồ sao chép bộ nhớ đệm bất biến từ GridComponent/GridTerrain theo lưới thực thể và `MapBuildState.Revision`, qua TerrainSurfaceData mà không cần camera/renderer. Bốn góc tái tạo chính xác bề mặt hai tam giác và dốc nối tầng cũ. Thay/hủy bộ nhớ đệm chỉ hoàn tất tác vụ ở ranh giới vòng đời bản đồ, rồi đánh dấu instance chờ lại. Topology địa hình cố định trong trận; vật chặn động không dựng lại độ cao. Mọi thế giới ECS dùng dữ liệu grid/revision; phương án dự phòng bộ dựng hình tĩnh đã bỏ, kiểm thử độc lập tạo bộ đệm lưới thật.

bộ đệm bản đồ có thể sẵn sàng độc lập Presentation. Cờ chờ vẫn bật cho đến khi lượt khởi tạo tiếp theo thấy dữ liệu. lối chơi XZ có thể chạy khi chưa có Y; dựng hình, gốc phóng đạn và chọn bằng tia khối va chạm vẫn cần độ cao. Đối tượng sinh sau lượt độ cao cuối được xử lý ở update sau. khối va chạm gốc theo LocalTransform; vật thể physics có parent còn cần truyền transform cha đúng.

Thử nghiệm ảnh vật liệu phẳng và tùy chọn tắt/bật độ cao đã bỏ. luồng xử lý vật liệu atlas dùng độ cao địa hình chung cộng độ lệch điểm gốc. Các hệ thống offset/restore được quản lý cũ và kiểm thử riêng đã xóa. chọn bằng tia dùng tia camera với khối va chạm đã nâng; chọn kéo chiếu transform thật và không cộng độ cao lần hai.

Main không có cấu hình chuyển dữ liệu sang ECS phẳng. Nó dựng hình ảnh lưới hình học ảnh ảnh sprite/atlas và bật độ cao cho gốc mặt đất. Hình học ảnh sprite địa hình vẫn phẳng; không chuyển toàn bộ thành địa hình mesh3D. Shader vật liệu dựng lại độ cao bề mặt ảo để ghi độ sâu hình học, gồm dãy núi chỉ phục vụ hiển thị.

chuyển động không còn trộn song tuyến tính hướng của bốn ô. Nó đọc hướng đã tính trong ô XZ chứa tác nhân. Tính hướng từ chối đích bị chặn và đường chéo có một ô theo bốn hướng chính cạnh bên bị chặn, khớp quy tắc không cắt góc của integration. Giữ làm mượt xoay và trộn flow/hướng thẳng gần đích. Nội suy tam giác độ cao và lọc ảnh vật liệu độc lập chuyển động, không đổi.

Tìm mục tiêu dùng UnitSpatialBucket sau chuyển động, xét tầm tâm-tới-tâm trên XZ thay vì OverlapSphere tới bề mặt collider3D. Tầm vì vậy khớp ShootAttackSystem; vùng chiếm chỗ khối va chạm lớn có thể khác ranh giới tìm mục tiêu cũ. Không có chuyển đổi mesh/render địa hình trong phần này.

Entry kiểm tra: `TerrainHeightValidation.RunRegression`. Bao phủ địa hình đến muộn, kế thừa mẫu đối tượng, sinh muộn, lấy mẫu chính xác, pivot/áp lại không đổi, lọc gốc đứng yên, lực tách vận tốc0, thay/vòng đời bề mặt, đến đích XZ và tìm mục tiêu khác cao độ.

## Bằng chứng lịch sử

Trước khi bỏ thử nghiệm, ngày2026-10-12 trên Unity6000.3.13f1: biên dịch, đang chờ-độ cao, hồi quy bản đồ runtime/snap và thứ tự hệ thống đã đạt. `TerrainHeightMainSmoke.Run` đạt khởi tạo Main với1,960gốc tĩnh/40 tác nhân: phẳng/nâng/phẳng, mỗi chế độ30 khung hình (90tổng), kế thừa thành phần mẫu đối tượng, cờ chờ đã tắt và height/pivot chính xác. Đây là kiểm tra chức năng, không phải đo hiệu năng. Báo cáo: `Artifacts/TerrainHeight/validation.txt`, `Artifacts/TerrainHeight/main-smoke.txt`, `Artifacts/TerrainHeight-main-smoke.log`.

Sau khi khôi phục ảnh sprite rendering và bỏ trường hướng song tuyến tính, biên dịch/Main kiểm tra nhanh đạt lại ở chế độ nâng/phẳng/nâng. Hồi quy lấy mẫu đạt với hướng hàng xóm đối nghịch, biên ô, đầu vào sai, cạnh chéo bị chặn và chéo mở. Báo cáo: `Artifacts/TerrainHeight/flow-sampling.txt`, `Artifacts/TerrainHeight-render-flow-smoke.log`. Cấu hình Main lưu trên disk bắt đầu nâng, không có ảnh vật liệu phẳng đã chuyển dữ liệu sang ECS.

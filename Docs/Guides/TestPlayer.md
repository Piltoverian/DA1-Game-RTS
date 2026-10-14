# Test người chơi trong cảnh Main

Object `Test Player` hiện nằm trong SubScene `Assets/_Project/Scenes/Main/EntitySubscene.unity`, giữ TestPlayer và PlayerContextAuthoring. người chơi Id = 1, Civ Def = TempCiv, Use khởi tạo Context bật. Đổi người chơi Id trên PlayerContextAuthoring để chọn vị trí khác; bộ chuyển dữ liệu sang ECS TestPlayer lấy cùng ID khi hai thành phần cùng object.

Object `Map Settings` mang PlayerBootstrapAuthoring: giữ MapConfig, thiết lập và roster. Nó tạo người chơi ECS và sinh đối tượng đầu trận. Khi Use khởi tạo Context bật, PlayerContextAuthoring chuyển dữ liệu sang ECS liên kết tới context trong roster, không tạo context thứ hai. Khi tắt, thành phần chuyển dữ liệu sang ECS context độc lập cho cảnh không dùng khởi tạo.

Chỉ gắn CivDef, bộ chuyển dữ liệu sang ECS tự lấy CivDef.Id (TempCiv → temp.civ); không có trường Civilization Id/Civ Id nhập tay. Asset phải có ID hợp lệ và đăng ký trong GameReg để bootstrap/lookup lối chơi dùng được. Trong chế độ khởi tạo, Age và tài nguyên đầu trận vẫn do bản đồ Settings cấu hình.

lựa chọn, UnitImage, CommandMenu, CommandButton và UnitController lấy danh tính từ Test người chơi hoặc LocalTestPlayer đã chuyển dữ liệu sang ECS khi SubScene đóng. Chờ world/context tồn tại và khởi tạo Ready trước khi cho phép thao tác.

Sau khi nhập code/scene, mở Main và Play: map/bootstrap Ready, chọn thợ hoặc TownHall của người chơi 1, kiểm tra icon/menu và lệnh. Đổi ID để kiểm tra quyền sở hữu; ID ngoài roster phải không điều khiển được thực thể và không phát NullReferenceException. Nếu cảnh đang mở có thay đổi chưa lưu, nạp lại bản cảnh trên disk để thấy Test người chơi và tên bản đồ Settings mới.

# Test Player trong scene Main

Object `Test Player` hiện nằm trong SubScene `Assets/_Project/Scenes/Main/EntitySubscene.unity`, giữ TestPlayer và PlayerContextAuthoring. Player Id = 1, Civ Def = TempCiv, Use Bootstrap Context bật. Đổi Player Id trên PlayerContextAuthoring để chọn slot khác; baker TestPlayer lấy cùng ID khi hai component cùng object.

Object `Map Settings` mang PlayerBootstrapAuthoring: giữ MapConfig, settings và roster. Nó tạo player ECS và spawn đầu trận. Khi Use Bootstrap Context bật, PlayerContextAuthoring bake binding tới context trong roster, không tạo context thứ hai. Khi tắt, component bake context độc lập cho scene không dùng bootstrap.

Chỉ gắn CivDef, baker tự lấy CivDef.Id (TempCiv → temp.civ); không có trường Civilization Id/Civ Id nhập tay. Asset phải có ID hợp lệ và đăng ký trong GameReg để bootstrap/lookup gameplay dùng được. Trong chế độ bootstrap, Age và tài nguyên đầu trận vẫn do Map Settings cấu hình.

Selection, UnitImage, CommandMenu, CommandButton và UnitController lấy danh tính từ Test Player hoặc LocalTestPlayer đã bake khi SubScene đóng. Chờ world/context tồn tại và bootstrap Ready trước khi cho phép thao tác.

Sau khi import code/scene, mở Main và Play: map/bootstrap Ready, chọn worker hoặc TownHall của player 1, kiểm tra icon/menu và lệnh. Đổi ID để kiểm tra ownership; ID ngoài roster phải không điều khiển được entity và không phát NullReferenceException. Nếu scene đang mở có thay đổi chưa lưu, reload bản scene trên disk để thấy Test Player và tên Map Settings mới.

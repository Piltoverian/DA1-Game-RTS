# Bàn giao ca tiếp theo — 14/10/2026

## 1. Mục tiêu và phạm vi

Lượt chốt session: đã dọn 4 bộ Editor test và helper dogfood sau kết quả cuối 51/51 PASS; xóa artifact thử nghiệm cũ của session, giữ XML/log cuối và receipt bàn giao. Skill brainstorm chỉ còn bản cá nhân ngoài project. Chủ dự án đã chuyển việc gán RegistryAuthoring.Instance từ Awake sang Baker.Bake. Giữ nguyên sửa đổi runtime của người dùng; đã nghỉ dùng và xóa tests/helper của session. Hướng dẫn chính: [registry/UI](../Guides/GameData/RegistryPresentation.md), [BuildingPipeline](../Architecture/BuildingPipeline.md), [CurrentState](CurrentState.md), [Roadmap](Roadmap.md).

Mọi cập nhật tiếp theo phải khảo sát → brainstorm → trình pipeline → chờ duyệt theo [skill](C:/Users/Legion/.codex/skills/brainstorm-before-change/SKILL.md) và [AGENTS.md](../../AGENTS.md). Phê duyệt đã có vẫn hiệu lực; không hỏi lại cùng pipeline. Không tự stage/commit/push/PR/merge, không reset diff chưa hiểu hoặc tạo lại harness đã xóa.

## 2. Thay đổi và quyết định

- RegistryAuthoring không còn Awake. Đầu Baker.Bake kiểm tra số authoring, gán static __instance từ gameDataRegistry, rồi tạo GameDataRegistryComponent và buffer/blob ECS. Static ScriptableObject không thuộc ECS World. Mở SubScene không bảo đảm Awake chạy; baking không làm mất asset GameReg.
- ResetInstance tại SubsystemRegistration vẫn xóa static khi bắt đầu Play. Chưa chứng minh Baker chạy lại và khôi phục tham chiếu khi dùng cache/player build; không mô tả sửa đổi này như đã nghiệm thu mọi vòng đời runtime.
- Test registry từng thực thi Baker thật, kiểm tra component/static/icon/preview và đã PASS; source test đã xóa theo yêu cầu chốt session.
- Registry dogfood thành Assets/_Project/Resources/GameReg.asset, GUID f1769d193e7cb494eb67a0d8bf379fbc. Registry cũ và registry riêng trong fixture đã bỏ. Main giữ 8 player/spawn slot, Civ A/B xen kẽ; scene/config dogfood thử nghiệm sau đó đã xóa trong lượt dọn fixture dư; asset gameplay còn dùng vẫn giữ.
- Worker/WorkerBase/Worker.prefab, TrainWorker và Build mới trong Tests/Fixtures/TwoCivDogfood dùng cùng TownHall mới. StartWorker/unlock/train/build đều nối bộ mới; registry không đăng ký bộ MapGeneration cũ. Asset nguồn MapGeneration vẫn giữ cho fixture. Helper Setup/ConfigureWorker đã xóa; asset hiện hành vẫn được game tham chiếu.
- DefaultWorker/DefaultTownHall nằm ở registry. BakeCivs ưu tiên prefab civ, thiếu từng loại mới fallback độc lập. Map không có trường prefab Worker/Hall (đã vắng trước lượt này). Worker được chọn vẫn phải trong roster, không yêu cầu tech; bootstrap validate và lập plan trước instantiate.
- Projectile: bullet đến đích theo bước speed × deltaTime, recheck target/team khi trúng; artillery không distress. TeamID quyết định đồng minh, quyền điều khiển theo PlayerID.
- PlanBuild snapshot toàn bộ selection → placement tạo một nhà → Build cho từng worker hợp lệ. Build chỉ giao công trình đã có. Preview scale trước show; builder clamp footprint cộng clearance, navigation dùng anchor LocalTransform. Các lỗi worker từ ca trước được chủ dự án xác nhận đã sửa; builder/work0 mới được theo dõi riêng.

Workspace D:/Unity/TechTextAndChosingForRTS sample, branch Refactor-Prepare-for-multiplayer-bot. Working tree có nhiều thay đổi từ nhiều lượt và của chủ dự án; chưa commit, chưa có snapshot đầy đủ. Phần chưa commit chỉ còn trong workspace, checkout HEAD không khôi phục chúng; khi chuyển máy mang cả untracked và artifact cần dùng. Không quy toàn bộ diff cho lượt này.

Lượt dọn tiếp theo đã được người dùng duyệt: xóa 14 harness Editor, 369 asset fixture dư cùng meta và runner Task3; sau đó đã tách preview thành tool riêng và dọn các asset chỉ còn phục vụ helper. Manifest dọn đã xóa theo yêu cầu ngày 15/10/2026. Không tái tạo harness đã xóa. Thay đổi vẫn chưa commit.

## 3. Bằng chứng kiểm chứng

[Suite sau sửa Baker](../../Artifacts/TwoCivDogfood/registry-baker-results.xml): **51/51 PASS**, 0 fail/skip, Unity 6000.3.13f1 batch EditMode, kết thúc 14/10/2026 12:41:51 UTC (19:41:51 Bangkok). Log registry-baker-tests.log ghi exit0. Test mới thực thi Baker và xác nhận component ECS, static registry, icon dogfood và prefab preview; không thay thế Play Mode/cache/player build. [Receipt](../../Artifacts/Handoff/registry-baker-verification.json) lưu hashes và giới hạn. Lần launch đầu không chạy được vì project đang được Editor giữ; chủ dự án đã đóng Editor để chạy lại. Không xóa lock/Library.

Artifact thử nghiệm cũ của session đã xóa theo yêu cầu; giữ registry-baker-results.xml, registry-baker-tests.log và registry-baker-verification.json. 51/51 là bằng chứng trước dọn test, không phải suite còn có thể chạy lại. Không tái tạo các test/helper đã nghỉ dùng. Manifest/log/receipt cleanup cũ đã xóa theo yêu cầu ngày 15/10/2026. Sau dọn, Unity batch import/compile thành công và thoát exit0; kiểm tra 124 Markdown, 0 broken local targets. Kết quả được giữ dưới dạng tóm tắt này.

## 4. Việc còn lại và giới hạn

- Registry/UI: kiểm chứng Play Mode sau reset, không rebake, Domain Reload bật/tắt, SubScene đóng và player build dùng dữ liệu bake sẵn. UnitImage vẫn bật Image khi icon null, có thể hiển thị ô trắng; lượt này không sửa UI/runtime.
- Builder clamp: PlanBuild nhiều worker → tất cả tới tầm → CompletedWork tăng → completed; tiếp tục nhà dở/queue/cancel/chuyển lệnh. Không ghi đã chơi pass bản cuối.
- Gather/box-select, AOE/team Play Mode chưa nghiệm thu. Dogfood còn HUD stale khi đổi player và khó click unit sát TownHall; kiểm tra duplicate registry theo pipeline mới trước sửa.
- Multiplayer transport/server+2client/resync, 2.000 unit/30 FPS và simulation20Hz chưa thực hiện. Không biến bằng chứng dogfood cục bộ thành multiplayer.
- Không tạo lại prototype/formation/hierarchy/portal, renderer cũ hoặc các gameplay harness đã xóa. [Handoff chuyển động](VerificationHistory.md) là phạm vi độc lập.

## 5. Một hành động tiếp theo

Đề xuất pipeline kiểm chứng vòng đời registry/UI trong Main: reset khi vào Play → có/không rebake → chọn TownHall → kiểm tra Instance và sprite. Trình người dùng duyệt trước hành động mới; kết quả mong đợi là xác định tham chiếu UI còn hợp lệ sau reset và khi dùng dữ liệu bake sẵn.

Receipt dọn trước đã xóa; helper BootstrapSpawnSmokeTest nay đã xóa. Artifact lượt dọn tool/docs/atlas đã xóa theo yêu cầu; kết quả được tóm tắt trong CurrentState.

## Tool và tài liệu hiện hành

Đã tách preview, xóa helper test và thống nhất một bộ docs với JadeSlate là nguồn atlas reference. Đọc [CurrentState](CurrentState.md) để xem danh sách tool và giới hạn; [lịch sử hợp nhất](VerificationHistory.md) giữ các báo cáo cũ. Main vẫn dùng LunarTheme.

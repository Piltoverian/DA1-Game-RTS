# Bàn giao ca tiếp theo — 17/10/2026

## Phạm vi đang tiếp tục: chuẩn bị multiplayer sau FOW local

**Bắt đầu từ [FogOfWarHandoff](FogOfWarHandoff.md)**: render local đã nối và chạy trong Main, 33 assertion của lượt render; sửa height sau đó đo 5 unit lệch 0. Các lượt kiểm chứng có source/giới hạn riêng, không phải toàn suite đã chạy lại sau mọi sửa đổi. Snapshot trả full Explored; chỉ building/resource giữ hình cuối; không có WorldId/MapId/MapRevision trong frame. Điều kiện drag cuối là thời gian >0,05 giây AND chiều rộng rect > Screen.width/40; chỉ build pass, người dùng không yêu cầu Play lại trước bàn giao.

Hướng mới ở [Multiplayer](../Multiplayer/README.md): server ECS authoritative, client Mono qua adapter, Ghost/variants/templates theo package tương thích, RPC bọc payload command/request hiện có. Chưa cài Netcode; client presentation/camera hiện vẫn còn ECS systems, chưa chuyển Mono. Việc đầu tiên: kiểm kê component/system/prefab/caller và ranh giới server/Ghost/Mono/adapter bao phủ các chức năng hiện có. Không dừng bản đầu ở Worker/TownHall; không coi snapshot Entity local là wire protocol.

## Phạm vi độc lập còn giữ: snapper và selection (bàn giao lịch sử)

Các trạng thái workspace/bằng chứng bên dưới thuộc lượt snapper/registry cũ; không thay thế trạng thái ngày 17/10 ở handoff FOW phía trên.

**Mục tiêu/phạm vi:** sửa tỷ lệ unit 0.75 ô so với resource 1 ô và nhà 7 ô; khôi phục đường request của drag. Các phần registry/UI ở dưới vẫn là công việc chưa nghiệm thu độc lập.

**Đã triển khai:** ModelSnapSource thêm VisualSize/VisualCenter và capture bounds mesh có transform con, bỏ health bar/sprite chọn. GridSnapperAuthoring, GridSnapperMath, GridSnapperBakingSystem và GridSnapperSystem dùng scale đều theo cạnh bounds XZ lớn nhất; physics/avoidance/blockage giữ footprint logic. [Tài liệu chính](../MapGeneration/GridSnapper_ResourceSpawn.md). SelectManager.DragSelect đã bỏ nhánh TerrainVisualSurface ghi trực tiếp Selected rồi return; click/drag đều đi SelectionRequest → SelectSystem. Giữ nguyên diff SelectSystem.cs đã có trước ca này, không nhận là thay đổi của ca.

**Quyết định người dùng:** CancelBuildingRequest đi riêng ngoài CommandQueue là chủ ý. SelectionMode.Add/Remove/Clear còn TODO, người dùng chưa có thời gian xử lý; không tự mở rộng sửa hai phần này. Yêu cầu tăng zoom sensitivity và mở góc camera snap về base đã khảo sát nhưng chưa triển khai; đề xuất trước là 2 → 3 đơn vị/nấc và mức nhìn ban đầu xa hơn, chưa được nghiệm thu.

**Nơi tiếp tục:** code nằm tại `D:\Unity\TechTextAndChosingForRTS sample`, dưới Assets/_Project/Scripts/Runtime/Simulation/MapGeneration và Presentation/Input/GameManager/Manager/SelectManager.cs. Khi kết thúc sửa code, các thay đổi chưa commit; lúc cập nhật docs, git status chỉ còn bốn file docs được sửa, không còn báo diff source. Không suy đoán ai đã commit hoặc đồng bộ giữa hai thời điểm. Branch/revision chưa xác nhận; không có patch/snapshot riêng. Docs của lượt này chưa commit. Không reset diff người dùng.

**Bằng chứng:** dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet thành công sau sửa selection và bản snapper cuối: 0 lỗi, 5 cảnh báo. Gọi hàm GridSnapperMath.Scale thực từ Temp/bin/Debug/Assembly-CSharp.dll qua PowerShell, ba trường hợp đích 0.75/1/7 đều khớp trong sai số 0.00001 và scale đều. Không có log/receipt mới được giữ. Kiểm tra này không bao phủ capture mesh, baking thực, physics hay render. Rà code tĩnh input/command không thấy thêm nhánh UI ghi state trực tiếp như drag; không phải nghiệm thu mọi pipeline bằng Play Mode.

**Còn lại:** rebake và kiểm tra kích thước mesh/physics/child transforms trong Main; kiểm tra click nhà rồi drag unit có bỏ single select, cả terrain và đường picking dự phòng. Zoom/camera vẫn chờ làm. Không coi PASS Main lịch sử ở docs là PASS công thức mới.

**Hành động đầu tiên:** mở Main trong Unity, rebake dữ liệu rồi vào Play; đối chiếu bounds XZ và collider của Worker/Soldier 0.75, resource 1 và TownHall 7 theo cùng cellsize. Cạnh mesh XZ dài nhất phải bằng số ô đã khai báo, hình dáng được giữ và collider nằm tại anchor logic.

## 1. Mục tiêu và phạm vi

Lượt chốt session: đã dọn 4 bộ Editor test và helper dogfood sau kết quả cuối 51/51 PASS; xóa artifact thử nghiệm cũ của session, giữ XML/log cuối và receipt bàn giao. Skill brainstorm chỉ còn bản cá nhân ngoài project. Chủ dự án đã chuyển việc gán RegistryAuthoring.Instance từ Awake sang Baker.Bake. Giữ nguyên sửa đổi runtime của người dùng; đã nghỉ dùng và xóa tests/helper của session. Hướng dẫn chính: [registry/UI](../Guides/GameData/RegistryPresentation.md), [BuildingPipeline](../Architecture/BuildingPipeline.md), [CurrentState](CurrentState.md), [Roadmap](Roadmap.md).

Mọi cập nhật tiếp theo phải khảo sát → brainstorm → trình pipeline → chờ duyệt theo [skill](C:/Users/Legion/.codex/skills/brainstorm-before-change/SKILL.md) và hướng dẫn AGENTS.md do người dùng cung cấp trong hội thoại. Phê duyệt đã có vẫn hiệu lực; không hỏi lại cùng pipeline. Không tự stage/commit/push/PR/merge, không reset diff chưa hiểu hoặc tạo lại harness đã xóa.

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

## 5. Hành động tiếp theo của phạm vi registry cũ

Đề xuất pipeline kiểm chứng vòng đời registry/UI trong Main: reset khi vào Play → có/không rebake → chọn TownHall → kiểm tra Instance và sprite. Trình người dùng duyệt trước hành động mới; kết quả mong đợi là xác định tham chiếu UI còn hợp lệ sau reset và khi dùng dữ liệu bake sẵn.

Receipt dọn trước đã xóa; helper BootstrapSpawnSmokeTest nay đã xóa. Artifact lượt dọn tool/docs/atlas đã xóa theo yêu cầu; kết quả được tóm tắt trong CurrentState.

## Tool và tài liệu hiện hành

Đã tách preview, xóa helper test và thống nhất một bộ docs với JadeSlate là nguồn atlas reference. Đọc [CurrentState](CurrentState.md) để xem danh sách tool và giới hạn; [lịch sử hợp nhất](VerificationHistory.md) giữ các báo cáo cũ. Main vẫn dùng LunarTheme.

# Bàn giao ca tiếp theo — 20/10/2026

## Phạm vi đang tiếp tục: terrain local/server/client

### 1. Mục tiêu và yêu cầu đã chốt

Người dùng đang học làm game online lần đầu. AI phụ trách toàn bộ pipeline terrain; người dùng tự làm unit và muốn được hướng dẫn từng bước nhỏ. Không yêu cầu học xử lý model trước. Terrain phải chạy được local trước, rồi host/client dùng cùng compiler và renderer. Mỗi player chỉ nhận dữ liệu terrain theo FOW riêng của mình, không gửi chung toàn map hoặc seed toàn map qua network.

Pipeline và checklist chính tại [ServerMapLoading](../Multiplayer/ServerMapLoading.md). Không tự thêm unit networking, thay luật FOW hoặc tự chuyển MatchState sang Running. Host có hình thông qua ClientWorld; dedicated server chỉ tính và gửi dữ liệu.

### 2. Workspace, thay đổi và quyết định

- Workspace: `D:\Unity\TechTextAndChosingForRTS sample`; branch `Fog-of-wars`; HEAD `352e9ec8069308c77e177999ba23cd21db496e3c`.
- Toàn bộ thay đổi terrain/match mới còn trong working tree, có file mới chưa được Git theo dõi. Chưa stage/commit/push; chưa tạo patch/snapshot. Session tiếp theo phải mở đúng workspace này. Chỉ checkout HEAD sẽ không lấy lại phần mới; nếu chuyển máy phải mang cả file sửa, file mới và `.meta`.
- Có nhiều thay đổi ngoài terrain từ trước và các prefab cũ đã bị xóa. Không reset/restore/dọn chúng như artifact của lượt này. Asset runtime trong Tests/Fixtures vẫn đang được dùng.
- Unity `6000.3.13f1`; package resolve: Entities `1.5.0`, Netcode `1.14.3`, Transport `2.7.4`. Không đọc số manifest cũ rồi kết luận chưa cài Netcode.

Các entry point cần đọc, không phải đọc lại toàn repo:

| Nguồn | Trạng thái và vai trò |
|---|---|
| [TerrainVisualSourceSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainVisualSourceSystem.cs) | Mới: cache compiler trên local/server theo revision/hash, không phụ thuộc camera. |
| [TerrainCellVisualData.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainCellVisualData.cs) | Mới: tính sẵn top/wall/mountain mask và độ cao; object render + buffer ô/vách + AppendToGhostBuffers. |
| [TerrainChunkGhostAuthoring.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainChunkGhostAuthoring.cs) | Mới: metadata stream và Baker cho prefab Ghost. |
| [TerrainNetworkServerSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainNetworkServerSystem.cs) | Mới: RPC join/ready, connection→player, sender và cleanup. TerrainPlayerConnection có thể được unit networking dùng lại. |
| [TerrainNetworkReceiveSystem.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainNetworkReceiveSystem.cs) | Mới: nhận các chunk của owner, kiểm tra range và tải thiếu, dựng dữ liệu ô/mask. |
| [MapTerrainPresentationSystem.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/MapTerrainPresentationSystem.cs) | Đã sửa: chọn nguồn local/network, gọi renderer chung, gửi ACK sau render. |
| [TerrainMapRenderer.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainMapRenderer.cs) | Đã sửa: ưu tiên ClientWorld; yêu cầu load SubScene vào các World tạo sau khi Main đã mở. |
| [TerrainChunkRenderer.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainChunkRenderer.cs), [CanonicalIsometricSprites.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/CanonicalIsometricSprites.cs), [TerrainVisualSurface.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainVisualSurface.cs) | Đã sửa: render/picking dùng dữ liệu ô đã tính; không cần client biết ô hàng xóm. |
| [GridAuthoring.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Grid/GridAuthoring.cs) | Đã sửa: bake TerrainSyncSettings từ theme và prefab. |
| [TerrainChunkGhost.prefab](../../Assets/_Project/Prefabs/TerrainChunkGhost.prefab), [Main/EntitySubscene.unity](../../Assets/_Project/Scenes/Main/EntitySubscene.unity) | Mới/đã gắn: HasOwner, Interpolated, Static, prefabId khớp GUID; grid dùng LunarTheme giống Main. |

Luồng thực tế đã nối: server gen map → compiler tính hình từ map đầy đủ → cấp slot từ PlayerBootstrapSlot cho connection → lọc Explored theo player → các Ghost vùng 8×8 riêng cho connection, cập nhật 10 Hz → client nhận header/buffer → renderer dùng chung → TerrainReadyRequest → server ghi TerrainClientReady.

Header và hai buffer đều SendToOwner; server lấy GhostOwner.NetworkId từ connection thật, không lấy PlayerId do client gửi. Các client khác có thể có entity Ghost rỗng, không có dữ liệu terrain của owner khác. Đây là cấp slot để thử local, chưa phải tài khoản/authentication hoặc khôi phục danh tính qua reconnect.

Terrain chờ PlayerBootstrapState.Ready để có base/nguồn vision, nhưng không chờ MatchPhase.Running. NetworkStreamInGame chỉ bật snapshot. Client không cần CubeSpawner hoặc RPC của scene ví dụ để nhận terrain Main. Giữ bộ ví dụ NetcodeForEntitiesSetup, không xóa nó như artifact.

Ngữ cảnh match đã chốt từ các lượt trước: không dùng custom simulation group. Gameplay dùng FixedStepSimulationSystemGroup, phần Late vẫn Late. [ServerMatch.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Shared/ServerMatch.cs) có SharedStatic cache; 24 system dùng `ServerMatch.isGamePlayCanRun(ref state)`: local được phép, server chỉ Running đúng owner World, client bị chặn. Dùng SetPhase để đổi phase/cache, không sửa singleton bằng đường khác rồi mong cache tự cập nhật. MatchLifecycle mới chuyển Preparing→WaitingForPlayers; ready terrain chưa đủ để bắt đầu toàn trận.

### 3. Bằng chứng đã có và giới hạn của bằng chứng

Các kết quả sau là của lượt triển khai trước khi viết handoff này, không chạy lại trong lượt docs:

- Unity 6000.3.13f1 batch compile thành công và có output Netcode tạo serializer cho header/buffer terrain, RPC join/ready. Compile cuối gồm helper load SubScene cho World tạo sau Main và prefabId đã cố định.
- Fixture ECS ngoài Play Mode qua **32 assertion**: cache local/revision, cấp player khác nhau và full roster, lọc terrain riêng, parity độ cao/mask, chờ thiếu chunk, bỏ owner khác, mask visible/explored, không rebuild khi chỉ đổi visibility, range vách lỗi, Ready sai/đúng phiên và cleanup disconnect.
- 32 assertion chạy trước hai sửa cuối về helper load SubScene/prefabId. Hai sửa cuối đã compile, chưa có kiểm tra hành vi riêng. Fixture dùng World/EntityManager trong bộ nhớ, không chạy truyền snapshot qua transport thật và không render hình bằng camera.
- Lệnh compile đã dùng: `Unity.exe -batchmode -nographics -quit -projectPath "D:/Unity/TechTextAndChosingForRTS sample" -logFile ".../Temp/TerrainPipelineFinalCompile.log"`. Fixture đã dùng `-executeMethod TerrainPipelineVerification.Run`. Source fixture, `.meta` và các log một lần đã xóa theo yêu cầu; không có log/fixture được giữ để session mới chạy lại nguyên lệnh fixture.
- Đã kiểm tra prefab GUID/tham chiếu Main, `.meta`, link docs và diff whitespace. Review source độc lập không tìm thấy blocker; đây không phải nghiệm thu runtime.
- Người dùng chưa xác nhận hình terrain hoặc mạng trong Play Mode. Không diễn giải lời “ok/tiếp tục” thành kết quả test.

Lưu ý môi trường kiểm tra: dotnet SDK 10 từng crash CLR; build dotnet ngoài Unity còn có cảnh báo NetCodeSourceGenerator về template filename Windows. Dùng compiler Unity làm bằng chứng source generation. Chạy Unity từ PowerShell có thể trả về ngay trước khi tiến trình thật kết thúc; phải đọc marker compile/fixture trong log, không coi exit code của shell là PASS. Batch cuối đã compile và báo quit thành công rồi kẹt ở Cleanup mono; đã xác minh command line và dừng đúng tiến trình tác vụ để giải phóng log, không dừng Editor của người dùng.

### 4. Việc còn lại

**Debug mới nhất — người dùng đã làm rõ triệu chứng:** terrain trong vùng đã nhìn thấy render đúng; chỉ vùng chưa khám phá lộ skybox thay vì màu đen. Không tiếp tục coi đây là mất toàn bộ terrain hoặc kết luận camera/UI là nguyên nhân. LocalFogPresentationSystem chỉ chạy local và tự đổi nền camera đen; nhánh client thiếu bước này. Đã thêm SolidColor + Color.black sau Configure camera trong UpdateNetworkTerrain của MapTerrainPresentationSystem.cs. Chưa xác nhận Play Mode sau sửa nền. Các sửa frontend/camera trước đó vẫn còn trong working tree: ẩn frontend khi Main load, chọn camera theo scene và gán World mặc định sang ClientWorld; không tự revert nếu chưa có yêu cầu.

- Chưa nghiệm thu Main offline, host + hai client, hình camera, actual baking/streaming prefab khi chạy và transport snapshot thật. Chưa benchmark map lớn/nhiều player.
- Kiểm tra Main load trước connect: các World mới phải nhận GridComponent, TerrainSyncSettings và prefab. Nếu chờ mãi, lần theo bootstrap/map phase trước rồi mới kết luận Ghost lỗi.
- Kiểm tra MapPresentationState.Ready ở client và TerrainClientReady trên connection server. Ready terrain chỉ là một phần readiness toàn trận; không auto Running.
- Kiểm tra hai player khác quyền FOW; nếu cùng team chia sẻ map thì tập ô có thể giống nhau theo luật hiện tại. Giữ vùng đã khám phá tối khi mất vision, unknown không có dữ liệu, núi/vách không đổi theo việc biết thêm hàng xóm.
- Chunks có thể nhận từ các tick visibility khác nhau; không cam kết snapshot vision toàn map nguyên tử. Khi thêm ô, hiện rebuild tập terrain đã biết, chưa cập nhật mesh tăng dần.
- Reconnect được cấp slot trống, không có tài khoản/resume identity. Nếu nhận lại cùng player slot, lịch sử Explored trên server vẫn thuộc slot đó. Kiểm tra không giữ hình/cache sai connection/session.
- Kiểm tra style hiện so thông số hình học/visual seed, chưa hash toàn texture/catalog. Chưa chuyển toàn presentation sang Mono; các ECS presentation system hiện vẫn điều phối renderer Mono.
- Không khôi phục các test Editor đã xóa hoặc giữ script/log tạm như kết quả bàn giao. Không tự commit/push. Quy tắc brainstorm trước sửa vẫn áp dụng cho phạm vi mới; pipeline terrain nêu trên đã được người dùng duyệt triển khai.

### 5. Một hành động đầu tiên cho session mới

**Kiểm tra vùng unknown của Host sau bản sửa nền camera.** Người dùng dừng Play, chờ Unity compile rồi chạy Host lại. Kết quả mong đợi: terrain đã khám phá giữ hình hiện có, vùng chưa khám phá là đen; vùng đã khám phá nhưng khuất vẫn tối theo FOW. Người dùng đã xác nhận terrain vùng được nhìn thấy render đúng, không hỏi lại như thể toàn bộ terrain đang mất. Sau đó kiểm tra offline/hai client theo tài liệu chính.

## FOW local và bộ lệnh — lịch sử 17/10

**Bắt đầu từ [FogOfWarHandoff](FogOfWarHandoff.md)** để đọc bằng chứng FOW cũ. Bộ lệnh local đã nối: Attack mới chỉ target địch đang thấy, Build preview ba trạng thái và đi thử Unknown, FIFO rồi tìm nhà dở theo Gather, Repair không queue, Gather theo snapshot. GhostUnknown.mat riêng đã gán Main, bỏ fallback lúc chạy. Luật chính ở [kiến trúc FOW](../Architecture/FogOfWarRendering.md).

Regression 20/20 ECS/picking pass; smoke Main có FOW và Move pass, trước các sửa cuối picking/selection; material riêng chỉ build/kiểm tra liên kết asset, chưa xem lại Play. Build runtime/Editor 0 lỗi, 5 warning cũ ở lượt đầy đủ. Không gộp thành nghiệm thu mọi thao tác chuột. HEAD hiện tại `352e9ec`, branch `Fog-of-wars`; source đã trong commit, lượt docs này chưa commit. Các mốc/workspace bên dưới là lịch sử.

**Kiểm tra FOW còn giữ:** kiểm tra bằng chuột trong Main preview Unknown với material riêng → phát kế hoạch → tới thử → thành công hoặc tiếp tục FIFO khi bị block. Multiplayer đã tiếp tục ở phần terrain phía trên. Hướng server ECS/client Mono/Ghost/RPC ở [Multiplayer](../Multiplayer/README.md); client full Mono chưa refactor.

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

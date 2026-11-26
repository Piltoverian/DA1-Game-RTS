# Cấu trúc và cách làm việc

## Mở project

Unity Editor **6000.3.13f1**, theo [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt). Package được resolve trong [packages-lock.json](../Packages/packages-lock.json); không dùng một phiên bản manifest cũ để kết luận package đang chạy.

| Package | Phiên bản resolve lúc cập nhật docs |
|---|---|
| Entities | 1.5.0 |
| Entities Graphics | 1.5.0 |
| Unity Physics | 1.4.5 |
| Netcode for Entities | 1.14.3 |
| Transport | 2.7.4 |
| Burst | 1.8.30 |
| URP | 17.3.0 |

Mở project bằng Unity Hub, chờ import/compile rồi mở [Main](../Assets/_Project/Scenes/Main.unity). Grid, registry và player authoring nằm trong [EntitySubscene](../Assets/_Project/Scenes/Main/EntitySubscene.unity). Main dùng [LunarTheme](../Assets/_Project/Data/Terrain/Generated/MoonTerrain/LunarTheme.asset).

Offline cần LocalSimulation World; kiểm tra cấu hình [CustomBootstrap](../Assets/NetcodeForEntitiesSetup/Scripts/CustomBootstrap.cs) nếu bật chế độ host/client. Kết nối mạng bắt đầu từ [ConnectionUI](../Assets/NetcodeForEntitiesSetup/Scenes/ConnectionUI.unity); xem [Multiplayer](Multiplayer/README.md). Không thay bootstrap chỉ để che lỗi scene/data chưa sẵn sàng.

## Tìm đúng nơi sửa

| Thư mục dưới Assets/_Project/Scripts | Trách nhiệm |
|---|---|
| Runtime/Simulation/Commands | Quyền, dispatch command, tiếp cận mục tiêu đã biết. |
| Runtime/Simulation/Construction, Economy, Combat | State machine gameplay và biến đổi dữ liệu authoritative. |
| Runtime/Simulation/Movement | Grid/cost, field, target, ORCA, actuation, height. |
| Runtime/Simulation/MapGeneration, Players | Map recipe, geometry source, resource plan, bootstrap và connection/player. |
| Runtime/Simulation/FogOfWar | Visibility queries, explored membership, snapshot được phép biết. |
| Runtime/Presentation | Input, UI, camera, render, picking và ký ức hình. |
| Runtime/GameData | SO definitions, validation, authoring, blobs và registry. |
| Runtime/Events | Cầu nối event tới presentation. |
| Editor/GameData, Editor/Terrain | Inspector/validator và atlas import/preview; không vào player runtime. |

Tests/Fixtures còn chứa prefab/SO được Main/registry tham chiếu: **không xóa theo tên Tests**. ArtSource là nguồn thiết kế; Art/Data/Resources/Prefabs chứa dữ liệu bàn giao/runtime. Library, Temp, obj và csproj/solution Unity là generated/cache; không dọn toàn bộ cache như một phần sửa code.

## World, tick và structural change

- Simulation authoritative chạy LocalSimulation hoặc ServerSimulation; client nhận dữ liệu và render. Kiểm tra WorldSystemFilter/UpdateInGroup của system đang sửa.
- Gameplay/navigation chủ yếu trong FixedStepSimulationSystemGroup; có thể có nhiều fixed tick trong một render frame. Late/presentation không mặc nhiên thuộc cùng nhịp.
- [ServerMatch.cs](../Assets/_Project/Scripts/Runtime/Simulation/Shared/ServerMatch.cs) cho local chạy; server chỉ chạy khi phase Running và cache thuộc đúng World; client không chạy gameplay authoritative. Đổi phase qua SetPhase, không ghi singleton trực tiếp rồi bỏ qua SharedStatic cache.
- [MatchLifecycleSystem](../Assets/_Project/Scripts/Runtime/Simulation/Shared/MatchlLifeCycleSystem.cs) hiện chỉ chuyển Preparing → WaitingForPlayers khi bootstrap Ready. Terrain Ready không tự bắt đầu toàn trận.
- Không giữ DynamicBuffer/RefRW qua structural change. Snapshot command trước khi placement/production tạo entity; ECB playback là điểm publish thay đổi đã hoãn. Deferred Entity trong ECB phải được remap ở cả component/buffer tham chiếu.
- Job đọc/ghi lookup/buffer cần dependency đúng; hoàn tất job trước khi đưa dữ liệu sang managed Unity graphics. Native container phải có owner/disposal rõ ràng.

## Quy tắc sửa

Đọc caller và source trước; giữ validation, rollback và ranh giới thông tin FOW. Dùng API/helper hiện có; không thêm framework cho một caller. Tách kiểm chứng compile, fixture ECS và Play Mode/mạng thật. Không tự stage/commit/push/merge. Kết quả và việc tiếp theo nằm ở [CurrentState](Maintenance/CurrentState.md).

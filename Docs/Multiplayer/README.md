# Multiplayer: identity, handshake và gameplay

Server xử lý gameplay; client nhận Ghost và dùng presentation hiện có. Civ, unit/prefab/definition và building hiện vẫn **load all**. Chưa áp dụng sketch bootstrap lọc theo CivID.

Flow và vị trí code chi tiết nằm ở [GameplayNetworking](GameplayNetworking.md). Luồng terrain/renderer dùng chung với local nằm ở [Terrain](../Architecture/Terrain.md).

## Identity và ownership

`PlayerJoinRequest` được xử lý bởi `PlayerJoinServerSystem` trong [PlayerConnectionSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Players/PlayerConnectionSystem.cs). Server lấy `SourceConnection`, cấp slot chưa dùng, rồi trả `PlayerJoinAcceptedRpc`.

- `MatchSessionId`: ID chung của trận, nằm trên Match Ghost.
- `JoinGeneration`: ID lượt join, không phải ID trận.
- `PlayerId`: slot gameplay do server cấp.
- `NetworkId`: ID connection do NetCode cấp, dùng trong `GhostOwner.NetworkId`.
- `Epoch`: phiên terrain hiện tại.

Player context được bake thành seed. `NetworkGameplayBootstrapSystem` tạo context Ghost trên server, hoặc entity thường ở local. Client không materialize seed thành context trùng. `PlayerGhostOwnershipSystem` dùng mapping connection/player để gán owner cho context và entity có `EntityOwner`, kể cả spawn sau. Context, resource balance, technology và production queue vẫn gửi Owner. Team/civ roster công khai nằm trên Match Ghost.

Slot đã có chủ được giữ qua disconnect. Rejoin dùng vé riêng lưu local, không dùng session/PlayerId làm authentication. Chưa tích hợp account/lobby identity; xem GameplayNetworking để biết giới hạn transport và cách chọn Local profile.

## Handshake chung

```text
Join → identity server cấp
→ server gửi danh sách Ghost ID/spawn tick bắt buộc ban đầu
→ client nhận đủ Ghost + registry + terrain
→ MapTerrainPresentationSystem dựng renderer/fog/camera binding
→ PlayerHandshakeClientSystem gửi PlayerReadyRequest
→ PlayerHandshakeServerSystem validate connection/session/join/epoch
→ PlayerReadyAcceptedRpc
→ đủ PlayerBootstrapSlot bắt buộc: MatchLifecycleSystem chuyển Running
→ client nhận MatchState Running rồi mở selection/input
```

Không còn RPC join/ready riêng cho terrain. `NetworkStreamInGame` bật snapshot trước khi ready; không đồng nghĩa trận đã Running. Server bootstrap/FOW và gửi dữ liệu khởi tạo không chờ client ACK.

Manifest là danh sách Ghost ID + spawn tick cụ thể, không phải số hall/worker suy đoán. Nó chỉ chứa gameplay Ghost relevant ban đầu, context của player và Match Ghost. Mỗi client chỉ có một context thực; context session và GhostOwner phải khớp identity trước ACK. Manifest tới trước identity mới được giữ lại, ACK phiên cũ bị loại.

Roster trong Main hiện có tám slot. Đủ ready thì bắt đầu ngay; nếu còn thiếu thì chờ timeout cấu hình (mặc định30giây), giữ slot thiếu ở trạng thái Disconnected rồi bắt đầu. Smoke hai client cũ chỉ rút roster trong RAM, không sửa scene nguồn.

## Terrain và FOW relevancy

```text
TerrainVisualSource + FOW Explored của player
→ TerrainNetworkServerSystem: chunk 8×8, GhostOwner theo connection
→ header + cell/wall buffer Owner, cập nhật 10 Hz
→ TerrainNetworkReceiveSystem validate và publish TerrainView
→ renderer báo trạng thái; handshake chung gửi ACK
```

- Không gửi occupancy/GridNodeCost hoặc resource/unit live state qua terrain.
- Unknown không có visual record; explored khuất giữ terrain tối. Một ô explored không cho phép biết entity mới hoặc resource amount hiện tại.
- Cell mang geometry đã compile; wall ranges thuộc buffer cùng Ghost. Receiver kiểm tra size/range/chunk, duplicate và đủ chunk trước publish.
- Khi thêm chunk cùng phiên, giữ view hoàn chỉnh trước. Đổi session/join/epoch hoặc disconnect thì bỏ view cũ.
- `GameplayGhostRelevancySystem` chạy sau fixed FOW và ECB, trước `GhostSendSystem`. Chỉ Ghost có `CustomGhostRelevancy` tham gia luật custom; Ghost thường giữ delivery mặc định qua `DefaultRelevancyQuery`.
- Cơ chế dùng `SetIsRelevant` cho nhóm custom để Ghost mới chưa có ID không bị lộ snapshot đầu. Owned unit/building luôn được owner nhận; entity khác theo Visible/footprint; terrain và context chỉ đúng người nhận.
- FOW phải sẵn sàng và đúng grid/revision. Explored không dùng để tiếp tục gửi live entity state trong fog.
- GeometryVersion là phiên input renderer của player, không phải match session/map epoch. Chunk visibility chưa được bảo đảm nguyên tử trên toàn map.
- Last-seen building/resource ở multiplayer chưa được nối với despawn relevancy; adapter ký ức hiện tại vẫn chỉ chạy local. Xem [FOW local](../Architecture/FogOfWarRendering.md).

## RPC gameplay

`CommandDataHelper` giữ đường queue cũ cho server/local; ở client gửi `GameplayCommandRpc`. Adapter server lấy issuer từ connection, resolve Ghost ID/spawn tick rồi đưa vào queue/request hiện có.

Đã nối Move, TargetTo, Build, Progression (train/research), PlanBuild và cancel building. Registry objects truyền bằng stable ID/kind; không gửi Entity index, prefab handle hoặc blob pointer. Placement enqueue từ member hợp lệ đầu tiên; member muộn tham gia cùng BuildOrder/site. Quota và rate limit riêng từng connection, không chờ đủ worker.

`GameplayCommandResultRpc.Queued` chỉ xác nhận đã qua gate mạng và vào queue/request; không khẳng định gameplay đã hoàn tất hoặc đã thanh toán thành công. Gameplay cũ vẫn validate tài nguyên, khả năng, target và trạng thái khi xử lý.

## Dự kiến: matchmaking, lobby và vòng đời match

Ghi nhận trao đổi ngày 30/10/2026 để triển khai dần; đây là thiết kế dự kiến, chưa nghiệm thu runtime. Sketch hiện có ở [MatchBootstrap.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Shared/MatchState/MatchBootstrap.cs): `MatchRequestData` và `PlayerMatchDataAfterBanPick`. Chưa coi constructor cấp array là pipeline tạo trận đã hoàn tất.

### Flow và trách nhiệm

```text
Matchmaking ghép đủ người và xác nhận match
    → server tạo dữ liệu match/roster nội bộ
    → chuẩn bị World và kết nối của nhóm người chơi
    → load map → chọn spawn → ban/pick
    → server chốt cấu hình → kiểm tra readiness → bắt đầu gameplay
    → lưu kết quả → dọn dữ liệu/World của trận → trở về lobby
```

Server quyết định danh tính theo connection đã xác thực, kiểm tra spawn/civ và lượt ban/pick; client gửi yêu cầu lựa chọn. Load map và có connection chưa đồng nghĩa trận đã chạy. Tách phase chọn spawn, draft, chờ ready và playing; ánh xạ vào MatchLifecycle hiện có khi triển khai, không tự coi các phase dự kiến là enum đã tồn tại. Chưa chạy di chuyển/chiến đấu/tài nguyên trong bước chuẩn bị.

Mỗi trận có Server World riêng; mỗi người có Client World tương ứng trên process của họ. Có thể tạo Match World trước chọn spawn/ban-pick để dùng map và kết nối suốt quá trình. Khi hết trận, dispose World sau khi lưu kết quả và hoàn tất chuyển connection hoặc disconnect. Process server có thể tiếp tục sống để nhận trận khác.

### Hướng khảo sát: Lobby World → Match World giữ connection

Người dùng đề xuất Lobby World nhẹ chỉ quản lý player/ready, Match World quản lý map và gameplay. Khảo sát `DriverMigrationSystem` để chuyển connection khi cả phòng chuyển sang trận và chuyển về lobby sau trận. Nếu chọn hướng này, migrate sang Match World trước chọn spawn/ban-pick; không tạo lại World giữa các phase đó.

Source package cài hiện tại (`com.unity.netcode` 1.14.3 trong `Packages/manifest.json`) có `DriverMigrationSystem` với `StoreWorld`/`LoadWorld`. Đây mới là xác nhận API từ source, chưa chứng minh pipeline hoạt động. Khi triển khai cần:

- Giữ system điều phối migration ngoài World sắp dispose; store driver/connection trước dispose, load vào World đích trước khởi tạo các system của World đó.
- Chuyển tương ứng phía server/client và có bước đồng bộ chuyển phase, scene-ready, timeout; giữ connection không bảo đảm chuyển World kéo dài tùy ý mà không timeout.
- Dựng lại dữ liệu player/map/gameplay và kiểm tra reference Entity khi đổi World. Migration chỉ chuyển các entity có `NetworkStreamConnection`, không tự chuyển toàn bộ dữ liệu trận.
- Không coi migration là chia một phần connection của Lobby World chung sang nhiều trận, hoặc chuyển sang process/máy server khác. Nếu cần lobby chung và nhiều match đồng thời, phải chốt kiến trúc routing/kết nối trước.

### Dữ liệu match trên server

`MatchRequestData` dự kiến là dữ liệu nội bộ cùng process, được tạo sau matchmaking rồi hoàn thiện dần:

| Thời điểm | Dữ liệu |
|---|---|
| Sau matchmaking | `matchId`, `mapID`, `seed`, `typeOfMatch`, roster `PlayerId` |
| Sau chọn spawn | Chốt `spawnPointId` hợp lệ cho từng người |
| Sau ban/pick | Chốt `civId`; cấu hình hoàn chỉnh dùng để khởi tạo gameplay |

Nếu dùng player struct xuyên suốt quá trình, cân nhắc đổi tên `PlayerMatchDataAfterBanPick` thành `PlayerMatchData` và thêm trạng thái đã chọn/đã khóa; không dùng giá trị mặc định để ngầm hiểu lựa chọn hợp lệ. Chưa đổi code ở lượt ghi chú này.

- `PlayerId` cần ánh xạ với identity server quản lý; mapping slot thử hiện tại chưa phải account identity/reconnect hoàn chỉnh.
- `matchId` ở mỗi player có thể bỏ nếu luôn nằm trong request của một match. `playerCount` nên suy ra từ array length, hoặc ghi rõ đó là số slot dự kiến và validate hai giá trị.
- `int` cho map/mode/seed và `FixedString64Bytes` cho civ đủ dùng ở sketch; validate catalog, dung lượng ID và quy tắc seed theo RNG được chọn. Không gửi seed/full map bí mật xuống client trái với luật terrain/FOW hiện tại; phần map cho chọn spawn cần chốt dữ liệu công khai được phép xem.
- `NativeArray` dùng `Allocator.Persistent` phải có một chủ sở hữu, dispose đúng một lần khi đã copy vào entity hoặc khi hết trận nếu vẫn còn dùng. Copy struct chỉ alias allocation; không phải copy sâu. Xử lý cả hủy matchmaking/draft, lỗi tạo trận và disconnect, không chỉ happy path.
- Không dùng request chứa `NativeArray` trực tiếp như payload gửi sang process khác; nếu sau này tách server thì định nghĩa serialization riêng.

### Các bước triển khai tiếp theo

1. Chốt lobby là một phòng chuyển cả nhóm hay lobby chung chia nhiều trận; chọn migration hay kết nối riêng theo ranh giới process.
2. Hoàn thiện ownership/lifetime của request và trạng thái lựa chọn; validate roster, civ, spawn, số người và ánh xạ connection.
3. Nối dữ liệu đã chốt vào bootstrap player/map hiện tại; thêm gate phase và readiness trước gameplay.
4. Nếu dùng migration, làm thử lobby → match → lobby với server và hai client trước khi tích hợp toàn bộ draft.
5. Kiểm tra nhiều trận liên tiếp/đồng thời theo mô hình đã chọn, giữ identity qua migration, connection timeout/disconnect, hủy draft và dọn allocation/World. Chưa có kết quả kiểm thử cho các bước này.

## Chạy và kiểm tra

Mở [ConnectionUI](../../Assets/NetcodeForEntitiesSetup/Scenes/ConnectionUI.unity), nhập Local profile riêng từng người (ví dụ Host/Alice/Bob), rồi Host hoặc Client. Mở lại client dùng cùng profile để Rejoin; khi trùng profile chọn thêm Player ID. Host render từ ClientWorld; dedicated server không tạo camera/mesh. Mỗi client chỉ mở input sau identity, ACK và MatchState Running.

Kiểm tra hồi quy tái sử dụng: menu `Tools/RTS/Check Multiplayer Networking`. Kết quả kiểm chứng và giới hạn hiện hành nằm trong [GameplayNetworking](GameplayNetworking.md#kiểm-chứng).

Giữ các asset runtime trong Tests/Fixtures: chúng được scene/registry tham chiếu qua GUID, không phải artifact tạm.

Cập nhật 03/11/2026: seed sinh riêng server một lần/trận; giữ slot khi disconnect, chờ roster30giây và deadline tải riêng60giây/connection; vé rejoin lưu theo profile/player. BuildOrder nhận member đầu ngay, member muộn nhập cùng site. Neutral giữ quan sát cuối riêng player, gửi lại khi rejoin; registry resolve theo client world và terrain dựng qua nhiều frame. Chạy `Tools/RTS/Check Networking Protocol`; kết quả và giới hạn ở [GameplayNetworking](GameplayNetworking.md).

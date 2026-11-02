# Multiplayer: terrain hiện tại và ranh giới gameplay

## Phạm vi đã có

Netcode terrain đã nối source/prefab vào Main; unit networking chưa hoàn tất và người dùng phụ trách phần đó. Server authoritative; client nhận dữ liệu rồi render. Presentation hiện vẫn có ECS adapters; chưa chuyển toàn bộ input/UI/camera/render sang MonoBehaviour như mục tiêu.

Chế độ local và client đi chung TerrainView/renderer, xem [Terrain](../Architecture/Terrain.md). Không chạy lại generator ở client hoặc gửi seed/full map bí mật để tiện dựng hình.

## Connection và player

[PlayerConnectionSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Players/PlayerConnectionSystem.cs) cấp PlayerConnection từ roster PlayerBootstrapSlot, lấy NetworkId/SourceConnection do Netcode quản lý. PlayerId không do client tự khai. Đây là cấp slot cho phiên thử, chưa phải account authentication hoặc phục hồi identity khi reconnect.

Tên RPC TerrainJoinRequest/TerrainJoinSent còn giữ để tương thích wire; việc cấp player/session đã tách khỏi module terrain. Client vẫn chờ TerrainSyncSettings của Main/SubScene trước NetworkStreamInGame và join: prefab Ghost phải sẵn sàng trước nhận snapshot. Đây là gate scene-ready hiện có, chưa có framework readiness chung cho mọi stream.

NetworkStreamInGame chỉ bật snapshot, không tự đổi MatchPhase.Running. MatchLifecycle hiện chuyển Preparing → WaitingForPlayers sau bootstrap; code mở trận phải dùng ServerMatch.SetPhase. Ready terrain chưa bằng Ready unit/toàn trận.

## Luồng terrain

```text
TerrainVisualSource + FOW Explored của PlayerConnection
    → TerrainNetworkServerSystem: nhóm chunk 8×8, GhostOwner của connection
    → header + cell buffer + wall buffer SendToOwner, cập nhật 10 Hz
    → TerrainNetworkReceiveSystem: owner/session/epoch/size/range/đủ chunk
    → TerrainView → renderer chung
    → TerrainReadyRequest sau dựng hình thành công
    → TerrainReadyServerSystem xác minh và ghi TerrainClientReady
```

Nguồn: [TerrainNetworkServerSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainNetworkServerSystem.cs), [TerrainNetworkReceiveSystem.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainNetworkReceiveSystem.cs), [TerrainChunkGhostAuthoring.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainChunkGhostAuthoring.cs), [TerrainCellVisualData.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainCellVisualData.cs).

[TerrainChunkGhost prefab](../../Assets/_Project/Prefabs/TerrainChunkGhost.prefab) đã được Main/EntitySubscene tham chiếu. Server chờ PlayerBootstrapState.Ready để có player/vision/spawn camera, không chờ Running để gửi terrain.

## Invariant nhận/gửi

- GhostOwner/SendToOwner xác định người nhận; **server vẫn phải lọc Explored**. Không gửi occupancy, GridNodeCost hoặc resource/unit state qua luồng terrain. Client khác có thể thấy Ghost entity rỗng nhưng không có owner payload.
- Unknown không có visual record, mask bằng 0. Explored khuất giữ geometry tối; entity mới trong ô Explored không tự được biết.
- Cell có CornerHeights/TopHeight/mountain/walls đã compile; không cần client biết hàng xóm để dựng mặt. WallStart/WallCount phải nằm trong buffer cùng Ghost, cell phải thuộc chunk và không trùng.
- Publish view khi đủ bộ chunk hợp lệ; khi cùng session/epoch đang nhận thêm chunk thì giữ view hoàn chỉnh trước. Session/epoch mới hoặc disconnect phải bỏ view cũ.
- Session thuộc connection assignment; epoch thuộc source visual compile; MapBuildState.Revision/hash thuộc map; GeometryVersion thuộc mesh input của player. Không gộp counter hoặc thêm WorldId/MapId vào frame FOW.
- Ready ACK phải khớp SourceConnection, session và epoch hiện tại. Payload shape và owner filtering không thay bằng client-side hiding.
- Chunk có thể mang visibility từ các tick khác nhau; chưa có cam kết snapshot vision nguyên tử toàn map. Theme settings phải khớp; chưa hash toàn texture/catalog.

## Unit và command networking chưa hoàn tất

Giữ payload backend hiện có: CommandType, GameBlobReference, position/target và request riêng như CancelBuildingRequest. RPC adapter lấy issuer từ connection, resolve Ghost identity sang Entity của server rồi validate trước dispatch. Không serialize Entity handle local hoặc raw blob pointer. SourcesEntity của PlanBuild là entity payload local; mạng cần truyền/khôi phục danh sách worker, không chỉ gửi handle đó.

Netcode xử lý replication/interpolation/reliability của transport. Chỉ thêm sequence/ACK ứng dụng nếu có yêu cầu gameplay được xác định và ca kiểm chứng; không dựng cơ chế trùng vì một kế hoạch cũ từng đề cập.

Unit/projectile khuất không có hình nhớ; building/resource cần last-seen memory độc lập Ghost. Client despawn do relevancy không đồng nghĩa chết và không tự chạy death animation. Identity ổn định và chính sách reconnect/late join cho entity memory còn phải triển khai. [FOW](../Architecture/FogOfWarRendering.md) giữ luật thông tin.

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

Mở [ConnectionUI](../../Assets/NetcodeForEntitiesSetup/Scenes/ConnectionUI.unity) rồi Main theo bootstrap hiện có. Frontend và Main có thể cùng camera; TerrainMapRenderer phải chọn camera trong scene Main, không chọn nhầm frontend. Nó đăng ký SubScene cho Network Worlds tạo sau khi Main đã mở và đổi binding khi World đổi. Host render qua ClientWorld; dedicated server không tạo mesh/camera.

Kiểm tra server + hai client: mapping player khác nhau; thiếu slot; prefab/scene load trước go-in-game; chỉ owner nhận terrain; exploration/vision đổi; chunk thiếu/sai range; Ready sai/đúng session; disconnect/reconnect và đổi map; camera snap chỉ khi sẵn sàng. Sau đó mới nghiệm thu mọi gameplay command, relevancy/static memory/UI và tải lớn. [CurrentState](../Maintenance/CurrentState.md) ghi phần nào đã thực sự chạy.

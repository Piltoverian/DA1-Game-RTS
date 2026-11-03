# Flow gameplay multiplayer

Mục đích: giữ component/queue/gameplay cũ, thêm identity, ownership, lọc FOW và RPC để client chỉ nhận dữ liệu được phép và gửi ý định về server. Source cập nhật ngày 03/11/2026; chưa commit.

## Điểm bắt đầu để đọc code

| File | Trách nhiệm |
|---|---|
| [PlayerConnectionSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Players/PlayerConnectionSystem.cs) | RPC/types chung, cấp identity, helper gửi RPC và truy vấn identity local |
| [NetworkGameplayBootstrapSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Players/NetworkGameplayBootstrapSystem.cs) | Materialize context seed; server tạo player/match Ghost, local tạo context thường |
| [PlayerGhostOwnershipSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Players/PlayerGhostOwnershipSystem.cs) | Gán GhostOwner từ connection/player, gồm entity spawn sau và reset owner khi disconnect |
| [GameplayGhostRelevancySystem](../../Assets/_Project/Scripts/Runtime/Simulation/Players/GameplayGhostRelevancySystem.cs) | Lọc nhóm Ghost có quyền nhận custom theo owner/FOW |
| [PlayerHandshakeServerSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Players/PlayerHandshakeServerSystem.cs) | Chốt danh sách Ghost ban đầu và validate ACK |
| [PlayerHandshakeClientSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Players/PlayerHandshakeClientSystem.cs) | Nhận identity/manifest, tổng hợp readiness rồi gửi một ACK |
| [GameplayCommandRpcSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Players/GameplayCommandRpcSystem.cs) | Adapter lệnh client/server và receipt Queued/rejected |
| [NetworkClientWorldSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Players/NetworkClientWorldSystem.cs) | Gắn grid/FOW interaction từ terrain đã được phép nhận |
| [MatchLifecycleSystem](../../Assets/_Project/Scripts/Runtime/Simulation/Shared/MatchState/MatchlLifeCycleSystem.cs) | Chờ đủ roster ready rồi đổi phase qua ServerMatch.SetPhase |

## 1. Baking và tạo state

Giữ load all civ/unit/building như hiện tại. PlayerBootstrapAuthoring bake NetworkGameplayPrefabs cùng roster/context seed; standalone PlayerContextAuthoring cũng bake seed. Hai prefab mới trong Resources/Networking được tham chiếu khi bake để có trong Ghost Collection.

ServerMapSeedSystem tạo seed thực tế riêng một lần lúc server khởi tạo map, thay seed cấu hình trước khi sinh terrain/resource. Không bake hoặc replicate seed thực tế. Pure-client bake bỏ recipe và cấu hình placement; combined client world có guard loại authority component. Local offline vẫn dùng seed cấu hình để tái lập. VisualSeed chỉ chọn artwork.

Server instantiate PlayerContextGhost cho từng seed và cập nhật PlayerBootstrapSlot.Context sang instance. ResourcePair/tech/cache được khởi tạo trên instance đó. Pure-client bake không giữ context/bootstrap seed. Combined Editor/host client world loại authority data và không tự materialize context trùng. Local simulation materialize entity thường và không bắt buộc có network prefab.

MatchGhost chứa MatchSession, MatchState, MatchTimeComponent và roster công khai. Server không tạo thêm state/time singleton thường. Client chỉ đọc instance nhận mạng; ServerMatch cache chỉ phục vụ authority, không phải nguồn UI client.

## 2. Join và mapping

```text
NetworkId → chờ catalog và prefab local sẵn sàng
→ bật NetworkStreamInGame + gửi PlayerJoinRequest
→ ReceiveRpcCommandRequest.SourceConnection
→ server xác thực vé rejoin hoặc chọn slot chưa từng có chủ
→ PlayerConnection gắn trên đúng connection entity
→ PlayerJoinAcceptedRpc trả identity về client
```

`PlayerClientPrefabReadiness` kiểm tra catalog, prefab Match/PlayerContext/Terrain, RegistryPrefabElement và MapResourceGhostPrefabReference. Không bật InGame khi SubScene còn tải: server đã chạy có thể gửi bảng ghost ngay snapshot đầu, khiến client thiếu prefab bị NetCode ngắt. Client có deadline local60giây từ lúc có NetworkId; quá hạn báo LocalPrefabsTimedOut trong UI và ngắt, không che prefab thiếu thật bằng LoadingActive vô hạn. Deadline này độc lập deadline server sau admission.

MatchSessionId chung cho trận, JoinGeneration riêng lượt join, NetworkId do NetCode cấp, PlayerId là slot gameplay. Không lấy PlayerId client tự khai để làm authority. PersistentPlayerSlot tồn tại độc lập với connection; slot Claimed không được cấp lại cho fresh join sau disconnect. Terrain header Session hiện mang MatchSessionId và có JoinGeneration riêng.

PlayerGhostOwnershipSystem chạy trước relevancy/send, đọc PlayerConnection + NetworkId. Context dùng PlayerContext.PlayerId; unit/building dùng EntityOwner.PlayerID. GhostOwner phải có trên prefab từ baking; hall/worker/soldier đã bật Has Owner. Entity spawn sau tự được gán theo cùng mapping.

## 3. Relevancy

```text
Fixed tick: movement/spatial → FOW
→ EndSimulation ECB
→ ownership
→ custom relevancy
→ bootstrap manifest/ACK handling
→ GhostSendSystem
```

Chỉ các Ghost có CustomGhostRelevancy tham gia luật custom. Ghost không có tag giữ delivery mặc định qua DefaultRelevancyQuery. Với nhóm custom:

- Terrain: đúng GhostOwner.NetworkId.
- Player context: đúng PlayerId.
- Unit/building của mình: luôn được nhận.
- Unit/building khác, resource và projectile: FOW Visible cho player; một ô footprint visible đủ thấy entity, như luật local hiện có.
- FOW chưa ready hoặc sai grid/revision: không cho phép entity bên ngoài quyền owner.

Dùng SetIsRelevant cho nhóm custom vì GhostSendSystem cấp ID mới trong bước gửi: nếu dùng danh sách chặn, ID chưa biết có thể lọt snapshot đầu. Ghost thường không cần thêm từng cặp vào set.

Relevancy xét cả entity; OwnerSendType xét component bên trong. Explored chỉ phục vụ terrain/memory, không cho phép gửi trạng thái sống mới của resource/địch đã khuất.

## 4. Một ACK sau khi nhận và gắn xong

Server chốt danh sách Ghost ID + spawn tick của các entity initially relevant, context riêng và Match Ghost. Danh sách không dựa vào số lượng hall/worker suy đoán. Các loại RPC có thể tới khác thứ tự; client giữ manifest của phiên mới đến trước identity và bỏ tuple cũ.

Client chỉ gửi PlayerReadyRequest khi:

1. Identity hợp lệ và manifest/summary cùng tuple.
2. Mỗi Ghost ID/spawn tick đã nhận ít nhất một lần hoặc được server rút khỏi yêu cầu vì despawn/mất quyền nhìn. Context và Match Ghost vẫn phải hiện hữu và đúng identity khi ACK.
3. Context có PlayerId, PlayerContextSession và GhostOwner khớp identity.
4. MatchSession nhận mạng khớp trận và registry hiện có.
5. Terrain đủ chunk hợp lệ, renderer/fog/camera binding đã dựng ít nhất GeometryVersion ban đầu được chốt. Exploration mới không làm lùi cutoff và chặn ACK liên tục.
6. Physics/selection infrastructure, catalog của chính client world và snapshot neutral memory đã sẵn sàng.

Server validate SourceConnection, MatchSessionId, JoinGeneration, Epoch và trạng thái đã gửi manifest; trả PlayerReadyAcceptedRpc. MatchLifecycle bắt đầu ngay khi đủ roster ready; nếu chưa đủ, chờ JoinTimeoutSeconds (mặc định 30 giây từ WaitingForPlayers), giữ slot vắng ở trạng thái Disconnected rồi chuyển Running. Connection đã được nhận có deadline tải riêng mặc định 60 giây theo wall clock; hết hạn tải mới bị ngắt và vẫn giữ slot/context. Rejoin trong Running cũng có deadline riêng và không pause trận. Client mở input khi đã được chấp nhận và nhận MatchState Running.

Manifest gửi tối đa128 message/connection/update; client tra Ghost bằng hash lookup. RPC gameplay đã giao được xử lý FIFO tối đa4096/connection/update, phần chưa xử lý giữ lại. Backlog vượt8192 được trả kết quả từ chối tổng hợp rõ ràng và ngắt connection; không bỏ âm thầm các lệnh vượt budget frame.

## Neutral memory và presentation khi tải chậm

`NeutralMemoryServerSystem` giữ quan sát cuối riêng từng PlayerId, độc lập connection. Resource, neutral unit/building và prop gameplay được ghi khi đang visible; thay đổi số lượng, di chuyển, chết hoặc đổi chủ trong vùng khuất không cập nhật trí nhớ. Khi player nhìn lại, server cập nhật trạng thái mới hoặc tombstone nếu footprint cũ đã thấy mà đối tượng không còn ở đó. Không lưu Entity server hoặc thông tin production riêng của owner trong payload.

Rejoin nhận snapshot riêng connection, có cutoff revision cố định, index và completion; delta mới có revision để không bị page cũ ghi đè. Tổng budget32 RPC/connection/frame, dành tối đa8 cho delta trong lúc tải snapshot. Page/completion đến trước header được giữ lại. ObservedTick bảo vệ quan sát live mới hơn trước page RPC bị trễ.

Client dựng proxy hình ảnh từ prefab local; proxy không có collider hoặc simulation. Ghost live thay thế hình nhớ khi hiện diện. Lệnh tới resource đã nhớ chỉ gửi NeutralId; server kiểm tra trí nhớ của đúng player rồi cho worker tới vị trí đã biết, chỉ resolve đối tượng thật khi nhìn thấy. Disconnect không xóa trí nhớ; server trận kết thúc mới mất dữ liệu này.

Catalog là reference được bake trên registry local và resolve theo client world đang sống. Đổi world hủy preview và bỏ cache entity/selection cũ. Terrain dựng candidate qua nhiều frame, giữ renderer đã publish tới khi candidate hoàn tất, dùng chung atlas/material cùng theme/grid, retry lỗi tạm. FOW/source tạm thiếu không xóa mask đang được renderer sử dụng. Lần pack/upload atlas đầu tiên vẫn chạy đồng bộ; đây chưa phải cam kết thời gian frame tối đa.

NetworkStreamInGame bật trước ACK để nhận snapshot. Server bootstrap và FOW cũng chạy trước Running, tránh hai bên chờ nhau.

## 5. RPC gameplay

CommandDataHelper giữ đường queue cho server/local; client gọi adapter RPC. Source/target là Ghost ID + spawn tick. Job/building dùng stable ID/kind từ registry. Server resolve thành Entity/prefab của mình rồi gọi lại queue/request cũ.

- Move, TargetTo, Build: dùng source/target resolve trên server; target phải được player phép nhận.
- Progression: server dựng GameBlobReference từ job ID/kind và gọi production cũ.
- PlanBuild: member hợp lệ đầu tiên enqueue ngay một NetworkBuildOrder. Member sau dùng cùng order: tiếp cận chung khi chưa visible, nhập vào request đang đặt hoặc enqueue Build vào Site đã tạo. Không chờ MemberCount, không tạo nhà/thanh toán lần hai. Terminal fail/complete/despawn không được tự tạo lại.
- Order reference đi qua CommandQueue, BuilderComponent/BuilderQueueElement và PlaceBuildingRequest; BuildingPlacementSystem publish thành công/thất bại trước khi xóa request. Worker bỏ kế hoạch hoặc có lệnh mới hơn không bị member cũ kéo trở lại.
- Budget riêng connection: 4 lệnh mới/giây, burst8; 2048 member/giây, burst8192; tối đa4 nhóm chưa đủ member trong cửa sổ10giây và64 order Planning/Placing. Mỗi group tối đa4096 member. Vượt giới hạn trả RateLimited. Lịch sử dedup256 request/connection; request cũ đã retire không thể tạo lại. Hết cửa sổ nhận member không hủy việc xây đang chạy hoặc plan đã được worker nhận.
- Cancel: resolve building của player rồi tạo CancelBuildingRequest trên server.
- Server kiểm tra phiên, ready tuple, current terrain epoch, ownership, health và dữ liệu hữu hạn trước enqueue. Gameplay cũ tiếp tục validate chi phí/capability/state.
- GameplayCommandResultRpc.Queued chỉ là xác nhận đã vào queue/request, không phải hoàn tất hành động hoặc thanh toán thành công.

Selected và vòng chọn vẫn local, không thêm GhostEnabledBit. UI resource/pop nhận context Ghost; cache/event bridge chạy client/local. Construction visual được tính lại từ progress nhận mạng; client không tự tăng tiến độ.

## Disconnect và rejoin sau tắt/mở client

Server giữ context, EntityOwner, tài nguyên, unit/building và các lệnh đang chạy. Disconnect chỉ tháo transport/GhostOwner và stream terrain riêng connection. Map/MatchSession không tạo lại.

Vé resume chứa 256 bit ngẫu nhiên, chỉ gửi RPC về connection đó; server giữ SHA256 digest. Client lưu receipt trong Application.persistentDataPath/NetworkResume, tách endpoint + match + PlayerId. Không log/replicate vé trong Ghost. UI yêu cầu Local profile; lần mở lại nhập cùng profile và bấm Rejoin. Nếu một profile có nhiều player, phải chọn Player ID; không tự lấy vé của player khác. Profile/PlayerId chỉ chọn receipt local, bằng chứng server kiểm tra vẫn là vé.

Resume hợp lệ gắn connection mới vào cùng slot, tăng JoinGeneration, ngắt connection cũ và bỏ ACK cũ. UI tạo lại riêng client world do nó sở hữu, giữ server/sibling client worlds. Client nhận context, match, explored terrain và gameplay Ghost đang được phép, dựng renderer/physics/selection rồi ACK. Lệnh mang join cũ bị từ chối.

## Kiểm chứng

Unity6000.3.13f1 compile gồm NetCode serializers/IL post-processing. Menu `Tools/RTS/Check Networking Protocol` gom:

- MultiplayerNetworkingChecks: 37 assertions về identity, ownership, visibility, bootstrap và command authority.
- SeedNetworkingChecks: seed chỉ tạo trên server, không đổi khi update/rejoin, late resource settings, offline deterministic và client stripping.
- RejoinNetworkingChecks: 45 assertions về vé, giữ slot/disconnect/takeover, timeout, mutable manifest, lưu file/profile và chỉ disconnect đúng UI-owned client.
- RejoinWorldLifetimeChecks: 8 assertions giữ ServerWorld khi thay client world.
- ProtocolTimingChecks: 29 assertions về deadline riêng, backlog, manifest budget, validation lệnh neutral và cold-client prefab gate (chưa tải catalog/prefab → chưa Join/InGame; tải đủ → đúng một Join; thiếu thật → timeout rõ ràng).
- WorldCatalogChecks: 14 assertions về catalog và selection qua các world generation.
- NeutralMemoryChecks: 216 assertions về quan sát riêng từng player, hidden change/death/conversion, revision, paging budget, privacy, rejoin và page tới trước header.
- TerrainStagingChecks: 18 assertions về staging, ordering, chia batch, chia sẻ art, fog mask và hủy candidate.
- IncrementalBuildChecks: enqueue member đầu, late member cùng order, dedup, supersession, terminal state và quota riêng connection/ECS active-order bound.
- NetworkBuildPlacementChecks: 18 assertions dùng queue/builder/placement thực, hidden→visible tạo một site và trừ tiền một lần, late member cùng site, fail/complete/destroy không tái tạo, supersession và bỏ approach không phát sinh thanh toán muộn.

Regression đã quan sát fail khi tái lập barrier chờ đủ member, rồi pass khi bỏ barrier. Bộ tích hợp cuối đã pass trong Unity batch và thoát mã0 với `--burst-force-sync-compilation`. Lần trước các assertions đã pass nhưng background Burst compiler crash lúc shutdown; chạy đồng bộ đã kết thúc sạch. Không đổi cấu hình Burst của project.

Kiểm tra lại ngày03/11/2026 sau hardening: cả10 nhóm trong NetworkingProtocolChecks pass, compile/NetCode generation thành công và Unity thoát mã0. Test lifecycle dọn singleton NetCode do chính fixture khởi tạo; lần cuối không còn cảnh báo native allocation leak. Kiểm tra whitespace source/docs pass; các dòng trống có dấu cách trong YAML prefab do Unity serialize không được sửa kèm.

## Giới hạn còn lại

- Chưa chạy lại smoke hai client qua transport và thao tác tắt/mở process với continuation này. Smoke hai client trước continuation đã qua move/train/place/cancel và FOW, không được coi là bằng chứng cho rejoin mới.
- Chưa build/inspect toàn bộ player client. Project hiện hỗ trợ ClientAndServer cho host; pure-client baking omission chỉ áp dụng khi chọn genuine NetCode Client target. Actual multiplayer seed vẫn sinh riêng runtime server, không có trong baked asset. Host sở hữu server nên có thể đọc authority memory của chính host.
- Transport hiện chưa mã hóa; RPC gửi riêng connection không đồng nghĩa mã hóa vé. Receipt local nằm trong vùng dữ liệu ứng dụng, chưa gắn account/lobby identity. Rejoin yêu cầu server trận còn sống; đóng host/server không có server persistence để khôi phục.
- Gather/attack/research và proxy neutral mới chưa có smoke end-to-end tương đương move/train/place/cancel trên source continuation này. Neutral memory đã có kiểm tra ECS, chưa nghiệm thu hình ảnh/GPU trong hai process thật.
- Batch không chứng minh GPU/UI/camera/HUD, mạng trễ/mất gói hoặc tải nhiều nghìn unit. Các rate-limit ban đầu cần đo và điều chỉnh theo tải thực tế.

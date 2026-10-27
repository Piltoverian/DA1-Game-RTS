# Terrain local và Netcode

Cập nhật 20/10/2026. Pipeline source đã nối trong Main; kiểm chứng và giới hạn ở cuối trang. Unit networking thuộc phần người dùng thực hiện. Không tự chuyển MatchState sang Running.

## Luồng dữ liệu

    GridAuthoring → GridTerrain + TerrainSyncSettings
      → TerrainVisualSourceSystem (local hoặc server)
      → compiler tính mask/độ cao/vách/núi từ map đầy đủ, cache theo revision/hash
      ├─ local: lọc các ô Explored của viewer → renderer chung
      └─ server: connection → player slot → lọc Explored của player
           → các TerrainChunkGhost riêng cho connection, chia vùng 8×8
           → Netcode gửi header + buffer ô + buffer vách chỉ cho GhostOwner
           → client kiểm tra owner/session/epoch, số chunk và range vách
           → giải mã ô + texture FOW → renderer chung
           → TerrainReadyRequest sau khi dựng hình thành công
           → server ghi TerrainClientReady trên connection

Terrain được gửi sau PlayerBootstrapState.Ready, trước MatchPhase.Running. Bootstrap phải hoàn tất để có player, nguồn vision và vị trí camera. NetworkStreamInGame chỉ bật luồng snapshot, không bắt đầu gameplay.

## Nguồn code

| File | Trách nhiệm |
|---|---|
| [GridAuthoring.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Grid/GridAuthoring.cs) | Bake prefab và thông số hình terrain. Main dùng LunarTheme giống renderer. |
| [TerrainVisualSourceSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainVisualSourceSystem.cs) | Cache map đã biên dịch; server không cần camera/mesh/material. Epoch tăng khi biên dịch lại. |
| [TerrainCellVisualData.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainCellVisualData.cs) | Compiler dùng chung, object render và hai buffer Ghost; WallStart/WallCount trỏ các vách trong cùng Ghost. |
| [TerrainChunkGhostAuthoring.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainChunkGhostAuthoring.cs) | Header Ghost và Baker cho hai buffer. |
| [TerrainNetworkServerSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainNetworkServerSystem.cs) | Join, cấp slot trống, gửi theo player ở 10 Hz, xác minh Ready và cleanup. |
| [TerrainNetworkReceiveSystem.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainNetworkReceiveSystem.cs) | Ghép chunk của owner, giữ view trước khi nhận đủ chunk mới, tạo mask và reset khi đổi connection/map. |
| [MapTerrainPresentationSystem.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/MapTerrainPresentationSystem.cs) | Chọn nguồn local/network, dùng cùng renderer và báo Ready sau dựng hình. |
| [TerrainMapRenderer.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainMapRenderer.cs) | Ưu tiên ClientWorld, nếu không có dùng local; đổi binding khi World thay đổi. |

Prefab [TerrainChunkGhost](../../Assets/_Project/Prefabs/TerrainChunkGhost.prefab) đã được tham chiếu trong [Main/EntitySubscene](../../Assets/_Project/Scenes/Main/EntitySubscene.unity) để baking đăng ký hai phía. Không cần thêm prefab thủ công khi chạy Main. TerrainMapRenderer yêu cầu load các SubScene tự động của Main vào Netcode Worlds được tạo sau khi Main đã mở, đồng thời tránh tạo yêu cầu trùng nếu scene đã đăng ký.

## Quyền nhận và FOW

Server lấy NetworkId từ connection do Netcode quản lý, không nhận PlayerId do client đề nghị. TerrainPlayerConnection lưu player được cấp từ roster PlayerBootstrapSlot. Đây là cấp slot cho thử local, chưa phải đăng nhập tài khoản hoặc khôi phục danh tính qua reconnect.

Mỗi connection sở hữu tập chunk riêng. Header và cả hai buffer có OwnerSendType = SendToOwner. GhostOwner chỉ xác định người nhận, không tự lọc FOW: bộ gửi chỉ thêm các index Explored của player. Không gửi occupancy, GridNodeCost, resource hoặc unit qua luồng này. Client khác có thể thấy entity Ghost rỗng nhưng không nhận header/nội dung buffer.

Ô Explored được giữ khi mất vision. Visible trong record điều khiển mask trắng hoặc 80/255. Unknown không có record, mask bằng 0. Chỉ đổi visibility không dựng lại mesh. Thêm ô hiện vẫn rebuild tập hình terrain đã biết; chưa cập nhật mesh tăng dần theo chunk.

Compiler đọc map đầy đủ, renderer đọc gói ô đã lọc. Vách và hình núi không phụ thuộc vào số hàng xóm client đã biết. Client cần cùng theme asset; kiểm tra thông số hình học/visual seed phát hiện cấu hình khác, chưa kiểm tra hash toàn bộ texture/catalog.

## Tải thiếu và vòng đời

- Client publish view khi nhận đủ số chunk và range ô/vách hợp lệ. Trong lúc nhận thêm chunk, giữ view đầy đủ trước đó.
- Chunk có thể thuộc các tick visibility khác nhau; không cam kết snapshot vision nguyên tử toàn map.
- TerrainClientReady gồm Session/Epoch. Server lấy SourceConnection và đối chiếu player session, epoch hiện tại; ACK sai phiên bị bỏ. Ready terrain chưa đại diện cho Ready unit hoặc toàn trận.
- Disconnect xóa hình/cache client và chunk trên server. Connection mới được cấp slot trống; nếu nhận cùng slot thì lịch sử Explored vẫn thuộc player slot đó trên server.
- Session/Epoch là metadata vận chuyển, không đưa WorldId/MapId trở lại frame FOW ứng dụng.
- Host render qua ClientWorld. Dedicated server không tạo mesh/camera. Local offline dùng compiler/renderer chung.

## Test Main trong Play Mode

1. Offline: mở Main với Netcode bootstrap tắt hoặc dùng LocalSimulation; kiểm tra terrain, camera, FOW như trước.
2. Host và client: dùng kết nối hiện có, bảo đảm hai phía load Main; chọn đúng Play Mode Type. Luồng terrain không phụ thuộc CubeSpawner của scene ví dụ.
3. ServerWorld: connection có TerrainPlayerConnection; sau bootstrap có TerrainChunkGhost với GhostOwner khớp NetworkId. ClientWorld chỉ giải mã chunk của owner local.
4. Binding phải có MapPresentationState.Ready; server connection nhận ACK phải có TerrainClientReady. Nếu lỗi, đọc MapPresentationState.Error; thiếu dữ liệu giữ WaitingTerrain.
5. Hai client khác player: cùng tọa độ đã khám phá có cùng hình, vùng được phép biết có thể khác. Player cùng team chia sẻ vision/map có thể nhận tập ô giống nhau theo luật FOW.
6. Di chuyển/thay đổi nguồn vision ở server: vùng mới xuất hiện, vùng khuất giữ terrain tối, núi/vách cũ không đổi theo việc biết thêm hàng xóm. Unit networking chưa được thêm nên client chưa thấy unit là dự kiến.
7. Disconnect/reconnect: hình cũ phải bị dọn; luồng mới dựng từ dữ liệu session mới.

Khi đi từ scene ConnectionUI vào Main, ConnectionUI chờ scene load, đặt Main làm active scene, tắt camera/Canvas/EventSystem/AudioListener/light của frontend và trỏ World mặc định sang ClientWorld. Controller kết nối còn sống để giữ reference trạng thái; không unload World mạng. Renderer và camera snap chọn MainCamera trong chính scene Main, không chọn Camera.main từ frontend.

## Kiểm chứng và giới hạn

- Unity 6000.3.13f1 batch compile và tạo serializer terrain/RPC thành công; chưa chứng minh truyền snapshot thực tế.
- Fixture ECS ngoài Play Mode: 32 assertion pass trên source cuối ngày 20/10. Bao phủ cache/revision local, cấp player, full roster, phân chia dữ liệu, parity local/network, chờ thiếu chunk, owner filter, mask, visibility không rebuild, range lỗi, ACK Ready sai/đúng phiên và cleanup.
- Fixture/log dùng một lần được dọn sau kiểm tra; không khôi phục bộ test Editor cũ.
- Chưa nghiệm thu host + hai client qua transport thật hoặc hình ảnh Play Mode. Người dùng thực hiện bước này.
- Chưa benchmark map 512×512/nhiều player; không suy ra hiệu năng/băng thông từ compile hoặc fixture.
- Xác nhận mới nhất của người dùng: terrain trong vùng nhìn thấy render đúng; vùng unknown đang lộ skybox. Nhánh client đã được bổ sung SolidColor/Color.black như local. Chưa xác nhận Play Mode sau sửa nền; không diễn giải lại triệu chứng thành mất toàn bộ terrain. Các sửa chuyển frontend/camera trước đó vẫn được giữ, nhưng chưa có bằng chứng chúng là nguyên nhân của skybox unknown.

# Fog of War — bàn giao local và chuẩn bị multiplayer, 17/10/2026

## 1. Mục tiêu và phạm vi

Người dùng đã chốt dừng bước local để chuyển sang chuẩn bị multiplayer. Render FOW local đã nối trong Main; chưa có Netcode/transport. Đây là nguồn chính cho trạng thái, bằng chứng và hành động tiếp theo. Chi tiết runtime ở [kiến trúc render](../Architecture/FogOfWarRendering.md); hướng multiplayer ở [Multiplayer](../Multiplayer/README.md).

Yêu cầu mới: client presentation/input/UI/camera ưu tiên MonoBehaviour, dùng adapter nhận dữ liệu từ ECS/Netcode. Đây là hướng đã yêu cầu, **chưa refactor**: LocalFogPresentationSystem, MapTerrainPresentationSystem và LocalBaseCameraSnapSystem vẫn tồn tại. Không coi client full Mono là đã hoàn tất.

## 2. Thay đổi và quyết định

### Runtime local hiện có

- [LocalFogPresentationSystem](../../Assets/_Project/Scripts/Runtime/Presentation/Components/LocalFogPresentationSystem.cs) chạy Presentation trước map/EntitiesGraphics, hoàn tất jobs rồi Collect → Apply. Viewer lấy từ LocalTestPlayer hoặc nguồn SelectHelper hiện hữu.
- Entity đang visible dùng renderer gốc. Khi khuất, dùng DisableRendering cho phần mesh; SpriteRenderer được ẩn/khôi phục riêng, đối chiếu trạng thái chọn/minimap. Không disable entity simulation.
- Chỉ building/resource giữ ký ức. Capture mesh reference, material copy, LocalToWorld của hình con và RevealHeight khi còn thấy; khi khuất dùng Graphics.DrawMesh với dữ liệu đóng băng. Không clone component gameplay; không đọc entity đã chết để dựng lại hình. Unit/đạn không có ký ức. Không có nhóm vật cản entity tĩnh khác; vật cản còn lại là terrain.
- Root/hình con được nối bằng LinkedEntityGroup và truy Parent. Khôi phục các trạng thái render adapter đã can thiệp và dọn material/frame/texture khi đóng adapter. Adapter local có ScopeVersion nội bộ để map presentation phát hiện đổi viewer/grid; không phải ID gửi mạng.
- [FogEntityRenderData](../../Assets/_Project/Scripts/Runtime/Simulation/FogOfWar/FogEntityRenderData.cs) giữ Entity runtime; GameRegId là FixedString64Bytes nullable, không phải int. Có blob unit/building trong registry thì dùng ID; không có thì SourceEntity là entity runtime local để capture lúc visible, **không phải một prefab Entity đã bảo đảm sống qua fog**.
- Health có Current/MaxOverride; Resource chỉ Amount; ActiveAbility chỉ ability đang thực thi và ActiveJob chỉ phần tử production đầu queue. Không gửi danh sách khả năng/queue. Đây là schema local, chưa phải Ghost schema hoặc codec mạng. Không khôi phục VisualId/identity baker cũ.
- Collect trả **full tập terrain Explored mỗi lần**, gồm lịch sử chia sẻ từ đồng đội; không gửi ô chưa khám phá hoặc occupancy động. Tham số knownTerrain còn giữ để tương thích caller nhưng không lọc tile nữa. Đạn không mở fog/terrain.
- Đã bỏ WorldId, MapId, MapRevision khỏi frame và điều kiện reset view/cache. Mỗi trận có một map bất biến; owner consumer phải Clear khi đổi trận, kể cả grid geometry trùng nhau. Revision nội bộ map generation chưa bị xóa.
- Frame có TerrainBaseLevel: mốc thấp nhất toàn map dùng chuẩn hóa height, là metadata hình học, không phải revision. Renderer điền mốc này vào placeholder unknown, không dựng sprite cho ô unknown. Trước sửa, unknown=0 làm terrain FOW cao hơn unit một tầng trong Main.
- Tầm nhìn hình tròn theo ô, không occlusion; chỉ cần một ô footprint visible là nhận toàn entity. Team mode giữ luật đã chốt: người nhận Team nhập vision/history của đồng đội, không tự đổi mode của đồng đội.

### Selection cuối lượt

[SelectManager](../../Assets/_Project/Scripts/Runtime/Presentation/Input/GameManager/Manager/SelectManager.cs) chỉ gửi Drag khi `holdbuffer > 0.05f && size.x > Screen.width / 40f`. Đây là AND giữa thời gian và **chiều rộng**, không còn ngưỡng diện tích 64 px². Click vẫn đi SelectionRequest → SelectSystem. Bản điều kiện cuối chỉ build pass, chưa thao tác Play Mode lại; người dùng chọn không kiểm tra thêm trước bàn giao. Giữ CancelBuildingRequest riêng và FIFO chỉ của build; không tự gộp mọi command/request.

### Vị trí khôi phục

Workspace `D:/Unity/TechTextAndChosingForRTS sample`, branch `Fog-of-wars`, HEAD khảo sát `9f8ea41`. Thay đổi còn trong working tree, có nhiều untracked gồm FOW source/docs. Chưa commit/push, không có patch/snapshot riêng. Checkout HEAD không khôi phục các file mới; chuyển workspace phải mang cả tracked diff và untracked. Giữ thay đổi Main/fixture/gameplay có từ trước, không quy toàn bộ diff cho lượt FOW.

## 3. Bằng chứng kiểm chứng

Các kết quả dưới đây là từ các lượt code trong hội thoại ngày 17/10, **không chạy lại trong lượt cập nhật docs**:

| Kiểm tra | Kết quả và giới hạn |
|---|---|
| Snapshot tùy chọn và terrain full | 21 assertion ECS pass, Unity 6000.3.13f1, exit 0; trước bỏ ba field World/Map ID/revision. |
| Adapter render + Main Play Mode | 33 assertion pass, Unity có graphics, Burst tắt riêng khi chạy, exit 0. Unit/đạn ẩn, building/resource giữ hình sau destroy và xóa khi nhìn lại; sprite restore/disposal; mask và Main terrain nhận đúng cache. |
| Main khi còn vision / mất toàn bộ vision | Main có 14 entity visible, 377/65.536 ô Explored. Runtime test bỏ vision: unit mất, ký ức tĩnh còn, terrain tối; đã xem ảnh camera URP thực. Lượt này còn lỗi height được phát hiện sau đó, không dùng ảnh cũ để nghiệm thu height. |
| Height sau sửa TerrainBaseLevel | Chạy trực tiếp Main trong Editor đang mở: 5 unit trước sửa lệch 1,59472; sau sửa cả unit/cache/mặt terrain đều Y=3,18944, sai lệch 0. Đã xem ảnh mới thấy chân unit/nhà đúng mặt đất. Không phải kiểm thử toàn bộ ramp/movement dài. |
| Build sau điều kiện drag cuối | `dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet -p:UseSharedCompilation=false`: 0 lỗi, 5 warning cũ. Chưa Play Mode lại drag. |

33 assertion thuộc source trước sửa datum height; không mô tả là toàn suite đã chạy lại sau mọi sửa đổi. Build Editor sau cleanup height pass; từng lượt build có thể không lặp warning do incremental. Unity còn log Visual Scripting không tìm được type Readme; chưa sửa phạm vi đó. Không có nghiệm thu server+2client, reconnect hay tải 2.000 unit.

Helper FogSnapshotCheck/FogRenderCheck/FogHeightVerification, meta và log/result dùng một lần đã được dọn; không giữ runner/receipt để chứng minh công việc. Ảnh giao người dùng nằm ngoài repo trong thư mục visualizations của Codex, không phải dependency để tiếp tục; ảnh sau sửa tên FowHeightFixed.png. Các số 11/24/34 ca trong handoff cũ là bằng chứng lịch sử, không phải suite còn trên đĩa.

## 4. Việc còn lại và trở ngại

- Chưa cài com.unity.netcode; chưa chọn/pin phiên bản tương thích Unity 6000.3.13f1, Entities 1.4.5 và Physics/Graphics hiện có.
- Chuyển client presentation sang Mono theo yêu cầu; giữ một adapter rõ ràng thay vì mỗi Mono tự query World.Default. Camera snap và FOW hiện vẫn là ECS systems.
- Chốt Ghost prefab/component variants, serializer/template và identity trước thay schema local. Không gửi Entity.Index/Version hay BlobAssetReference/pointer local nguyên trạng qua mạng.
- Command RPC bọc payload hiện có; connection xác định người phát lệnh, ánh xạ entity/reference và danh sách payload qua mạng rồi gọi đúng backend hiện tại. Không tự thay luật gameplay/queue.
- Phải thiết kế xóa ký ức khi nhìn lại vùng có building/resource đã mất, và phục hồi ký ức/Explored khi reconnect. Ghost mất relevancy không tự cho biết đó là chết hay khuất.
- Sprite selection/minimap đã được chặn render; picking, panel UI, command targeting và các ngoại lệ tài sản của chủ sở hữu chưa tích hợp đầy đủ visibility. Chưa đo hiệu năng việc rebuild terrain khi tập Explored tăng.
- Backlog builder/combat và tải thực độc lập vẫn ở [Roadmap](Roadmap.md); không coi local smoke là nghiệm thu toàn gameplay.

## 5. Một hành động đầu tiên ca sau

Lập bảng ánh xạ các component/system đang dùng trong Main sang **server simulation / dữ liệu Ghost đồng bộ / client Mono / adapter**, bắt đầu ở LocalFogPresentationSystem, LocalBaseCameraSnapSystem, EntityState, WorkerGatherData, BuilderComponent, production và CommandQueue. Kết quả cần bao phủ movement, combat/đạn, build, gather/resource, production/research, health, UI và FOW/terrain hiện có; không chốt bàn giao chỉ Worker/TownHall. Sau bảng này mới trình pipeline chuyển đổi và xác nhận package compatibility theo [hướng multiplayer](../Multiplayer/README.md).

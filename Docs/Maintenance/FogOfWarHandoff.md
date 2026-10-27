# Fog of War — bàn giao render và bộ lệnh local, 17/10/2026

## 1. Mục tiêu và phạm vi

Người dùng đã tạm gác multiplayer để chốt và triển khai bộ lệnh FOW local. Render, Attack mới, Build/preview/FIFO, Repair và Gather theo snapshot đã nối. Luật và source chính ở [kiến trúc FOW](../Architecture/FogOfWarRendering.md), xây dựng ở [BuildingPipeline](../Architecture/BuildingPipeline.md). [Multiplayer](../Multiplayer/README.md) giữ hướng đã bàn để dùng khi người dùng mở lại, chưa triển khai Netcode.

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

### Bộ lệnh và material mới

Luật chi tiết ở [FOW: lệnh và thông tin đã biết](../Architecture/FogOfWarRendering.md#lệnh-và-thông-tin-đã-biết). Một ô Visible cập nhật toàn entity; preview xây xanh/đỏ thông thường cần toàn footprint Visible. Unknown vẫn nhận kế hoạch để đi thử. Build tiếp tục FIFO rồi tìm nhà dở theo cửa sổ ±5 ô/cách lọc island của Gather; Repair không có queue. Có xử lý mục tiêu snapshot đã mất, movement thất bại và Move hủy placement chờ.

GhostUnknown.mat vàng nhạt alpha 0.65 đã gán Main; runtime fallback đã bỏ. Picking snapshot chọn đối tượng tĩnh gần ray nhất; bounds còn là footprint/hộp cao 2. SelectSystem.HandleClick đổi sang SystemAPI.GetSingleton để bỏ managed array trong nhánh Burst, không đổi luật click/drag.

### Vị trí khôi phục

Workspace `D:/Unity/TechTextAndChosingForRTS sample`, branch `Fog-of-wars`, HEAD khảo sát hiện tại `352e9ec` (`FOW command link complete`). Trước lượt docs này working tree sạch: source lệnh/material/Main/regression đã nằm trong HEAD hiện tại; không quy việc commit cho lượt docs. Docs đang cập nhật chưa commit/push, không có patch/snapshot riêng. Chuyển workspace cần commit trên và diff docs này.

## 3. Bằng chứng kiểm chứng

Các kết quả dưới đây là từ các lượt code trong hội thoại ngày 17/10, **không chạy lại trong lượt cập nhật docs**:

| Kiểm tra | Kết quả và giới hạn |
|---|---|
| Snapshot tùy chọn và terrain full | 21 assertion ECS pass, Unity 6000.3.13f1, exit 0; trước bỏ ba field World/Map ID/revision. |
| Adapter render + Main Play Mode | 33 assertion pass, Unity có graphics, Burst tắt riêng khi chạy, exit 0. Unit/đạn ẩn, building/resource giữ hình sau destroy và xóa khi nhìn lại; sprite restore/disposal; mask và Main terrain nhận đúng cache. |
| Main khi còn vision / mất toàn bộ vision | Main có 14 entity visible, 377/65.536 ô Explored. Runtime test bỏ vision: unit mất, ký ức tĩnh còn, terrain tối; đã xem ảnh camera URP thực. Lượt này còn lỗi height được phát hiện sau đó, không dùng ảnh cũ để nghiệm thu height. |
| Height sau sửa TerrainBaseLevel | Chạy trực tiếp Main trong Editor đang mở: 5 unit trước sửa lệch 1,59472; sau sửa cả unit/cache/mặt terrain đều Y=3,18944, sai lệch 0. Đã xem ảnh mới thấy chân unit/nhà đúng mặt đất. Không phải kiểm thử toàn bộ ramp/movement dài. |
| Build sau điều kiện drag cuối | `dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet -p:UseSharedCompilation=false`: 0 lỗi, 5 warning cũ. Chưa Play Mode lại drag. |

Bổ sung của lượt bộ lệnh và material (không chạy lại trong lượt docs):

| Kiểm tra | Kết quả và giới hạn |
|---|---|
| Regression lệnh | **20/20 pass** lịch sử trong Unity Editor; source test đã xóa theo yêu cầu ngày 20/10/2026. Bao phủ Attack khuất → Move, preview/terrain/occupancy, snapshot đã mất, FIFO/tìm gần, placement thành công/thất bại/hủy, retry và picking bị nhà che. Là fixture ECS/picking, không phải thao tác chuột toàn Main. |
| Build runtime + Editor | `dotnet build Assembly-CSharp-Editor.csproj --no-restore --verbosity quiet -p:UseSharedCompilation=false`: 0 lỗi, 5 warning cũ ở lượt đầy đủ. |
| Smoke Main | Play Main: 14 entity visible, 377 ô Explored, worker Move khoảng 0,9097 đơn vị, bộ đếm lỗi runtime helper = 0; trả Editor về Edit Mode. Trước hai chỉnh sửa cuối picking và singleton selection, không gọi là nghiệm thu lại toàn Main sau mọi thay đổi. |
| Material Unknown riêng | Kiểm tra asset/meta/GUID/reference Main và build runtime sau bỏ fallback: 0 lỗi/5 warning cũ. Chưa xem lại preview asset riêng trong Play; 20 ca chạy trước lượt tách material. |

Source regression FOW, terrain và cache server đã xóa theo yêu cầu ngày 20/10; không còn menu/batch runner để chạy lại. Helper smoke/marker/result/log một lần đã dọn; không có receipt mới. Các kết quả phía trên là lịch sử, không phải kiểm chứng runtime của lượt dọn.

33 assertion thuộc source trước sửa datum height; không mô tả là toàn suite đã chạy lại sau mọi sửa đổi. Build Editor sau cleanup height pass; từng lượt build có thể không lặp warning do incremental. Unity còn log Visual Scripting không tìm được type Readme; chưa sửa phạm vi đó. Không có nghiệm thu server+2client, reconnect hay tải 2.000 unit.

Helper FogSnapshotCheck/FogRenderCheck/FogHeightVerification, meta và log/result dùng một lần đã được dọn; không giữ runner/receipt để chứng minh công việc. Ảnh giao người dùng nằm ngoài repo trong thư mục visualizations của Codex, không phải dependency để tiếp tục; ảnh sau sửa tên FowHeightFixed.png. Các số 11/24/34 ca trong handoff cũ là bằng chứng lịch sử, không phải suite còn trên đĩa.

## 4. Việc còn lại và trở ngại

- Nghiệm thu bằng chuột trong Main: màu preview asset riêng, lệnh tới snapshot đã mất/thay đổi, nhiều worker/queue dài, chuyển lệnh/cancel, Gather/depot/tìm node mới.
- Chưa chốt Attack đang chạy rồi mất vision. Không coi đề xuất chase vị trí cuối thấy là luật đã duyệt.
- Panel UI/selection nói chung và ngoại lệ thông tin tài sản chủ sở hữu chưa tích hợp/kiểm chứng đầy đủ FOW. Picking bộ lệnh đã nối; không ghi toàn bộ command targeting là chưa làm.
- Chưa đo terrain rebuild khi Explored tăng, tải 2.000 unit hoặc nhiều island/địa hình phức tạp trong Main.
- Client full Mono chưa refactor. Multiplayer tạm gác: compatibility, Ghost/identity, RPC, reconnect/late join/session lifecycle chưa triển khai; xem [hướng đã lưu](../Multiplayer/README.md) khi người dùng mở lại.
- Backlog registry/combat độc lập ở [Roadmap](Roadmap.md). Không tái tạo harness đã xóa; kiểm chứng tiếp theo chọn phương pháp phù hợp với tác vụ.

## 5. Một hành động đầu tiên ca sau

Trong Main, kiểm tra bằng chuột **preview Unknown dùng GhostUnknown.mat → nhận PlanBuild → worker tới thử → placement hợp lệ hoặc chuyển phần tử FIFO kế tiếp khi bị block**. Chọn vùng chưa thấy đầy đủ và một kế hoạch tiếp theo; đối chiếu màu preview, thời điểm tạo nhà/chi tiền và target builder. Mục tiêu là xác nhận asset riêng cùng input thật; chưa mở lại multiplayer hoặc tự mở rộng luật Attack.

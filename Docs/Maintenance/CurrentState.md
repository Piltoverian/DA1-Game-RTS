# Trạng thái hiện tại — 17/10/2026

**Bắt đầu từ [lộ trình](Roadmap.md) nếu muốn biết việc tiếp theo. Đọc trang này khoảng 3 phút.**

## Fog of War local và bước multiplayer

Render FOW local đã nối vào Main, được người dùng chốt để chuyển bước. Native graphics hiện khi visible; unit/đạn khuất thì ẩn, building/resource giữ mesh/material/matrix cuối thấy và xóa ký ức khi nhìn lại footprint trống. Terrain dựng từ full tập Explored nhận mỗi Collect, mask 1 / 80/255 / 0. Frame bỏ WorldId/MapId/MapRevision; TerrainBaseLevel giữ mốc height chung để unknown không làm terrain cao lệch unit.

Lượt render: 33 assertion và Main Play graphics pass, Unity exit 0, trước sửa datum height. Lượt height sau đó: 5 unit từ lệch 1,59472 về 0, đã xem ảnh camera; không phải chạy lại toàn suite. Điều kiện drag cuối: >0,05 giây AND rộng >Screen.width/40, build 0 lỗi/5 warning cũ, chưa Play lại. Người dùng chọn không kiểm tra thêm local trước handoff.

Xem [kiến trúc render](../Architecture/FogOfWarRendering.md) và [handoff FOW](FogOfWarHandoff.md) cho source và giới hạn. [Multiplayer](../Multiplayer/README.md) là quyết định tiếp theo: client Mono qua adapter, server ECS/Netcode, RPC giữ payload command, Ghost/serializer theo package đã kiểm tra compatibility. Chưa cài Netcode, chưa chuyển presentation systems sang Mono, chưa có server+2client hoặc nghiệm thu UI/picking theo FOW.

## Lối chơi và dữ liệu

Cập nhật 15/10: snapper fit bounds mesh bằng scale đều, thay chuẩn collider; xem [công thức và kiểm chứng](../MapGeneration/GridSnapper_ResourceSpawn.md). Drag đã bỏ nhánh terrain ghi Selected trực tiếp: click/drag đều gửi SelectionRequest tới SelectSystem. Build thành công; chưa nghiệm thu hai thay đổi trong Play Mode. [Handoff](Handoff.md) ghi việc tiếp theo và phạm vi còn lại. Hủy nhà gửi CancelBuildingRequest riêng là chủ ý được người dùng xác nhận, không chuyển sang CommandQueue. Zoom/camera snap chưa sửa.

| Phần | Trạng thái dùng để tiếp tục |
|---|---|
| quyền sở hữu | Chỉ chủ sở hữu điều khiển thực thể; cùng đội không cấp quyền điều khiển chéo. CommandAuthority kiểm tra người phát lệnh, thực thể sống, mẫu đối tượng và chủ sở hữu. |
| Progression/registry | Registry dogfood đã thành Resources/GameReg.asset dùng chung Main/fixture; registry cũ đã xóa. Worker/TownHall/TrainWorker/Build dùng bộ mới trong TwoCivDogfood, không đăng ký bộ cũ MapGeneration. Spawn ưu tiên prefab civ, thiếu từng loại mới dùng DefaultWorker/DefaultTownHall của registry; map không có trường Worker/Hall. Main giữ 8 player/spawn slot, Civ A/B xen kẽ. [Cấu hình registry](../Guides/GameData/RegistryPresentation.md). Queue dùng GameBlobReference (CatalogKey + ID); registry sắp xếp lúc baking, tra cứu nhị phân khi chạy. |
| tài nguyên | Cost tĩnh được chuẩn hóa trước chuyển dữ liệu sang ECS; thanh toán nguyên tử, khả năng chi trả và hoàn tiền khi chạy dùng chung. |
| Placement/blockage | xem trước và mô phỏng dùng cùng bộ kiểm tra hợp lệ; chỉ nền phẳng cùng cao độ, reject ramp/cliff/mountain/occupancy. Cho phép xây chặn đường; vùng liên thông refresh theo kết nối lưới, dọn dẹp giữ vật chặn chồng lấn còn sống. |
| Combat/team | TeamID trong PlayerContext/cache; FindTarget/ShootAttack/bullet/AOE bỏ đồng minh. Bullet dùng bước đi xác định tới đích, kiểm tra lại target lúc trúng; artillery bỏ distress call. Unit tests pass; AOE/team Play Mode chưa nghiệm thu. |
| PlanBuild / Build | Hai command riêng. PlanBuild snapshot selection → placement tạo một công trình → Build cho tất cả worker hợp lệ. Build giao xây/sửa công trình đã tồn tại. [Pipeline và validation](../Architecture/BuildingPipeline.md). |
| Builder/preview | Tiếp cận bằng clamp tới footprint cộng clearance của unit; không dò ô/bucket. Preview áp scale đã snap trước show. Navigation footprint dùng anchor logic. Unit tests pass; work tăng end-to-end trong Play Mode bản clamp cuối chưa xác nhận. |
| thợ từ ca trước | Chủ dự án đã xác nhận các lỗi thợ trước đó được sửa. Không mở lại cảnh báo cũ; builder/work0 phát hiện trong ca này được theo dõi riêng ở Handoff. |

Giữ hàng đang mang khi Move/Build hoặc cùng loại tài nguyên; đổi loại bỏ hàng đang mang. Giao hàng đúng một lần. Construction work25/100 hoàn75; hoàn tất100 hoàn0; trùng lặp hủy không hoàn thêm. Đây là hợp đồng lối chơi và bằng chứng lịch sử, không phải kết quả kiểm thử mới của lượt dọn tài liệu.

## Bộ kiểm tra hợp lệ và bộ kiểm thử

Validator/helper vẫn biên dịch vào khi chạy, đã xếp theo chức năng:

- `Simulation/Construction/Validation/BuildingPlacementValidator.cs`
- `Simulation/Commands/Validation/CommandAuthority.cs`
- `Simulation/Economy/Validation/ResourcePayment.cs`
- `GameData/Validation/`: GameDataValidator, ValidationContext, ValidationIssue; các nhánh Definitions/Civ/Tech/Resources.

Các đường dẫn trên tương đối với `Assets/_Project/Scripts/Runtime/`. Namespace/API và GUID giữ nguyên. Hai csproj Visual Studio đã cập nhật đường dẫn; Unity có thể sinh lại chúng khi regenerate dự án.

Theo yêu cầu người dùng, đã xóa 7 bộ kiểm thử lối chơi: đặt nhà, thợ hàng đang mang, động chặn lối, rà soát kiểm tra hồi quy, tài nguyên chi phí, lệnh quyền sở hữu và progression tham chiếu, cùng meta. Không gọi các điểm gọi đó nữa. Catalog hiện hành ở [README kiểm thử](../../Assets/_Project/Tests/README.md).

Runner Task3 đã xóa trong lượt dọn được duyệt ngày 14/10/2026. Helper test đã được loại sau khi tách preview; xem phần tool hiện hành bên dưới.

## Địa hình và chuyển động

Main hiện dùng MoonTerrain/LunarTheme.asset, một luồng xử lý vật liệu atlas + độ sâu hình học. [Quy trình Unity](../TerrainVisualizer/14_SuDung_Atlas_Implementation.md) hướng dẫn nhập vào cùng SO, ghi đè và xem trước. lối chơi dùng XZ; Y phục vụ height/render. Height mô phỏng đọc grid/surface dữ liệu, không phụ thuộc bộ dựng hình duy nhất.

chuyển động dùng Dial ngăn hàng đợi, hàng xóm mặt nạ bộ nhớ đệm theo kết nối lưới và hạt giống shortcut khi một vùng liên thông hợp lệ. Không có formation/hierarchy/portal mới. [Tổng quan chuyển động](../Architecture/Movement/MovementAgent/Architecture_Overview.md), [kết quả tối ưu và giới hạn](VerificationHistory.md).

## Bằng chứng được giữ

| Bằng chứng lịch sử | Ý nghĩa và giới hạn |
|---|---|
| Task3 suite biên nhận kết quả (artifact lịch sử hiện không còn trên đĩa) và mã nguồn hashes (artifact lịch sử hiện không còn trên đĩa) | 9suite exit0 trên phiên bản ghi trong biên nhận kết quả; các bộ kiểm thử lối chơi nay đã xóa. Hash paths ghi vị trí trước khi chuyển folder. |
| Main ảnh trạng thái biên nhận kết quả (artifact lịch sử hiện không còn trên đĩa) | Main90 khung hình,40 tác nhân/1960static gốc, mã nguồn ảnh trạng thái không đổi; không phải tải2.000 lính. |
| Main rerun biên nhận kết quả (artifact lịch sử hiện không còn trên đĩa) | Lượt chạy lại không tái hiện mã nguồn write. Trước launch thợ khác phiên bản từng nghiệm thu; biên nhận kết quả không nghiệm thu bản thợ sửa sau đó. |
| tài nguyên báo cáo (artifact lịch sử hiện không còn trên đĩa), [địa hình dọn dẹp](../TerrainVisualizer/16_Render_Cleanup.md) | Chi tiết backend thanh toán và bộ dựng hình dọn dẹp trên lượt ghi nhận. |

Không xác định được writer của các thay đổi thợ trước đây; không quy nguyên nhân cho Unity/Main. Chỉ các artifact hiện có trên đĩa mới còn được giữ; các receipt Task3/ResourcePayment trong bảng trên hiện đã vắng. Thay đổi hiện tại chưa commit/push/PR/merge.

## Kiểm chứng mới ngày 14/10/2026

Chủ dự án đã chuyển gán static RegistryAuthoring.Instance từ Awake sang Baker.Bake; UI vẫn đọc ScriptableObject qua static, ECS dùng blob/buffer riêng. ResetInstance vẫn chạy tại SubsystemRegistration. [Hướng dẫn registry](../Guides/GameData/RegistryPresentation.md) mô tả luồng hiện tại; [Suite Baker mới](../../Artifacts/TwoCivDogfood/registry-baker-results.xml): 51/51 PASS, 0 fail/skip, 14/10/2026 12:41:51 UTC; test xác nhận icon dogfood/preview sau baking thật. [Handoff](Handoff.md) ghi phạm vi và giới hạn. Suite 50/50 phía dưới thuộc trước sửa đổi này. Play Mode sau reset/cache và player build chưa nghiệm thu.

Bản chốt session: [51/51 EditMode PASS](../../Artifacts/TwoCivDogfood/registry-baker-results.xml), 0 fail/skip. Sau đó đã xóa 4 bộ test Editor và helper dogfood theo yêu cầu, cùng artifact thử nghiệm cũ của session. Kết quả này thuộc trước dọn source test; giữ fixture runtime và receipt bàn giao. Play Mode/cache/player build vẫn chưa nghiệm thu.

Trước yêu cầu cập nhật sau, dùng [brainstorm-before-change](C:/Users/Legion/.codex/skills/brainstorm-before-change/SKILL.md): khảo sát → brainstorm → trình pipeline → chờ duyệt → triển khai/kiểm chứng.

## Chưa nghiệm thu trên source hiện tại

1. Play Mode builder clamp cuối: PlanBuild nhiều worker, tiến vào tầm, CompletedWork tăng, completed/queue/cancel. Gather/box-select, GUI Validate/Undo và SubScene độc lập còn cần kiểm chứng.
2. AOE/team và các edge case combat trong Play Mode; group/ECB authoritative theo [lộ trình](Roadmap.md). Unit tests và duel cục bộ không thay thế các mục này.
3. Workload2.000 lính/30FPS và simulation20Hz; transport/server+2client/resync.

Mục tiêu chơi nhiều người: máy chủ quyết định trạng thái, nội suy phía máy khách; map512 ×512,2–10player,2.000 lính tổng. Đây là mục tiêu, không phải đo hiệu năng đã đạt.

## Tool và atlas sau dọn ngày 14/10/2026

Giữ TerrainAtlasWindow, TerrainAtlasAuthoring, TerrainCanonicalBaker, TerrainAtlasExample, TerrainAtlasPreview và editor GameData/ID. Validate Registry là thao tác thiết kế dữ liệu, không phải harness test. GridDebug phục vụ quan sát lưới đang chạy nên được giữ.

Xóa 3 helper test còn lại. Preview chuyển sang TerrainAtlasPreview.Render, trả Texture2D trực tiếp và dọn đối tượng tạm; không ghi report PASS hoặc dùng fixture test.

Một bộ docs chính nằm trong Docs; nội dung riêng được gộp, bộ GianLuoc đã bỏ. Một nguồn atlas reference JadeSlate; các reference khác đã bỏ. Main hiện gán MoonTerrain/LunarTheme.asset (GUID abf7ff7b1945a7a4bbbcbfb9f158a1fc), không đổi theme scene trong lượt dọn. JadeSlateAtlas là đầu ra của tool tạo ví dụ, không phải theme hiện được Main gán.

Thay đổi chưa commit. Artifact của lượt dọn tool/docs/atlas đã xóa theo yêu cầu; kết quả gameplay lịch sử không thay thế kiểm chứng mới.

Kiểm chứng lượt dọn tool/docs/reference: Unity 6000.3.13f1 batch import/compile PASS, exit0; kiểm tra 80 trang Markdown không có link hỏng, không thiếu compile include, không còn helper test C#. Preview mới chưa kiểm tra trực quan trong Editor; chưa chạy gameplay test mới. Log và receipt của lượt này đã xóa theo yêu cầu.

Dọn ngày 15/10/2026 theo yêu cầu: xóa 6 artifact cleanup cũ, example.txt và thư mục rỗng DefStruct/__MACOSX/TerrainValidation/TerrainUpgrade; bỏ hướng dẫn gọi test đã nghỉ dùng. Giữ nguyên JadeSlateAtlas, LunarTheme, toàn bộ scene và bằng chứng Baker. Lượt này chỉ dọn file/docs, không chạy lại Unity hoặc gameplay test, không tạo artifact mới.

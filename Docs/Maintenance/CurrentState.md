# Trạng thái cần biết trước khi sửa code

Các mục cũ đối chiếu source ngày 27/10/2026; phần multiplayer được cập nhật và kiểm chứng ngày 03/11/2026. Trang này chỉ giữ giới hạn, kiểm chứng còn hiệu lực và hành động tiếp theo; thiết kế nằm ở các trang chủ đề trong [mục lục](../README.md).

## Đã triển khai và đã kiểm chứng

| Phần | Trạng thái |
|---|---|
| Command/movement | Phân loại TargetTo qua một TryResolveTargetAction; các nhánh dừng dùng MovementAgentAPI. Các ca command resolution, pause giữ field, clear release field và lý do dừng đã pass trong Unity batch. |
| Multiplayer 03/11/2026 | Identity/owner, FOW/ACK/RPC đã nối; continuation thêm seed runtime riêng server, persistent slot/timeout, vé rejoin theo profile và BuildOrder incremental. Unity compile/codegen/ILPP, 37 assertions và smoke hai client qua move/train/ownership rejection/place/cancel; [chi tiết và giới hạn](../Multiplayer/GameplayNetworking.md#kiểm-chứng). |
| Terrain/FOW refactor | Local/network chung TerrainView và renderer; frame FOW mang index Explored/Visible; geometry cache riêng. Unity 6000.3.13f1 compile/source generation và các ca parity, exploration/viewer/revision, visibility không đổi geometry version, chunk thiếu/sai owner, disconnect và gate join đã pass. |
| Mesh | Smoke bằng LunarTheme của Main: một ô visual tạo đúng một sprite/4 vertex, không phát Unknown; surface giữ supplied height và được dọn khi clear. Đây là smoke mesh, không phải nghiệm thu hình ảnh Play Mode. |
| Cleanup | Đã bỏ overload renderer GeneratedTerrain và compiler fallback local; preview tự compile rồi dùng đầu vào chung. Regression thiếu cấu hình hiện tại chờ source đã pass. Runner/script/log dùng một lần đã dọn; không có executeMethod cũ để chạy lại. |

Người dùng xác nhận gameplay local ổn **trước** refactor. Chưa có xác nhận Play Mode sau refactor hoặc host/client qua transport thật. Các số PASS cũ không được dùng để nghiệm thu source sửa về sau. Lượt gom tài liệu chỉ kiểm tra tài liệu/source/link, không chạy lại Unity.

## Những việc còn cần xác nhận

- Matchmaking/lobby/chọn spawn/ban-pick: hiện có sketch dữ liệu trong `MatchBootstrap.cs`; chưa nghiệm thu pipeline tạo trận hoặc driver migration. Thiết kế và các bước triển khai tiếp theo nằm tại [Multiplayer](../Multiplayer/README.md#dự-kiến-matchmaking-lobby-và-vòng-đời-match), ghi nhận ngày 30/10/2026.

- Main local sau refactor: terrain/height/picking, preview Unknown, PlanBuild tới thử, FIFO, repair/gather khi snapshot mất, cancel và Move thay thế. Luật Attack đang chạy rồi mất vision còn chưa chốt; không tự thêm chase theo suy đoán.
- UI/selection chưa được chứng minh hoàn toàn chỉ dùng dữ liệu FOW. Đổi viewer có báo cáo HUD stale/click unit sát TownHall khó; cần reproduce trên source hiện tại trước sửa.
- RegistryAuthoring.Instance resolve reference trên registry của presentation world đang sống; đã có kiểm tra ECS đổi world/catalog/selection. Domain Reload bật/tắt, SubScene đóng, player build và GUI Validate/Undo chưa nghiệm thu đầy đủ.
- Multiplayer: happy path hai client trước continuation đã qua theo [GameplayNetworking](../Multiplayer/GameplayNetworking.md#kiểm-chứng). Source mới đã có kiểm tra ECS seed/rejoin/placement, deadline tải riêng, RPC backlog, neutral last-seen riêng player và staged terrain; còn smoke hai process với rejoin/tắt mở, mất gói/latency và camera/HUD/proxy neutral. Chưa đạt mục tiêu client full Mono.
- Movement: đo Integration/seed/rebuild và main-thread wait trong cùng workload. Source có giới hạn loop seed 1000 so với MaxIslands 10000; clearance unit lớn/overlap/far-stall chưa được chứng minh đầy đủ.
- Mục tiêu 2.000 unit, 20 Hz và map 512×512/2–10 player là mục tiêu thiết kế, chưa có benchmark hiện hành chứng minh FPS/băng thông. Không dùng smoke vài chục unit thay tải thật.

## Việc đầu tiên khi tiếp tục

Mở Main ở LocalSimulation, kiểm tra **preview Unknown → PlanBuild → worker tới thử → placement hoặc FIFO kế tiếp**, đồng thời quan sát terrain/height và lỗi Console. Dùng source hiện tại, không khôi phục harness đã xóa. Khi local đúng mới làm checklist host/client trong [Multiplayer](../Multiplayer/README.md).

## Workspace và cách giữ thay đổi

Workspace hiện tại: `D:/Unity/TechTextAndChosingForRTS sample`. Các thay đổi trong phiên còn trong working tree, gồm file mới và `.meta`; chưa stage/commit/push. Dùng git status/diff để biết trạng thái thực tế, không dựa vào SHA cũ trong một handoff. Nếu chuyển workspace phải mang cả thay đổi chưa commit và file mới; checkout HEAD không khôi phục chúng.

# Kế hoạch spawn nhà và quân

Trạng thái: kế hoạch, chưa triển khai. Dựa trên facility gameplay hiện có, ngày 2026-10-05.

## 1. Phạm vi

Đầu trận: đặt nhà chính và quân khởi đầu của từng player theo điểm terrain đã gen. Trong trận: quân từ production xuất hiện ở vị trí đi được quanh nhà sản xuất. Số lượng và loại quân là cấu hình designer; không hard-code một đội hình vào generator terrain.

Giữ luật mọi unit dùng chung walkable. Spawn không thêm clearance, physicalWalkable hoặc cơ chế cân bằng tuyệt đối.

Mọi phép đổi cell ↔ index và world ↔ cell dùng GridHelper.GetNodeIndex/GetGridPosFromIndex/WorldToGrid/GridToWorld; helper spawn không tự viết lại công thức index.

## 2. Dữ liệu và facility sử dụng

| Cần gì | Dùng lại | Bổ sung dự kiến |
|---|---|---|
| Điểm terrain | GridSpawnCell, GridHelper.GridToWorld | Ánh xạ spawn slot sang PlayerId thực tế |
| Player và civilization | PlayerContext | Cấu hình đội hình khởi đầu cho player/civ |
| Prefab nhà/quân | GameDataRegistryComponent, RegistryBlobElement, RegistryPrefabElement | Tra cứu definition ID của loadout |
| Owner và selection | EntityOwner, Selectable | Gán đúng owner khi instantiate |
| Nhà hoạt động | BuildingConstruction, EntityHealth, MainBaseTag | Nhà đầu trận có Phase Completed, HP đầy |
| Footprint và cost | BlockageData, BlockageNeedBakeTag, CostChangeSystem | Dùng chung helper tính/kiểm tra/reserve footprint với building placement |
| Population | PopulationSystem | Để hệ thống đăng ký entity spawn đầu trận và capacity, tránh cộng dân số hai lần |
| Movement | SetupUnitMoverDefaultPosition, MoveOverride | Khởi tạo unit ở vị trí hợp lệ; giữ rally nếu có |
| Production | ProductionSystem, ProductionJobs | Tìm ô xuất quân hợp lệ trước khi dequeue/population update |

GridSpawnCell.playerId hiện được ghi bằng chỉ số 0..players-1 khi bake. Không mặc định chỉ số này luôn là ID mạng hoặc identity của người chơi. Cần mapping rõ ràng; nếu player chọn spawn, mỗi slot chỉ được cấp cho một player.

Cấu hình tối thiểu dự kiến: StartingBuildingId; các cặp UnitDefinitionId/Count; mapping PlayerId/SpawnSlot. Giữ registry hiện tại làm nguồn prefab. Chưa cần một catalog prefab song song.

## 3. Rule đặt nhà đầu trận

1. Bắt đầu tìm gần tâm GridSpawnCell, duyệt ô theo thứ tự cố định, mở rộng ra ngoài trong vùng spawn được bảo vệ.
2. Kiểm tra toàn bộ footprint từ BlockageData, không chỉ ô tâm: trong bounds, terrain walkable, cùng heightLevel, không núi, không cliff/ramp và không chồng footprint đã reserve.
3. Dùng kích thước footprint trong world space rồi đổi sang grid; không coi footprint là số ô cố định khi cellsize thay đổi.
4. Giữ chỗ đi vòng và ít nhất một vị trí xuất quân hợp lệ ngoài footprint. Đây là điều kiện bố trí nhà, không phải clearance theo loại unit.
5. Nếu không đặt được trong vùng hợp lệ, báo không đủ diện tích; không đào cliff/núi và không ép đặt nhà lên ramp.
6. Nhà đầu trận được tạo hoàn chỉnh: owner, transform, construction Completed, HP đầy, MainBaseTag theo prefab. Không đi qua nhánh xây nhà cần worker/trừ tài nguyên.
7. Đăng ký footprint vào blockage/cost trước khi cho gameplay chạy. PopulationSystem tự ghi nhận capacity của nhà hoàn chỉnh.

BuildingPlacementSystem hiện có kiểm tra footprint/cost và reserve grid, nhưng đi kèm worker, affordability và trạng thái Planned. Cần tách helper hình học dùng chung; không gửi PlaceBuildingRequest giả để spawn nhà đầu trận.

## 4. Rule đặt quân khởi đầu

1. Đặt nhà trước; tìm ô quân bên ngoài footprint nhà trên cost/reservation đã cập nhật.
2. Duyệt vị trí quanh nhà theo thứ tự ổn định; ưu tiên gần nhà, trong vùng đi được nối với lối ra của nhà.
3. Mỗi unit có vị trí ban đầu riêng, tránh trùng tâm với unit đã lên kế hoạch hoặc entity đang đứng ở đó. Không thêm điều kiện unit lớn phải có walkability riêng.
4. Dùng GridToWorld và offset/pivot của prefab; giữ scale/rotation cần thiết. Gán EntityOwner và Selectable.playerID đồng nhất.
5. Để PopulationSystem đăng ký unit khởi đầu. Không gọi Adjust thủ công rồi lại để hệ thống tự đăng ký.
6. Không có lệnh ban đầu thì unit đứng yên. Nếu cấu hình rally, dùng MoveOverride hiện có. Facility setup vị trí chỉ là xử lý một lần, không thay cho bước chọn vị trí đúng trước instantiate.
7. Nếu không đủ vị trí cho toàn bộ loadout, chưa đánh dấu trận Ready và không âm thầm bỏ bớt quân.

## 5. Thứ tự khởi tạo đầu trận

| Giai đoạn | Điều kiện và hành động |
|---|---|
| Chờ dữ liệu | Grid/cost sẵn sàng, có GridSpawnCell, registry và PlayerContext đầy đủ; không thêm lại GridInitialized |
| Lập bố trí | Resolve prefab, kiểm tra owner/slot, tính vị trí tất cả nhà/quân trên reservation tạm; chưa instantiate |
| Tạo entity | Áp bố trí đã hợp lệ, tạo nhà trước và quân sau; gán owner/transform/trạng thái |
| Hoàn thiện gameplay | Ghi nhận blockage, cập nhật cost/island, population và setup movement |
| Ready | Đánh dấu khởi tạo hoàn tất một lần cho match; cho phép điều khiển/production |

Lập bố trí toàn bộ trước khi tạo entity giúp tránh player đầu có nhà/quân nhưng player sau không còn chỗ. Reservation tạm phải tính cả các nhà/quân trong cùng đợt spawn, kể cả khi ECB chưa playback.

State khởi tạo trận là state riêng, không dùng lại cờ khởi tạo grid. Chỉ chuyển Ready khi các hệ thống gameplay đã thấy entity và cost/island phản ánh footprint mới; một UpdateAfter giữa các group khác nhau không đủ để bảo đảm việc này.

## 6. Quân xuất từ production trong trận

ProductionSystem hiện lấy vị trí bằng TransformPoint(SpawnOffset), tăng population và bỏ item khỏi queue khi hoàn thành. Vị trí đó có thể nằm trong nhà, cliff hoặc obstacle mới.

Thay đổi dự kiến:

1. Khi item hoàn tất và đủ population, lấy SpawnOffset làm điểm ưu tiên rồi tìm vị trí hợp lệ quanh footprint producer.
2. Kiểm tra cost hiện tại, vị trí unit khác và reservation của các producer cùng tick.
3. Có vị trí mới instantiate; sau đó mới cập nhật population và dequeue như luồng hiện có.
4. Nếu bị vây kín, giữ item đã hoàn tất trong queue và thử lại tick sau; không trừ tài nguyên thêm hoặc reset thời gian đào tạo.
5. Giữ RallyOffset qua MoveOverride và cơ chế resolved target hiện tại.

## 7. Multiplayer

Spawn chạy một lần trên server/host có quyền quyết định gameplay. Client nhận entity, owner và transform qua cơ chế đồng bộ; không tự instantiate lại đội hình đầu trận.

Single-player dùng cùng luồng authority local. Khi chưa tích hợp networking, state Ready và mapping PlayerId/SpawnSlot vẫn hữu ích; không coi chúng là bằng chứng hệ thống đã có network replication.

Việc chọn spawn do host chốt trước bước lập bố trí. Client không được tự sửa mapping hoặc quyết định owner qua dữ liệu render.

## 8. Các bước triển khai

| Bước | Công việc | Điều kiện hoàn thành |
|---|---|---|
| 1 | Chốt loadout cấu hình và mapping slot/player; kiểm tra prefab registry | Mỗi player có một slot, mọi definition ID resolve đúng prefab |
| 2 | Tách helper footprint và lập bố trí nhà/quân | Không chồng lấn, không vượt biên, không đặt sai terrain |
| 3 | Thêm hệ thống spawn đầu trận và state hoàn tất một lần | Main có đúng nhà/quân/owner, không spawn thêm sau nhiều frame |
| 4 | Nối blockage, population và movement trước Ready | Nhà chặn đúng footprint; quân di chuyển được; population đúng |
| 5 | Dùng helper chọn vị trí cho ProductionSystem | SpawnOffset bị chặn thì tìm chỗ khác; bị vây kín thì giữ queue |
| 6 | Gắn quyền spawn vào server world khi tích hợp multiplayer | Client không tạo bản sao nhà/quân; owner/slot được host quyết định |

Xác nhận trực tiếp trong Main bằng các tình huống: nhiều player; ô ưu tiên bị chặn; không đủ footprint; producer bị vây kín rồi mở đường; scene tiếp tục chạy nhiều frame; số population trước/sau spawn. Không tạo lại scene preview hoặc project test riêng.

## 9. Chỉ mục code hiện có

- [GridSpawnCell và schema](<../../Assets/Scripts/Game(New Simulation Logic)/MovementAgent/Grid/GridComponent.cs>).
- [Registry lookup và lệnh Build từ UI](<../../Assets/Scripts/ClientSide (Presentation)/InputReceiver/BuildingPlacer.cs>).
- [BuildingPlacementSystem](<../../Assets/Scripts/Game(New Simulation Logic)/System/Construction/BuildingPlacementSystem.cs>).
- [ProductionSystem](<../../Assets/Scripts/Game(New Simulation Logic)/System/Construction/ProductionSystem.cs>), [ProductionJobs](<../../Assets/Scripts/Game(New Simulation Logic)/NewDataRefactor/Component/ProductionJobs.cs>).
- [PopulationSystem](<../../Assets/Scripts/Game(New Simulation Logic)/NewDataRefactor/Component/PopulationSystem.cs>).
- [Setup movement một lần](<../../Assets/Scripts/Game(New Simulation Logic)/Movement/SetupUnitDefaultPositionSystem.cs>).
- [MainBaseAuthoring](<../../Assets/Scripts/Game(New Simulation Logic)/CoreECS/Authoring/MainBaseAuthoring.cs>).

# Kế hoạch sinh đối tượng nhà và quân

Trạng thái: đã chuẩn bị khởi tạo đầu trận ngày 2026-10-06; cần cấu hình và nghiệm thu Main. Xem [người chơi khởi tạo chung](PlayerBootstrap.md). Tìm vị trí sản xuất và đồng bộ trạng thái chơi nhiều người vẫn chưa triển khai.

## 1. Phạm vi

Đầu trận: đặt nhà chính và quân khởi đầu của từng người chơi theo điểm địa hình đã gen. Trong trận: quân từ sản xuất xuất hiện ở vị trí đi được quanh nhà sản xuất. Số lượng và loại quân là cấu hình người thiết kế; không hard-mã một đội hình vào bộ sinh dữ liệu địa hình.

Giữ luật mọi lính dùng chung đi được. Spawn không thêm khoảng trống, physicalWalkable hoặc cơ chế cân bằng tuyệt đối.

Mọi phép đổi ô ↔ chỉ số và thế giới ECS ↔ ô dùng GridHelper.GetNodeIndex/GetGridPosFromIndex/WorldToGrid/GridToWorld; helper sinh đối tượng không tự viết lại công thức chỉ số. Caller kiểm tra giới hạn trước khi đọc bộ đệm; xem [quy ước GridHelper](MapGen_Unity_Rules.md).

## 2. Dữ liệu và facility sử dụng

| Cần gì | Dùng lại | Bổ sung dự kiến |
|---|---|---|
| Điểm địa hình | GridSpawnCell, GridHelper.GridToWorld | Ánh xạ sinh đối tượng vị trí sang PlayerId thực tế |
| người chơi và civilization | PlayerContext | Cấu hình đội hình khởi đầu cho player/civ |
| mẫu đối tượng nhà/quân | GameDataRegistryComponent, RegistryBlobElement, RegistryPrefabElement | Tra cứu definition ID của nhóm đối tượng khởi đầu |
| Owner và lựa chọn | EntityOwner, Selectable | Gán đúng chủ sở hữu khi tạo bản sao đối tượng |
| Nhà hoạt động | BuildingConstruction, EntityHealth, MainBaseTag | Nhà đầu trận có Phase Completed, HP đầy |
| Footprint và chi phí | BlockageData, BlockageNeedBakeTag, CostChangeSystem | Dùng chung helper tính/kiểm tra/reserve vùng chiếm chỗ với nhà đặt nhà |
| dân số | PopulationSystem | Để hệ thống đăng ký thực thể sinh đối tượng đầu trận và capacity, tránh cộng dân số hai lần |
| chuyển động | SetupUnitMoverDefaultPosition, MoveOverride | Khởi tạo lính ở vị trí hợp lệ; giữ rally nếu có |
| Production | ProductionSystem, ProductionJobs | Tìm ô xuất quân hợp lệ trước khi dequeue/population update |

GridSpawnCell.playerId hiện được ghi bằng chỉ số 0..players-1 khi chuyển dữ liệu sang ECS. Không mặc định chỉ số này luôn là ID mạng hoặc identity của người chơi. Cần mapping rõ ràng; nếu người chơi chọn sinh đối tượng, mỗi vị trí chỉ được cấp cho một người chơi.

Cấu hình hiện tại: `PlayerBootstrapSettings` chung cho mọi người chơi (số thợ, tài nguyên, tuổi, bán kính bố trí); roster chỉ chứa PlayerId/SpawnSlot/Civ. `CivBlob.TownHallPrefab` và `StartWorkerPrefab` trỏ vào danh mục dữ liệu mẫu đối tượng hiện có. Không có số worker/tài nguyên riêng cho từng civ/player.

## 3. Rule đặt nhà đầu trận

1. Bắt đầu tìm gần tâm GridSpawnCell, duyệt ô theo thứ tự cố định, mở rộng ra ngoài trong vùng sinh đối tượng được bảo vệ.
2. Kiểm tra toàn bộ vùng chiếm chỗ từ BlockageData, không chỉ ô tâm: trong giới hạn, địa hình đi được, cùng heightLevel, không núi, không cliff/ramp và không chồng vùng chiếm chỗ đã giữ chỗ.
3. Dùng kích thước vùng chiếm chỗ trong thế giới ECS space rồi đổi sang lưới; không coi vùng chiếm chỗ là số ô cố định khi cellsize thay đổi.
4. Giữ chỗ đi vòng và ít nhất một vị trí xuất quân hợp lệ ngoài vùng chiếm chỗ. Đây là điều kiện bố trí nhà, không phải khoảng trống theo loại lính.
5. Nếu không đặt được trong vùng hợp lệ, báo không đủ diện tích; không đào cliff/núi và không ép đặt nhà lên dốc nối tầng.
6. Nhà đầu trận được tạo hoàn chỉnh: chủ sở hữu, transform, construction Completed, HP đầy, MainBaseTag theo mẫu đối tượng. Không đi qua nhánh xây nhà cần worker/trừ tài nguyên.
7. Đăng ký vùng chiếm chỗ vào blockage/cost trước khi cho lối chơi chạy. PopulationSystem tự ghi nhận capacity của nhà hoàn chỉnh.

BuildingPlacementSystem hiện có kiểm tra footprint/cost và giữ chỗ lưới, nhưng đi kèm thợ, khả năng chi trả và trạng thái Planned. Cần tách helper hình học dùng chung; không gửi PlaceBuildingRequest giả để sinh đối tượng nhà đầu trận.

## 4. Rule đặt quân khởi đầu

1. Đặt nhà trước; tìm ô quân bên ngoài vùng chiếm chỗ nhà trên cost/reservation đã cập nhật.
2. Duyệt vị trí quanh nhà theo thứ tự ổn định; ưu tiên gần nhà, trong vùng đi được nối với lối ra của nhà.
3. Mỗi lính có vị trí ban đầu riêng, tránh trùng tâm với lính đã lên kế hoạch hoặc thực thể đang đứng ở đó. Không thêm điều kiện lính lớn phải có walkability riêng.
4. Dùng GridToWorld và offset/pivot của mẫu đối tượng; giữ scale/rotation cần thiết. Gán EntityOwner và Selectable.playerID đồng nhất.
5. Để PopulationSystem đăng ký lính khởi đầu. Không gọi Adjust thủ công rồi lại để hệ thống tự đăng ký.
6. Không có lệnh ban đầu thì lính đứng yên. Nếu cấu hình rally, dùng MoveOverride hiện có. Facility thiết lập vị trí chỉ là xử lý một lần, không thay cho bước chọn vị trí đúng trước tạo bản sao đối tượng.
7. Nếu không đủ vị trí cho toàn bộ nhóm đối tượng khởi đầu, chưa đánh dấu trận Ready và không âm thầm bỏ bớt quân.

## 5. Thứ tự khởi tạo đầu trận

| Giai đoạn | Điều kiện và hành động |
|---|---|
| Chờ dữ liệu | Grid/cost sẵn sàng, có GridSpawnCell, danh mục dữ liệu và PlayerContext đầy đủ; không thêm lại GridInitialized |
| Lập bố trí | phân giải mẫu đối tượng, kiểm tra owner/slot, tính vị trí tất cả nhà/quân trên reservation tạm; chưa tạo bản sao đối tượng |
| Tạo thực thể | Áp bố trí đã hợp lệ, tạo nhà trước và quân sau; gán owner/transform/trạng thái |
| Hoàn thiện lối chơi | Ghi nhận chặn lối, cập nhật chi phí/vùng liên thông, dân số và thiết lập chuyển động |
| Ready | Đánh dấu khởi tạo hoàn tất một lần cho match; cho phép điều khiển/production |

Lập bố trí toàn bộ trước khi tạo thực thể giúp tránh người chơi đầu có nhà/quân nhưng người chơi sau không còn chỗ. Reservation tạm phải tính cả các nhà/quân trong cùng đợt sinh đối tượng, kể cả khi ECB chưa playback.

State khởi tạo trận là trạng thái riêng, không dùng lại cờ khởi tạo lưới. Chỉ chuyển Ready khi các hệ thống lối chơi đã thấy thực thể và chi phí/vùng liên thông phản ánh vùng chiếm chỗ mới; một UpdateAfter giữa các group khác nhau không đủ để bảo đảm việc này.

## 6. Quân xuất từ sản xuất trong trận

ProductionSystem hiện lấy vị trí bằng TransformPoint(SpawnOffset), tăng dân số và bỏ item khỏi hàng đợi khi hoàn thành. Vị trí đó có thể nằm trong nhà, vách hoặc obstacle mới.

Thay đổi dự kiến:

1. Khi item hoàn tất và đủ dân số, lấy SpawnOffset làm điểm ưu tiên rồi tìm vị trí hợp lệ quanh vùng chiếm chỗ bên sản xuất.
2. Kiểm tra chi phí hiện tại, vị trí lính khác và reservation của các bên sản xuất cùng bước mô phỏng.
3. Có vị trí mới tạo bản sao đối tượng; sau đó mới cập nhật dân số và lấy khỏi hàng đợi như luồng hiện có.
4. Nếu bị vây kín, giữ item đã hoàn tất trong hàng đợi và thử lại bước mô phỏng sau; không trừ tài nguyên thêm hoặc đặt lại thời gian đào tạo.
5. Giữ RallyOffset qua MoveOverride và cơ chế resolved mục tiêu hiện tại.

## 7. chơi nhiều người

Spawn chạy một lần trên server/host có quyền quyết định lối chơi. máy khách nhận thực thể, chủ sở hữu và transform qua cơ chế đồng bộ; không tự tạo bản sao đối tượng lại đội hình đầu trận.

Single-người chơi dùng cùng luồng quyền điều khiển cục bộ. Khi chưa tích hợp networking, trạng thái Ready và mapping PlayerId/SpawnSlot vẫn hữu ích; không coi chúng là bằng chứng hệ thống đã có mạng đồng bộ trạng thái.

Việc chọn sinh đối tượng do máy chủ kiêm máy chơi chốt trước bước lập bố trí. máy khách không được tự sửa mapping hoặc quyết định chủ sở hữu qua dữ liệu dựng hình.

## 8. Các bước triển khai

| Bước | Công việc | Điều kiện hoàn thành |
|---|---|---|
| 1 | Chốt nhóm đối tượng khởi đầu cấu hình và mapping slot/player; kiểm tra mẫu đối tượng danh mục dữ liệu | Mỗi người chơi có một vị trí, mọi definition ID phân giải đúng mẫu đối tượng |
| 2 | Tách helper vùng chiếm chỗ và lập bố trí nhà/quân | Không chồng lấn, không vượt biên, không đặt sai địa hình |
| 3 | Thêm hệ thống sinh đối tượng đầu trận và trạng thái hoàn tất một lần | Main có đúng nhà/quân/owner, không sinh đối tượng thêm sau nhiều khung hình |
| 4 | Nối chặn lối, dân số và chuyển động trước Ready | Nhà chặn đúng vùng chiếm chỗ; quân di chuyển được; dân số đúng |
| 5 | Dùng helper chọn vị trí cho ProductionSystem | SpawnOffset bị chặn thì tìm chỗ khác; bị vây kín thì giữ hàng đợi |
| 6 | Gắn quyền sinh đối tượng vào máy chủ thế giới ECS khi tích hợp chơi nhiều người | máy khách không tạo bản sao nhà/quân; owner/slot được máy chủ kiêm máy chơi quyết định |

Xác nhận trực tiếp trong Main bằng các tình huống: nhiều người chơi; ô ưu tiên bị chặn; không đủ vùng chiếm chỗ; bên sản xuất bị vây kín rồi mở đường; cảnh tiếp tục chạy nhiều khung hình; số dân số trước/sau sinh đối tượng. Không tạo lại cảnh xem trước hoặc dự án kiểm thử riêng.

## 9. Chỉ mục mã hiện có

- GridHelper: tọa độ và chỉ số (nguồn lịch sử hiện không còn trên đĩa).
- GridSpawnCell và lược đồ dữ liệu (nguồn lịch sử hiện không còn trên đĩa).
- Registry tra cứu và lệnh Build từ UI (nguồn lịch sử hiện không còn trên đĩa).
- BuildingPlacementSystem (nguồn lịch sử hiện không còn trên đĩa).
- ProductionSystem (nguồn lịch sử hiện không còn trên đĩa), ProductionJobs (nguồn lịch sử hiện không còn trên đĩa).
- PopulationSystem (nguồn lịch sử hiện không còn trên đĩa).
- thiết lập chuyển động một lần (nguồn lịch sử hiện không còn trên đĩa).
- MainBaseAuthoring (nguồn lịch sử hiện không còn trên đĩa).

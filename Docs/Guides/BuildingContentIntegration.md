# Công trình và tài nguyên — 05/11/2026

## Cấu hình demo hiện hành — 25/11/2026

Các mục ngày 05/11 bên dưới là lịch sử tích hợp. Bản Main hiện dùng profile
1 người đấu 1 AI trong `Data/Playable`. TownHall cũng đã bật
`offsetsFollowModel`, dùng cùng phép tìm lối ra an toàn như Barracks.

| Nội dung | Giá trị hiện hành |
| --- | --- |
| Worker / Soldier | Tốc độ 10; trước đây 7 |
| Defense Tower | 500 HP, WorkLoad 25 |
| Resource Depot | 450 HP, giá 100 Wood, WorkLoad 40 |
| Barracks | 700 HP, WorkLoad 25 |
| Train Soldier | WorkLoad 15; giữ giá 25 Gold |
| Đạn Soldier / Tower | Tốc độ 28 / 20 để phù hợp đơn vị chạy nhanh hơn |

Worker trả tài nguyên dùng khoảng tương tác có tính đến kích thước ô và
khoảng dừng navigation; builder tự thoát công việc khi công trình đã đầy HP.
Combat cập nhật pause/move trước khi entity có thể bị hủy, không để lệnh
deferred ghi vào entity đã chết. Quân dừng bắn không bị movement xoay ngược
về rally point; tầm bắn công trình tính tới mép footprint thay vì tâm bị chặn.

Kiểm chứng vòng chơi hiện được chạy qua `PlayableLoopSmoke` trong Unity thật;
kết quả chỉ hoàn chỉnh khi có cả victory và restart, không suy ra từ compiler.

Khung điều khiển bốn vùng đã được chủ dự án nghiệm thu. Đợt này tích hợp
nội dung công trình vào pipeline hiện tại, không thay framework UI.

## Bản sửa tỉ lệ và vận hành sau ảnh nghiệm thu

- Giảm footprint tháp 6 → 3 ô, kho 20 → 4 ô, trại 27 → 6 ô; TownHall giữ 7 ô,
  worker giữ đường kính 0.75 ô. Model tiếp tục co đồng đều theo bounds để không
  méo hình; collider/grid/ghost dùng cùng footprint, không chỉ thu nhỏ ảnh.
- Nguyên nhân model xây xong bị cắt: ConstructionSystem dùng trần world-height
  `baseY + 10`. BuildingAuthoring nay bake ConstructionVisualBounds từ mesh,
  ConstructionSystem lấy đủ tám góc qua LocalTransform và PostTransformMatrix,
  nội suy từ đáy đến đỉnh thật có margin. 100% phải bao trùm cả mái/nòng súng,
  kể cả khi thay scale hoặc đặt trên nền cao. Mesh/transform hierarchy được đăng
  ký dependency để chỉnh model sẽ kích hoạt bake lại bounds.
- DefenseTower gắn StationaryDefenseAuthoring: ngắm xoay model quanh Y bằng
  PostTransformMatrix; root collider/navigation footprint đứng yên. Muzzle theo
  matrix model đang ngắm. Khi snap lại, trạng thái ngắm được reset để không
  giữ scale cũ hoặc tích lũy góc xoay sai.
- Barracks mới tìm ô đi được trong vòng ba ô quanh điểm spawn theo model;
  tránh footprint, ô bị chặn, quân hiện tại và điểm spawn đã giữ trong tick.
  Hết chỗ thì giữ job đã trả tiền/đã hoàn thành; chưa tăng dân số hoặc tạo unit.
  UI báo `Ready - exit blocked`, tự thử lại khi có chỗ. Hành vi này chỉ áp dụng
  producer bật offsetsFollowModel; TownHall cũ giữ hành vi spawn đã có.
- Depot cache không nhận kho có HP <= 0, ngoài các điều kiện hoàn thành/owner/
  loại tài nguyên/island đã có. Worker đang trở về sẽ chọn lại kho hợp lệ.
- Không đổi giá, HP, tốc độ xây/sinh lính hay tài nguyên trong bản sửa này.

Kiểm tra: compiler runtime/Editor; tham chiếu prefab; 27 ca chạy **mã
ConstructionRevealMath thực** với ba scale, ba độ cao nền và ba góc Y. Các ca
kiểm tra đủ tám góc mesh, progress 0/1, clamp và nội suy tăng đều đều đạt.
Đây là kiểm tra toán học ngoài Unity; không chứng minh shader đã render đúng
hoặc tháp/worker/production đã chạy trong Play Mode. Lượt này chưa có ảnh
Game View hoặc kiểm chứng runtime Unity mới.

Sau cập nhật, **thoát Play, đợi biên dịch/import/bake, rồi chạy lại Main**.
Entity đã spawn trong lượt Play cũ không được tự migrate sang component/scale mới.
Kiểm tra tháp hoàn tất hiện đủ model, bắn/đổi hướng mà footprint không xoay;
trại sinh ba lính và tạm chờ khi chặn hết cửa; kho nhận tài nguyên; thử ở nền cao.
Tìm ô spawn có phạm vi giới hạn, chưa bảo đảm tìm đường tới mọi rally point hoặc
đáp ứng đội hình lớn; bố trí chừa lối đi quanh trại. Tháp xoay model chung hiện
có, chưa tách animation riêng phần nòng và chân đế.

## Đã triển khai

| Đối tượng | Dữ liệu/prefab mới | UI và chức năng được nối |
| --- | --- | --- |
| Tháp phòng thủ | Data/Definitions/Buildings/DefenseTower*.asset; Prefabs/Buildings/DefenseTower.prefab | Portrait, HP, hai vũ khí, damage/range/cooldown; tooltip trạng thái target và bán kính AOE; AttackAbilityDefinition dùng combat hiện có |
| Kho | Data/Definitions/Buildings/ResourceDepot*.asset; Prefabs/Buildings/ResourceDepot.prefab | Portrait/HP; Accepts Gold/Wood/Food; StorageAbilityDefinition hiện có cho worker trả tài nguyên về stock chung |
| Trại lính | Data/Definitions/Buildings/Barracks*.asset; Prefabs/Buildings/Barracks.prefab | Portrait/HP; Production available, số queue/capacity; dùng TrainSoldier hiện có, spawn/rally giữ binding của prefab gốc |
| Mỏ tài nguyên | UnitInfoPanel.cs | Stock còn lại/ban đầu và thanh trữ lượng từ MapResourcePrefab khớp DefinitionId/Type/Tier; thiếu/không rõ catalog thì chỉ hiện Remaining, không đoán phần trăm |

Các đường dẫn trong bảng tính từ `Assets/_Project/`.
Thêm ba BuildingSO vào `Resources/GameReg.asset` và build ability của worker
`Tests/Fixtures/TwoCivDogfood/Build.asset`. Giữ TownHall và các offers/jobs cũ.
GameReg có hai dòng cùng TownHall; đã gộp còn một để tránh duplicate registry.

Các prefab mới là bản tích hợp từ PlasmaTower, ResourceDepot và Barracks cũ.
Bỏ binding legacy/missing script/conflicting UnitAuthoring trong **bản mới**;
gán BuildingSO/BasedSO, GridSnapperAuthoring và SingleSelectable ở root.
Giữ toàn bộ prefab legacy tại vị trí cũ, không đổi tên hoặc sửa chúng.

`Prefabs/Buildings/DefenseProjectile.prefab` sao từ Bullet 1, giữ artillery
behavior/AOE, bỏ UnitAuthoring chưa có definition và collider tạo ground blocker.
Giữ LinkedEntityGroup để các phần hình ảnh theo vòng đời entity.
Trong ShootAttackSystem, riêng muzzle của BuildingComponent có PostTransformMatrix
được biến đổi theo matrix co giãn/căn model trước khi ra world. Đường bắn của
unit giữ nguyên; đây là sửa tích hợp tháp vào grid-fit, không đổi damage/range.

Trại lính bật `ProductionAuthoring.offsetsFollowModel`: điểm sinh quân/rally
theo cùng phép co giãn/căn model, tránh bị nằm trong footprint khi cell size đổi.
Cờ mặc định tắt và chỉ bật trên Barracks mới; TownHall và producer cũ giữ đường
tính offset cũ. ProductionSystem chỉ đổi phép tọa độ khi cờ này bật, không đổi
queue, thanh toán, tốc độ sản xuất hoặc dân số.

SelectionStats hiển thị hai vũ khí giống nhau dạng `2 x 10 DMG`, không cộng thành
một phát bắn 20 damage. Các vũ khí khác nhau giữ thông tin riêng trong tooltip.
UI không thêm nút bắn thủ công, nâng cấp, sửa nhà hoặc hủy queue chưa được nối.

## Thông số và căn cứ — chưa coi là balance cuối

| Công trình | HP | Giá | WorkLoad | Footprint vuông | Căn cứ |
| --- | --- | --- | --- | --- | --- |
| Defense Tower | 100 | 100 Gold + 50 Wood | 25 | 3 ô | HP/cost legacy; 5 giây × worker WorkRate 5; footprint chỉnh theo phản hồi ảnh |
| Barracks | 100 | 100 Gold | 25 | 6 ô | HP/cost legacy; 5 giây × rate 5; footprint chỉnh theo phản hồi ảnh |
| Resource Depot | 100 | 200 Wood | 100 | 4 ô | MaxHP legacy; footprint chỉnh theo phản hồi ảnh; **giá/work tạm kế thừa TownHall** |

WorkRate 5 và JobCapacity 5 của producer dùng cấu hình prototype hiện có.
Không thay giá/job/dân số của lính hoặc TownHall. Không bỏ khả năng tạo lính
ở TownHall; Barracks bổ sung nơi sản xuất bằng job chung.
Footprint đã được thu gọn theo phản hồi; cần nghiệm thu lại model/ghost/collider
trong map. Giá/work của kho là quyết định tạm
cần chủ dự án chốt trước khi cân bằng game hoặc trình bày như thiết kế cuối.

Main hiện cấu hình catalog sinh mỏ **gold.common**. UI vẫn hỗ trợ Wood/Food khi
entity tương ứng có thật, nhưng đợt này không sửa phân bố map hoặc thêm mỏ giả.
Không thêm sức chứa kho, HP của mỏ hoặc cơ chế hồi tài nguyên chưa có dữ liệu.

## Kiểm chứng và giới hạn

- Compiler runtime và Editor của Unity 6000.3.13f1 biên dịch đạt.
- Kiểm tra tĩnh 11 asset mới: GUID/local fileID, owner của component, liên kết
  Base/Prefab/Definition, registry và offers hợp lệ; prefab gốc không bị sửa.
- Unity batchmode đã được thử nhưng thoát mã 1 trước khi chạy audit; không tạo
  BuildingIntegrationReport.md. Log ở Temp/BuildingIntegration/unity-validation.log.
  Không kết luận được nguyên nhân từ log hiện có, không báo bake đã đạt.
- Chưa xác nhận import/bake, render vật liệu, chiến đấu, nhận tài nguyên hoặc
  sinh lính trong Play Mode; chưa chạy PC build hay đo hiệu năng.

Trong Unity có thể chạy `Tools > RTS UI > Verify integrated buildings` để
kiểm tra registry, offer, portrait, prefab root, nested missing scripts và
capabilities. Công cụ ghi `Temp/BuildingIntegrationReport.md`; không sửa asset,
không tự đóng Unity và không thay thế bake/Play Mode. Entry RunBatch chỉ dùng
khi gọi chủ động bằng command line.

## Checklist nghiệm thu Main

1. Thoát Play rồi chờ import/bake; chạy menu Verify integrated buildings, đọc
   báo cáo, không có lỗi đỏ mới. Khởi động Main với local player/map sẵn sàng.
2. Chọn worker: ngoài TownHall thấy Defense Tower, Resource Depot, Barracks,
   đúng ảnh/giá/trạng thái thiếu tài nguyên. Đặt mỗi loại tại vùng trống đủ rộng.
3. Trong lúc xây: HP/tiến độ/queue đúng. Hoàn tất mới cho bắn, nhận tài nguyên
   hoặc sản xuất; click UI không ra lệnh world. Kiểm tra footprint/model/material.
4. Chọn tháp hoàn tất: thấy hai vũ khí, DMG/RNG/CD; đưa kẻ địch vào tầm, xác nhận
   chọn mục tiêu, bắn từ muzzle, AOE/damage, không bắn đồng minh; thử phá tháp.
5. Chọn kho: thấy Accepts. Cho worker đang mang Gold trả về kho hoàn tất của mình;
   stock chung tăng đúng một lần. Kho đang xây/kho địch không được chọn làm depot.
   Kiểm tra Wood/Food khi có nguồn hợp lệ; không coi Gold test là đủ cả ba loại.
6. Chọn trại: có Train Soldier; xếp ba job, badge/tiến độ đúng, lính sinh và đi
   ra ngoài footprint, không kẹt trong nhà; kiểm tra thiếu tài nguyên/chờ dân số.
7. Chọn mỏ Gold: hiện stock/catalog ban đầu, thanh giảm khi gather; đây không
   phải HP. Mỏ cạn/đổi lựa chọn không giữ thông tin cũ; nhóm worker vẫn điều khiển được.
8. Kiểm tra hồi quy TownHall/worker/quân, menu và minimap đã được nghiệm thu;
   chụp Game View cho từng loại trước khi đưa vào báo cáo hoàn tất.

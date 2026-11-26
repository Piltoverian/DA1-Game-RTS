# Hoàn thiện UI RTS để báo cáo

## Sửa click phải khai thác — 26/11/2026

Người dùng chọn được worker trong Main nhưng click phải mỏ không khai thác.
Nguyên nhân: UnitController.TryCommandGather chỉ raycast UnityEngine.Physics
và đòi ResourceNodeReference trên GameObject, trong khi tài nguyên Main là
entity đã bake có collider ECS. ResourceInspection vốn đã nhận được ECS.
Gather và inspection nay dùng chung ResourceInspection.TryResolveNode;
giữ kiểm tra worker/local owner và pipeline TargetTo/thanh toán hiện tại.
Không tự cộng tài nguyên hoặc thay tốc độ khai thác.

Unity Play Mode ngày 26/11: **PASS** bộ kiểm tra GatherPointerSmoke. Tia camera
nhận đúng Gold/Wood/Food trong khi đường GameObject cũ đều không hit resource;
không chọn worker thì từ chối lệnh, inspection giữ worker đã chọn; lệnh qua
UnitController chạy tới khai thác thật, mỏ giảm và stock của cả ba loại tăng
sau giao hàng. Biên nhận bền vững: `Artifacts/Verification/GatherPointer/report.txt`.
Chạy lại qua Tools → RTS UI → Verify pointer gathering khi đã Stop Play.

Bộ kiểm tra dùng vị trí màn hình và bộ dispatch runtime, thiết lập selection
cho test; chưa giả lập click chuột hệ điều hành. Smoke vòng chơi ngày 25/11
gửi lệnh ECS trực tiếp nên không bao phủ được lỗi pointer này. Bụi Food thấp
bị TownHall che khuất không thể click xuyên công trình; test chọn nguồn Food
khác nhìn thấy được. Khi nghiệm thu, click phần mỏ/cây nhìn thấy rõ trong Game View.

Bản Windows được build lại ngày 26/11 sau sửa pointer: thành công, 0 lỗi,
17 cảnh báo; `Builds/Windows/ImmortalRTS.exe` và DLL runtime đã được cập nhật.
Kiểm chứng khai thác mới ở trên chạy trong Editor; không coi build lại là
nghiệm thu tương tác chuột thủ công trên Player mới.

## Đợt hoàn thiện vòng chơi — 25/11/2026

Theo quyết định chủ dự án, Main chuyển sang 1 người đấu 1 AI đơn giản. Profile
mới ở Data/Playable/SkirmishMap.asset và SkirmishStart.asset; profile 8 người cũ
giữ làm tham khảo. Có đủ ba loại nguồn tài nguyên, mục tiêu phá TownHall,
thắng/thua, chơi lại và menu restart/exit. Worker/lính tăng tốc 7 → 10; camera
có Shift pan nhanh và giới hạn vùng nhìn theo map.

Kiểm tra actual Play Mode đã bắt được lỗi worker đứng ngoài khoảng tương tác
và worker không thoát trạng thái xây khi nhà đầy HP; đã sửa trong
WorkerInteractionMath/WorkerGatherSystem và BuilderSystem. Kết quả vòng smoke
mới nhất ở Temp/PlayableLoop/smoke-report.txt; chỉ coi PASS khi chạy hết cả
chiến đấu, victory và restart. Biên dịch hoặc các checkpoint đầu chưa đủ để
đánh dấu toàn bộ vòng chơi đạt.

Kết quả Play Mode ngày 25/11: **PASS toàn vòng**, 82.7 giây thực với mô phỏng
kiểm tra 2x (bản game bình thường 1x). Thu thập làm tăng stock và giảm mỏ thật;
xây xong Barracks/Depot/Tower; tạo ba lính theo FIFO, di chuyển trên 5 đơn vị,
nuôi quân tám lính bằng kinh tế; combat phá TownHall và hiện victory; ghi nhận
26 đạn tháp; restart tạo world mới với hai căn cứ, năm worker local, không còn
quân trận trước. Không ghi nhận Error/Exception/Assert mới trong lượt này.
Biên nhận được sao lưu ở `Builds/Windows/Verification/editor-smoke-report.txt`.
36 kiểm tra dùng source toán học reveal/interaction thực cũng đạt. Đây là
smoke quy mô demo, không phải nghiệm thu tải hàng nghìn quân hoặc mọi edge case.

### Bản Windows đã chạy thực tế

- Release Windows x64: `Builds/Windows/ImmortalRTS.exe`, Main duy nhất; build
  thành công với 0 lỗi và 22 cảnh báo. Lỗi Burst BC1028 trong SelectSystem
  được sửa bằng SystemAPI singleton, giữ nguyên logic raycast/quyền sở hữu.
- Khởi động: hai TownHall, mười worker tổng hai bên, ba loại tài nguyên,
  bootstrap Ready; log map xác nhận 12/12 clusters và 33 nodes, missing=0.
- Kiểm tra Player **có cửa sổ/render, 1280×720, simulation 1x**: AI tự thu thập,
  tạo quân và phá TownHall local; màn DEFEAT lúc 02:34. 8.974 mẫu sau warmup
  bốn giây: median 16.6668 ms, p95 16.6705 ms (~60 FPS, đã giới hạn 60).
  GPU NVIDIA GeForce RTX 3050 6GB Laptop GPU; không ngoại suy sang máy khác,
  độ phân giải khác hoặc tải lớn. Report không ghi Error/Exception mới.
- Ảnh màn thua và JSON/log thật ở `Builds/Windows/Verification/VisiblePlayer`;
  ảnh victory/restart và báo cáo Editor trong thư mục Verification gốc.
  Lượt Startup chạy ẩn chỉ kiểm chứng dữ liệu, ảnh đen không dùng để nghiệm thu
  render hoặc hiệu năng. Lượt Defeat chạy ẩn đã dừng, không phải kết quả đạt.

Còn cảnh báo hệ thống ECS cũ về attribute thứ tự giữa các group và cảnh báo
build; không coi build sạch cảnh báo. Native Player cũng ghi Curl error 35 từ
dịch vụ kết nối, trong khi trận offline vẫn hoàn tất. Chưa nghiệm thu mọi
trường hợp hủy queue/chặn lối, trận kéo dài hoặc nhiều trận liên tục trong Player.
Mỹ thuật/animation prototype và các mép địa hình lộ mảng tam giác còn cần polish;
đây là bản demo chơi trọn vòng, chưa phải bản phát hành thương mại hoàn chỉnh.

## Lịch sử đợt công trình và tài nguyên — 05/11/2026

Bản sửa sau ảnh người dùng: footprint tháp/kho/trại là 3/4/6 ô; hiệu ứng xây
dùng bounds mesh thực thay trần +10; tháp ngắm độc lập collider; trại chờ cửa
trống để sinh quân; kho chết bị loại khỏi nơi nhận tài nguyên. Chi tiết kiểm
chứng và giới hạn cập nhật trong BuildingContentIntegration.md. Cần chạy lại
Play để bake/entity mới nhận thay đổi.

Chủ dự án đã nghiệm thu khung điều khiển bốn vùng. Đợt tiếp theo đã tích hợp
ba loại Defense Tower/Resource Depot/Barracks thành asset mới, đăng ký registry
và menu xây; bổ sung thông tin chức năng và thanh trữ lượng mỏ từ catalog thật.
Chi tiết, thông số tạm và checklist: [BuildingContentIntegration.md](BuildingContentIntegration.md).
Tại đợt 05/11: compiler/runtime-reference checks đạt; Unity batch thoát trước khi chạy audit,
nên import/bake/Play Mode của công trình mới vẫn chưa được xác nhận.
Các ma trận và trạng thái đợt cũ bên dưới được giữ làm lịch sử.

## Bổ sung mới nhất: khung điều khiển chung

Theo yêu cầu ngày 05/11/2026, đã triển khai khung dưới bốn vùng: thông tin →
tác vụ → công việc/hàng chờ → minimap phải. Production và hàng xây worker
dùng dữ liệu ECS thực; mục trùng liền nhau có badge số lượng, giữ thứ tự FIFO.
Prompt, ưu tiên, giới hạn và checklist: [CommandDockImplementation.md](CommandDockImplementation.md).
Khung này đã được chủ dự án nghiệm thu; các kết quả bố cục cũ bên dưới là lịch sử.

Tài liệu cập nhật ngày 05/11/2026. Scene mục tiêu: `Assets/_Project/Scenes/Main.unity`.
Phạm vi báo cáo là HUD và tương tác UI cho gameplay hiện có trên PC, dùng uGUI,
chuột/bàn phím, chuẩn 1920×1080. Không coi mọi chi tiết trong concept là tính
năng đã có. Giữ nguyên Gold/Wood/Food và các thông số simulation hiện tại.

## Kết quả của đợt triển khai này

| Phần | Đã triển khai | Trạng thái nghiệm thu |
| --- | --- | --- |
| HUD tài nguyên/dân số/thời gian | Snapshot/event local player, chữ/số, icon, palette/viền | Bản chức năng trước đã được chủ dự án kiểm tra; bản style và sửa override tài nguyên còn cần xem lại |
| Bảng lựa chọn | Tên/ảnh/HP, số chọn, worker/cargo, tiến độ xây, local player/age | Đợt ngữ cảnh trước được chủ dự án xác nhận xong; vị trí mới cạnh minimap cần xem lại |
| Mỏ tài nguyên | Click trái xem tên loại/ảnh, lượng còn lại, tier; giữ nhóm quân để click phải gather; Escape đóng inspection | Đã viết mã/gắn prefab, biên dịch đạt; chưa nghiệm thu Play Mode |
| Thông tin quân/công trình | Bổ sung summary theo StorageResource, PopulationCapacity, AttackBlob và tooltip chi tiết theo vũ khí | Mã dùng dữ liệu ECS; chưa có bằng chứng render/thông số trên mọi loại đối tượng |
| Minimap | Tổng quan địa hình từ GridTerrain; marker quân/công trình/mỏ; màu team/owner; khung camera; click/kéo trái đưa camera đến vị trí trong map | Đã viết mã/gắn prefab và bật container trong Main; chưa nghiệm thu Play Mode |
| Hàng đợi sản xuất | Icon/job hiện tại, % và thời gian còn lại ở rate hiện tại, hai tên chờ tiếp theo và số bổ sung; chờ dân số/paused/under construction | Chỉ đọc queue thực; chưa nghiệm thu Play Mode; chưa có lệnh hủy |
| Input và tooltip | Giữ chặn world input qua HUD; ẩn menu/queue khi xem mỏ; chặn Delete lên lựa chọn ẩn; xóa tooltip khi bảng trống | Cần kiểm tra hồi quy thực tế cho vùng mới |
| Mã chẩn đoán tạm | Đã bỏ HudStyleAuditTemporary.cs và meta do agent tạo | Kiểm tra file đã không còn |

Không ghi “UI hoàn tất 100%” khi các dòng Play Mode còn chưa được nghiệm thu.
Compiler thành công không thay thế nghiệm thu Unity hoặc bản build PC.

## Các file triển khai chính

- `Assets/_Project/Scripts/Runtime/Presentation/UI/ResourceInspection.cs`: focus
  chỉ phục vụ xem mỏ, tách khỏi Selected và quyền điều khiển. Ưu tiên ECS raycast;
  fallback qua collider có ResourceNodeReference và node gần vị trí đó như
  đường gather hiện có. Không cho kiểm tra template chưa config hoặc node cạn.
- `Assets/_Project/Scripts/Runtime/Presentation/Input/GameManager/Manager/SelectManager.cs`:
  click mỏ mở inspection, không gửi yêu cầu selection tương ứng; click đối tượng
  khác/drag chọn lại xóa inspection. Giữ thuật toán chọn simulation cũ.
- `Assets/_Project/Scripts/Runtime/Presentation/UI/UnitInfoPanel.cs`: thông tin
  mỏ và integration summary/tooltip; Escape đóng inspection. Mỏ không hiển thị
  HP giả hoặc % dựa trên stock gốc chưa được resolve.
- `Assets/_Project/Scripts/Runtime/Presentation/UI/SelectionStats.cs`: đọc loại,
  owner, dân số đóng góp, tài nguyên nhận, damage/range/cooldown từ dữ liệu thật.
  Không cộng damage nhiều vũ khí thành một con số không có ý nghĩa.
- `Assets/_Project/Scripts/Runtime/Presentation/UI/TacticalMinimap.cs`: uGUI
  Graphic lấy grid/ECS trực tiếp. Giữ các asset/camera minimap cũ làm tham khảo;
  bản Main mới không phụ thuộc camera đi theo X/Z trong GameScene.
- `Assets/_Project/Scripts/Runtime/Presentation/UI/ProductionQueuePanel.cs`:
  chỉ đọc, không thay enqueue/payment/refund. Vị trí tự theo chiều cao panel lệnh
  để queue không đè lên menu nhiều hàng.
- `Assets/_Project/Prefabs/UI prefab/UICanvas.prefab`: minimap trái dưới, panel
  compact kế bên, bảng queue phía trên, icon và tooltip binding.
- `Assets/_Project/Scenes/Main.unity`: bật lại container minimap; giữ các override
  còn lại ngoài hai giá trị active/enabled của container trong đợt này.

## Ma trận công trình: tránh nhầm UI với tích hợp gameplay

| Đối tượng | Bằng chứng | Việc còn lại trước khi demo như công trình hoạt động |
| --- | --- | --- |
| Town Hall | GameReg.Buildings và Build ability hiện có TownHall; definition/ảnh đã gắn | Nghiệm thu queue, summary population/storage và bố cục mới |
| Tháp phòng thủ | Có TowerDefend/TowerDefend2/TowerDefendEnemy/PlasmaTower, ảnh Tower/PlasmaTower; có combat runtime AttackBlob | Chọn một prefab, chuyển binding authoring cũ sang BuildingSO/BasedSO hợp lệ, đăng ký registry/build offer nếu cần cho xây, kiểm tra bake/combat; rồi nghiệm thu nhánh UI vừa thêm |
| Kho tài nguyên | Có ResourceDepot prefab/ảnh và StorageResource capability | Kiểm tra/tích hợp definition/authoring vào Main, rồi kiểm tra Accepts từ buffer; không tự tạo chỉ số sức chứa kho |
| Trại lính | Có Barracks prefab, Barrack.png, production pipeline | Kiểm tra/tích hợp definition/jobs/unlock vào Main; queue UI đã có đường đọc chung |
| Nhà tăng dân số | Có BuildingTags.House và PopulationCapacity trong schema | Chưa xác nhận asset/runtime cụ thể cho Main; không báo đã có nhà hoạt động |

GameReg hiện chỉ đăng ký một loại công trình. Các prefab legacy còn field/tên
component cũ; BuildingAuthoring hiện yêu cầu buildingDef và không cho cùng entity
với UnitAuthoring. Đợt UI này không tự migrate các prefab đó hoặc đặt balance mới.
UI thông số dùng được cho entity bake hợp lệ; không khẳng định UI tháp đã demo
trong Main khi chưa có công trình phòng thủ hợp lệ trong scene/offer.

## Kế hoạch còn lại, theo thứ tự để chốt báo cáo

| Mốc | Ưu tiên | Công việc | Đầu ra/điều kiện đóng mốc |
| --- | --- | --- | --- |
| 1 | P0 | Nghiệm thu UI mới trong Main và sửa lỗi phát hiện | Hoàn thành checklist bên dưới, giữ Console sạch lỗi mới; chốt icon/layout tài nguyên |
| 2 | P0 | Tích hợp một tháp phòng thủ hợp lệ nếu báo cáo cần demo tháp | Có definition/authoring/bake đúng và kiểm tra combat trước; tên/ảnh/HP/stats UI khớp. Đây gồm việc dữ liệu gameplay, không chỉ trang trí UI |
| 3 | P1 | Kho/trại lính theo danh sách công trình thực sự cần báo cáo | Mỗi loại có scene hoặc build offer hợp lệ, thông tin và lệnh đúng; loại chưa dùng ghi ngoài phạm vi |
| 4 | P1 | Nghiệm thu độ phân giải và vòng đời | 1080p, 720p, 4:3, ultrawide; text dài, đổi chọn, node cạn, tắt/bật HUD, vào lại Play |
| 5 | P1 | Build PC và thu bằng chứng báo cáo | Player build chạy được, chụp Game View/build thật cho các trạng thái; ghi phiên bản và giới hạn |
| 6 | P2 | Hoàn thiện có chọn lọc | Nhóm theo loại, menu/settings và pause theo quyết định, phím tắt, UI scale; chỉ đưa vào scope nếu cần |

Minimap bản đầu không có fog of war/ping/lệnh chuột phải. Queue chưa có hủy.
Objectives, thắng/thua, mana/kỹ năng chủ động và màn hình nghiên cứu đầy đủ chưa
được thêm. Không đưa chúng vào điều kiện “xong HUD cốt lõi” chỉ để giống concept.

## Checklist nghiệm thu — chưa đánh dấu thay người kiểm tra

- [ ] Thoát Play, chờ import xong, chạy Main; không có lỗi đỏ mới.
- [ ] Gold/Wood/Food đủ ảnh/nhãn/số, không mất hoặc lệch khỏi thanh trên.
- [ ] Minimap hiện địa hình và các marker sau khi map/player khởi tạo; không
  chỉ là ô trống hoặc texture cũ. Hướng trên là +Z, bản đồ giữ đúng tỷ lệ.
- [ ] Click/kéo trái trong minimap di chuyển camera đúng vị trí, không đổi nhóm
  chọn, không phát Move/Gather xuống world; chuột phải trên minimap không ra lệnh.
- [ ] Khung trắng phản ánh vùng camera; so với vị trí thật ở giữa và sát mép map.
- [ ] Chọn worker, click trái mỏ: hiện loại/Remaining/Tier, menu build/queue ẩn.
  Click phải mỏ vẫn giao gather cho worker đã chọn; số còn lại giảm đúng dữ liệu.
- [ ] Escape khi xem mỏ quay lại thông tin nhóm trước; click đối tượng khác/drag
  vẫn hoạt động. Delete khi xem mỏ không hủy công trình đang giữ lựa chọn ẩn.
- [ ] Mỏ cạn/bị hủy, đổi World/local player hoặc tắt HUD: không lỗi entity cũ.
- [ ] Chọn Town Hall: tên/ảnh/HP/Completed và summary population/storage đúng;
  hover card thấy thông tin chi tiết, không còn tooltip sau khi bỏ chọn.
- [ ] Train một vài job: queue xuất hiện, tiến độ cập nhật, job hoàn thành biến
  mất; đầy dân số hiển thị chờ, không tự trừ thêm tiền hoặc khởi động lại work.
- [ ] Chọn Soldier đã có attack definition: summary/tooltip đọc đúng damage,
  range và cooldown; không dùng phép thử này để coi tháp phòng thủ đã đạt.
- [ ] Bảng selection/queue không đè minimap hoặc menu nhiều hàng; bảng rỗng ẩn.
- [ ] Kiểm tra 1080p/720p và các tỷ lệ khác; chữ nhỏ, tooltip mép, số dài, mất focus.
- [ ] Chạy player build PC, thu ảnh và ghi kết quả thực tế.

Minimap vẽ lưới lấy mẫu 48×48, refresh marker mặc định 0,2 giây, tối đa 2048
marker; không tạo một GameObject cho mỗi marker. Khung camera chiếu xuống mặt
phẳng grid, có thể khác biên nhìn theo địa hình cao. Chưa đo FPS/GC trong Unity.

## Kịch bản trình bày khoảng 5 phút

1. Giới thiệu mục tiêu và ảnh concept: bố cục compact, ngôn ngữ hình ảnh tu tiên,
   dữ liệu thực tế vẫn là Gold/Wood/Food, không thay gameplay chỉ để giống ảnh.
2. Chạy Main: chỉ thanh trên, local player/time và minimap. Thao tác điều hướng.
3. Chọn worker, giao gather, xem mỏ: giải thích dữ liệu còn lại và việc giữ nhóm.
4. Chọn Town Hall, train: trình bày chi phí/khóa/tooltip, queue và tiến độ.
5. Chọn quân chiến đấu và công trình đã được tích hợp hợp lệ: thông số lấy từ
   runtime. Nếu chưa tích hợp tháp thì ghi rõ đó là hạng mục còn lại, không demo
   prefab cũ như tính năng đã hoàn tất.
6. Nêu kết quả nghiệm thu thực tế và giới hạn. Chỉ dùng ảnh/số đo đã thu được.

Đoạn mô tả có thể dùng trong báo cáo sau khi bổ sung kết quả nghiệm thu:

> Giao diện được phát triển trên uGUI hiện có, liên kết dữ liệu ECS của local
> player và đối tượng đang xem. Phạm vi triển khai gồm HUD tài nguyên/dân số/
> thời gian, bảng lựa chọn theo ngữ cảnh, thông tin mỏ, menu lệnh và tooltip,
> tổng quan minimap có điều hướng camera, cùng bảng hàng đợi sản xuất chỉ đọc.
> UI xem đối tượng trung lập được tách khỏi quyền điều khiển. Các công trình
> legacy cần tích hợp definition/authoring hợp lệ trước khi nghiệm thu trong
> trận; các tính năng ngoài phạm vi được liệt kê riêng.

## Bằng chứng kiểm chứng của agent

### Đợt hoàn thiện để nghiệm thu một lần

- Cụm thông tin/lệnh/tooltip AOE được chủ dự án nghiệm thu trước đợt này.
- Bổ sung HudSystemMenu vào UICanvas: Menu hoặc Escape mở; Resume/Escape đóng;
  phím lên/xuống chọn nút, Submit theo Input System UI hiện tại. Escape khi
  placement/xem mỏ giữ ưu tiên hủy placement/đóng inspection trước mở menu.
- Menu có cỡ UI 80–120% (lưu khóa PlayerPrefs RTS.UI.Scale), bật/tắt âm thanh và
  hướng dẫn chuột. Không có nút save/load/quit giả. Không đổi Time.timeScale;
  luôn ghi rõ simulation vẫn chạy. Không thay đổi tên thời đại hoặc tài nguyên.
- Modal chặn selection/lệnh/camera; đóng menu khóa input đến lúc thả chuột.
  Tooltip đang hiện bị ẩn trong modal. Khôi phục focus khi đối tượng cũ còn tồn tại.
- Tooltip nhóm có số lượng theo loại, tối đa bốn loại kèm số loại còn lại;
  HP/công việc vẫn là thông tin đại diện, không biến thành tổng giả của nhóm.
- Nút sản xuất có số job cùng loại đang xếp hàng, tách khỏi dấu !/OK. Không
  thêm hủy queue, refund hoặc sửa luật simulation.
- Có menu Editor Tools > RTS UI > Export content readiness report để xuất
  Temp/HudContentReadiness.md từ registry và prefab thực, không sửa asset.
  Công cụ này đã biên dịch nhưng agent chưa chạy trong Unity.
- Compiler runtime và Editor đều exit 0; tham chiếu local prefab hợp lệ, không
  trùng fileID. Tính hình học canvas ở 1080p/720p/4:3/ultrawide với UI 120% đạt
  giới hạn card/menu. Đây không phải kiểm tra render/font/hitbox trong Game View.

Checklist bổ sung cho lượt nghiệm thu cuối:

- [ ] Menu mở/đóng bằng nút và Escape, focus bàn phím đúng; không phát lệnh world.
- [ ] Khi mở menu, camera không pan/zoom; game vẫn chạy như dòng thông báo.
- [ ] Đặt nhà hoặc xem mỏ rồi Escape: hành vi cũ giữ đúng ưu tiên, không mở menu cùng lúc.
- [ ] Thử UI 80/100/120%, mở lại game kiểm tra lưu cỡ; panel/queue/tooltip không chồng nhau.
- [ ] Toggle sound tắt rồi bật lại; Resume giữ nhóm chọn và không click xuyên UI.
- [ ] Chọn nhóm nhiều loại: tooltip đếm đúng; queue badge đổi theo job thực tế.

### Phạm vi chưa thể ghi là hoàn tất

UI trên dữ liệu hiện có đã được mở rộng để nghiệm thu tập trung. Chưa có cơ sở
đánh dấu **toàn bộ kế hoạch** hoàn thành: tháp/kho/trại lính legacy chưa được
migrate và đăng ký thành gameplay hợp lệ trong Main; NewBuildingSO.asset hiện
chưa gán BasedSO. Việc đó cần thêm authoring/definition/projectile/job hợp lệ,
không được giải quyết bằng nhãn UI hoặc lấy field cũ làm dữ liệu runtime.
Giữ các asset này nguyên trạng, không tự đặt balance để làm đủ màn hình.

Player build, đo FPS/GC và nghiệm thu Game View toàn bộ vẫn chưa chạy. Không
thêm fog/ping, mana/kỹ năng, objectives/thắng thua, pause simulation hoặc hủy
queue khi gameplay tương ứng chưa được xác định. Những dòng này là phần còn
lại hoặc ngoài phạm vi, không phải tính năng đã có chỉ vì concept có hình.

- Compiler C# đi kèm Unity 6000.3.13f1: runtime biên dịch thành công sau các
  thay đổi. Có warning obsolete ShootVictimAuthoring và thông báo SGFE009 cũ.
- Tham chiếu local trong prefab hợp lệ, GUID script mới không trùng; công cụ
  audit tạm đã được dọn; diff không có lỗi whitespace.
- Hash CommandQueue, SelectSystem, ResourceNodeDepletedSystem, GameReg và
  ProjectSettings.asset giữ nguyên trong đợt này; UI không sửa các luật đó.
- Chưa nghiệm thu Unity cho các phần mới: kích hoạt cửa sổ Unity bị timeout,
  ảnh trả về không phải nội dung Unity đáng tin cậy nên agent dừng thao tác.
  Chưa chạy build PC hoặc đo hiệu năng. Không có ảnh Game View mới làm bằng chứng.

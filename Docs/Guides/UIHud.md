# HUD local player — đợt đầu

## Prompt triển khai

Bạn là Unity UI Engineer C#. Hoàn thiện thanh tài nguyên, dân số và thời gian
của local player trong Main, dựa trên uGUI/TextMeshPro/EventBus hiện có.
Lấy 1920×1080 làm bố cục chuẩn và dùng anchor/layout để co giãn.
Giữ Gold/Wood/Food, Pop và nội dung prototype; dùng hướng màu ngọc tối/chữ ngà
của concept tu tiên. Đưa nhãn và định dạng vào trường Inspector để có thể
nối bản địa hóa sau, chưa cài framework localization.

Hiển thị đúng snapshot khi bootstrap sẵn sàng, đổi local player hoặc bật lại HUD;
cập nhật tiếp theo qua EventBus. Không dùng player ID cố định để hiển thị dữ liệu.
Giữ thứ tự Gold–Wood–Food, tái sử dụng ô tài nguyên, hiển thị -- khi chưa có dữ liệu.
Đồng hồ dùng thời gian simulation, định dạng Time: mm:ss và chỉ đổi text mỗi giây.
Xử lý ECS World chưa có/đã hủy/thay thế; giải phóng query khi tắt component.

Giữ nguyên gameplay, selection, pause, các scene tham khảo và asset cũ.
Chưa triển khai minimap/menu/queue, chưa thay framework hoặc sửa simulation.
Kiểm tra biên dịch, tham chiếu prefab, và ghi rõ phần chưa nghiệm thu Play Mode.

## File và luồng dữ liệu

- Scene đích: `Assets/_Project/Scenes/Main.unity`. UIManager hiện là component
  được Main thêm vào instance UICanvas, không tự thêm manager vào scene tham khảo.
- HUD dùng chung: `Assets/_Project/Prefabs/UI prefab/UICanvas.prefab`.
  Thay đổi diện mạo prefab cũng ảnh hưởng các instance ở scene khác.
- `Resource Info.prefab` là ô tài nguyên riêng, tạo từ cấu trúc Info Group.
  Giữ Info Group và GUID cũ cho bảng chỉ số lựa chọn; giữ ô thứ tư nhưng tắt.
- `Presentation/UI/UIManager.cs`: resolve LocalTestPlayer, đợi bootstrap Ready,
  tìm PlayerContext tương ứng; snapshot khi bind/re-enable/thay context.
  Scene chưa có local identity bake dùng TestPlayer đang hoạt động giống SelectManager.
- `UIComponent.cs`: quản lý bind/unavailable và yêu cầu snapshot khi bật lại.
- `ResourceGroup.cs`, `PopText.cs`: nhận snapshot rồi nhận sự kiện đã nối sẵn.
- `MatchTimeTextMesh.cs`: query MatchTimeComponent riêng, không thay tốc độ simulation.
- `InfoPanel.cs`: thêm cách gán text an toàn, giữ API giá trị/icon hiện có.

Nhãn/format chỉnh trên ResourceGroup, PopText, MatchTimeTextMesh trong Inspector.
Các trường này chuẩn bị cho bước localization; chưa phải hệ thống đa ngôn ngữ.
Chưa có icon tài nguyên được duyệt nên dùng tên tài nguyên kèm số, ẩn icon trống.
Player info/IndustrialAge, bảng lựa chọn và Menu vẫn là phần prototype,
không được coi là tính năng đã hoàn thiện trong đợt này.

## Bố cục

CanvasScaler: Scale With Screen Size, 1920×1080, Match Width/Height 0.5.
Thanh trên cao 72 đơn vị canvas; tài nguyên chiếm 48% bề ngang,
dân số 14%, Player info prototype 14%, thời gian 14%, vùng Menu cũ 10%.
Nền xanh ngọc tối, chữ ngà 24; layout group kiểm soát kích thước ô tài nguyên.
Các phần HUD ngoài thanh trên chưa được thiết kế lại; CanvasScaler chung có thể
thay đổi kích thước hiển thị của chúng, cần nghiệm thu cùng selection canvas.

## Kiểm chứng và giới hạn

Biên dịch toàn bộ runtime C# bằng compiler đi kèm Unity 6000.3.13f1 và response
file hiện có của Bee, chuyển đầu ra vào `Temp/HudVerification`: exit 0.
Còn cảnh báo obsolete ở ShootVictimAuthoring ngoài phạm vi thay đổi.
Đây là kiểm tra biên dịch C#, không phải Unity import, player build hoặc Play Mode.
Build trực tiếp csproj chưa dùng được vì thiếu DLL project phụ thuộc tại Temp/bin.

Tại thời điểm triển khai, agent chưa nghiệm thu trực quan hoặc chạy gameplay.
Ngày 16/10/2026, chủ dự án xác nhận đã kiểm tra trong Unity và HUD
tài nguyên–dân số–thời gian đạt bản đầu. Xác nhận này không tự động có nghĩa
toàn bộ trường hợp trong checklist dưới đây đã được kiểm tra riêng lẻ.

Checklist đối chiếu khi cần kiểm tra lại:

1. Mở Main, Game View 1920×1080: ba ô Gold/Wood/Food, Pop và Time không chồng.
2. Play từ đầu: -- chuyển sang đúng giá trị bootstrap, không cần đợi tài nguyên đổi.
3. Thu/chi tài nguyên, thay đổi dân số: số đúng với PlayerContext; sự kiện player khác
   không đổi HUD. Giữ cùng các instance ô, không Instantiate/Destroy mỗi lần đổi số.
4. Tắt/bật UICanvas và từng ResourceGroup/Pop: lấy lại snapshot mới nhất dù đã bỏ lỡ event.
5. Thay LocalTestPlayer.PlayerId trong chế độ kiểm tra: chuyển toàn bộ số liệu đúng player.
6. Bootstrap chưa Ready hoặc local identity không hợp lệ: hiện --, không giữ số player cũ.
7. Clock: 59→01:00, trên 60 phút tiếp tục tăng tổng số phút; không đổi tốc độ simulation.
8. Thử 1280×720, 2560×1440, 1024×768 và ultrawide: kiểm tra chữ, vùng bấm, thanh trên,
   panel dưới và kéo chọn. Chưa cam kết toàn bộ HUD cũ đã đạt mọi tỷ lệ màn hình.
9. Vào/thoát Play nhiều lần, Domain Reload bật/tắt: không lỗi query từ World cũ.
10. Đối chiếu chọn/xây/train hiện có; không đổi hợp đồng gameplay hay pause.

## Việc UI còn lại

- HUD tài nguyên–dân số–thời gian: đã được chủ dự án nghiệm thu bản đầu trong Unity.
- Polish màu sắc: còn lại, ưu tiên P2 sau khi bố cục/trạng thái HUD cốt lõi ổn định.
  Đối chiếu nền terrain, màu chữ, trạng thái tương tác và concept tu tiên;
  kiểm tra độ tương phản thực tế. Chưa đổi màu trong lượt lập kế hoạch 16/10/2026.
- Panel thông tin lựa chọn: đã triển khai bản mã đầu ngày 16/10/2026,
  chủ dự án phản hồi panel quá lớn và ảnh chưa nhận diện được. Đã sửa bản compact
  theo phản hồi; biên dịch đạt, chờ nghiệm thu lại trong Unity (chi tiết cuối trang).
- Sau panel lựa chọn: xử lý input xuyên HUD/placement, rồi trạng thái menu lệnh.
- Minimap, menu và queue: tiếp tục hoãn khỏi đợt UI cốt lõi này.

## Kế hoạch tiếp theo: panel thông tin lựa chọn

Kế hoạch dưới đây được lập trước khi triển khai. Bản mã đầu đã được triển khai
theo yêu cầu tiếp theo; xem kết quả và giới hạn bên dưới.

Căn cứ source hiện tại:

- UnitInfoPanel.Update hủy/tạo InfoPanel mỗi frame, tạo query và NativeArray
  không thấy giải phóng, chỉ đọc MaxHP; chưa lọc theo local player.
- UnitImage dùng SelectHelper.GetFirstSelectedEntityByplayerID, khác đường đọc
  của UnitInfoPanel; chân dung và chỉ số có thể lấy hai đối tượng khác nhau.
- EntityHealth có CurrentHP và MaxHP; EntityPresentation đã có tra icon/tên qua ID.
- CommandMenu cũng dùng GetFirstSelectedEntityByplayerID. Giữ quy tắc đại diện
  tương thích đường đọc này, không tự đổi thứ tự chọn hoặc thực thể nhận lệnh.

Phạm vi: tên, chân dung, HP hiện tại/tối đa, trạng thái không chọn/chưa sẵn sàng;
chọn nhiều dùng một đại diện và số lượng. HP ghi rõ thuộc đại diện, không cộng
máu nhóm. Chưa thêm chỉ số/khả năng, thanh tiến độ xây dựng hoặc danh sách chân dung.

| Bước | File dự kiến | Tiêu chí nghiệm thu | Phụ thuộc/rủi ro |
|---|---|---|---|
| 1. Ghi nhận panel trong Main và kiểm tra override | Main.unity, UICanvas.prefab | Xác định đúng instance/child cần sửa, giữ thanh trên đã đạt | Prefab dùng chung ảnh hưởng scene tham khảo |
| 2. Thống nhất dữ liệu lựa chọn cho các view | UnitInfoPanel.cs, UnitImage.cs; đối chiếu SelectHelper | Tên/icon/HP cùng entity của local player; không sửa selection hoặc nguồn nhận lệnh | Không tự chọn đại diện bằng quy tắc khác CommandMenu |
| 3. Tái sử dụng view, quản lý vòng đời query | UnitInfoPanel.cs, UnitImage.cs | Không Instantiate/Destroy mỗi frame; query/NativeArray được quản lý; mất World/entity không gây lỗi | Không suy diễn mức cải thiện FPS khi chưa đo |
| 4. Hoàn thiện trạng thái và bố cục | UICanvas.prefab; Info Group.prefab nếu cần | Không chọn, chọn đơn, chọn nhóm, thiếu icon/HP có biểu diễn rõ; màu giữ nguyên | Info Group còn dùng cho chỉ số; không ảnh hưởng Resource Info |
| 5. Kiểm chứng trong Unity | Main và các script/prefab đã sửa | HP đổi khi bị thương/hồi; đổi selection/player và entity chết không giữ dữ liệu cũ; không lệch menu lệnh | Registry có thể chưa sẵn sàng; không mở rộng sửa vòng đời registry nếu chưa tái hiện |

Kiểm tra bố cục tối thiểu ở 1920×1080 và 1280×720; đối chiếu thêm 4:3/ultrawide
theo môi trường có sẵn. Nghiệm thu chọn đơn unit/công trình, nhóm cùng/khác loại,
bỏ chọn, thực thể bị hủy, bật/tắt panel và vào/thoát Play. Không đổi gameplay,
pause, màu sắc hoặc triển khai các panel đã hoãn.

Hướng triển khai nhỏ: UnitInfoPanel điều phối việc đọc lựa chọn; UnitImage nhận dữ
liệu để hiển thị thay vì tự query riêng. Chỉ thêm helper presentation nhỏ nếu cần,
không đưa state UI vào ECS và không đổi framework.

## Panel lựa chọn — bản mã đầu, 16/10/2026

Đã sửa `UnitInfoPanel.cs`, `UnitImage.cs` và phần panel dưới của `UICanvas.prefab`.
Giữ GUID/type và tham chiếu InfoPanelPrefab cũ (ẩn trong Inspector); không sửa
Info Group, Resource Info, SelectHelper, CommandMenu, simulation hoặc scene.

- UnitInfoPanel đọc một snapshot selection mỗi frame, từ query Selected được cache.
  Lọc Selectable.playerID và lấy entity đầu tiên theo cùng quy tắc query của
  SelectHelper mà CommandMenu dùng; không tự sắp xếp hay gán lại selection.
- Tên, icon và HP cùng lấy từ representative này. UnitImage chỉ còn là view,
  không tự query ECS. Tên/icon đọc lại khi entity/definition/registry đổi.
- Hiển thị HP hiện tại/tối đa; nhóm có nhãn Representative HP và Selected: N.
  Đây là HP đại diện, không phải tổng HP nhóm. Không thêm cơ chế leader.
- Không có local player: Waiting for player...; không chọn: No selection;
  thiếu tên/definition dùng fallback, thiếu ảnh: No portrait; thiếu HP: HP: --/--.
  Thiếu registry vẫn có thể hiển thị DefinitionID làm tên theo fallback hiện có.
- Bốn TMP view được lưu sẵn trong prefab, không Instantiate/Destroy mỗi frame.
  Query được giải phóng khi tắt component; NativeArray selection dùng using.
  Có guard world/entity/component và xử lý World được thay thế.
- Vùng thông tin rộng 320, chân dung 96×96; cột chữ rộng 192.
  Toàn khối dưới rộng 820 thay cho 700; lưới lệnh được dịch từ x=200 sang x=320,
  giữ kích thước 500×250 và hành vi lệnh. Layout group cũ của cột chỉ số được giữ
  nhưng tắt vì ba dòng chữ mới có RectTransform riêng.
- Giữ nguyên giá trị màu của mọi component cũ. Chữ mới dùng màu prototype
  từ Info Group; polish màu vẫn nằm trong danh sách còn lại.

Trong prefab, tìm các child SelectionName, SelectionHealth, SelectionCount
dưới component UnitInfoPanel; PortraitFallback nằm dưới UnitImage.
Nhãn/format có thể chỉnh trên component trong Inspector; chưa cài localization.

Kiểm chứng đã làm: compiler Unity 6000.3.13f1 biên dịch runtime exit 0;
còn cảnh báo obsolete ở ShootVictimAuthoring ngoài phạm vi UI. Kiểm tra tĩnh
tham chiếu local/fileID prefab không trùng/thiếu, các block thanh trên không đổi,
giá trị màu cũ và file HUD tài nguyên–dân số–thời gian/settings được giữ nguyên.
Đây không phải kết quả Unity import, player build hoặc gameplay test.

Chờ nghiệm thu trong Main:

1. 1920×1080: bỏ chọn → chọn unit → chọn công trình; tên/ảnh/HP cùng đối tượng.
2. Chọn nhóm cùng/khác loại: Selected: N đúng, HP ghi rõ là của đại diện;
   so với đối tượng mà menu lệnh đang hiển thị.
3. Đối tượng mất/hồi HP: số đổi khi vẫn giữ selection. Đối tượng bị hủy hoặc
   bỏ chọn: thông tin cũ biến mất, không có lỗi Console.
4. Đổi local player, tắt/bật panel, vào/thoát Play: không giữ thông tin player cũ.
5. Trường hợp thiếu icon/registry/HP: fallback rõ, không tạo số liệu giả.
6. 1280×720, 4:3 và ultrawide khi có: kiểm tra chữ dài, chân dung không méo,
   panel dưới/lưới lệnh không chồng hoặc che thanh trên. Kiểm tra click/drag chọn
   và thao tác lệnh vẫn như trước.
7. Hierarchy: các view không tăng số lượng khi giữ selection hoặc HP thay đổi.

Chưa có số đo GC/FPS hoặc nghiệm thu Play Mode từ agent cho bản panel này.

## Panel compact và nhận diện — sửa theo hai ảnh phản hồi

Phản hồi thực tế: nền 820×250 quá lớn, ô lệnh 125×125 dành quá nhiều chỗ,
Worker hiện No portrait/ID và Town Hall/Soldier dùng ảnh màu trơn.
Đây là phần chưa đạt của bản trước, không coi kiểm tra bố cục tĩnh là nghiệm thu UX.

Đã triển khai:

- Info card 280×144; ảnh 88×104 giữ tỷ lệ; tên/HP/số lượng xếp trong cột 164.
  Bổ sung thanh HP ngang 164×8, dùng CurrentHP/MaxHP thực, không dùng số đã làm tròn
  để tính tỷ lệ. Chọn nhóm dùng nhãn Rep. HP, giữ HP của đại diện.
- Nút lệnh 72×72, icon 40×40 và nhãn mục tiêu 12pt phía dưới; gap 8, tối đa 4 cột.
  Chọn 72 thay vì 56–64 để có chỗ cho nhãn, giảm nguy cơ chỉ còn icon khó hiểu.
- CommandMenu tính kích thước vùng lệnh từ số nút thực: một lệnh → panel 360×144,
  hai lệnh → 440×144, bốn lệnh → 600×144, năm đến tám lệnh → 600×168.
  Không dành vùng rộng 500 dù chỉ có một lệnh. Root không vẽ nền trắng lớn;
  chỉ info card và vùng lệnh có nội dung vẽ nền. Khi không có selection, ẩn các
  graphic nhưng giữ controller hoạt động để nhận lựa chọn tiếp theo.
- Giữ mọi giá trị màu của component cũ; thanh HP dùng màu cyan/white có sẵn.
  Chưa polish palette. Không bổ sung queue/minimap/menu hoặc thông số gameplay mới.

Nguồn ảnh và tên:

- Worker.prefab hiện dùng mesh Zombie_Walk_0; Soldier.prefab dùng Soldier_Aim_0;
  TownHall.prefab chứa model rauma-town-hall/source/kaupungintalo.fbx.
- Đã xem và đối chiếu ảnh có sẵn Art/UI/Sprites/Worker.png, Soldier.png, TownHall.png.
  Gán chúng vào WorkerBase/TownHallBase/SoldierBase và TrainWorker/TrainSoldier
  trong Tests/Fixtures/TwoCivDogfood. Chỉ đổi Icon, giữ ID/Name/cost/prefab/thông số.
  Giữ nguyên file ảnh fixture màu trơn; chưa tạo hoặc chỉnh ảnh.
- WorkerBase.Name trên đĩa là Worker nhưng ảnh phản hồi hiện dogfood.worker:
  đây là dấu hiệu lookup runtime fallback, chưa xác định được chính xác trạng thái
  static registry trong phiên Unity đã chụp. Không kết luận chỉ thiếu tên trong asset.
- UnitInfoPanel và CommandMenu được gán GameReg trực tiếp qua trường
  presentationRegistry trong UICanvas. EntityPresentation nhận registry ưu tiên
  cho tên/icon; nếu không gán vẫn dùng RegistryAuthoring.Instance như trước.
  Không đổi Baker, publication static, registry ECS, BuildingPlacer hoặc CommandButton.
  Bản này không tự tải registry từ Resources và không viết tên Worker cứng vào code.
- JobIcon có fallback từ Train.OutputUnit.basedSO.Icon khi job thiếu ảnh;
  thiếu icon khác thì ẩn Image trống và giữ nhãn. Tile train dùng tên unit đầu ra,
  tile xây dùng tên công trình; tooltip hành động/chi phí đầy đủ còn ở bước menu lệnh.

File sửa trong đợt compact: UnitInfoPanel.cs, EntityPresentation.cs, CommandMenu.cs,
UICanvas.prefab, NonNavigationButton.prefab và năm asset icon binding kể trên.
Giữ các file thanh tài nguyên/dân số/thời gian, UIManager, Main và ProjectSettings.

Kiểm chứng: compiler Unity 6000.3.13f1 biên dịch runtime exit 0, còn cảnh báo
obsolete ShootVictimAuthoring cũ. Tham chiếu local/fileID và HP Filled sprite hợp lệ;
block thanh trên và màu cũ được đối chiếu với snapshot trước sửa. Kiểm tra hình học
cho 0/1/2/4/5/8/12 nút; kích thước canvas 1920×1080, 1280×720, 1024×768, 3440×1440.
Đây là kiểm tra tĩnh, chưa xác nhận wrap chữ, chất lượng ảnh và hitbox trong Game View.

Checklist nghiệm thu lại trong Main:

1. Worker hiện tên Worker và ảnh prototype đúng model; Town Hall/Soldier không còn
   dùng ô màu trơn. Kiểm tra cả trường hợp vào Play không rebake và Domain Reload tắt.
2. Chọn Worker → một nút xây; Town Hall → các nút train có ảnh/nhãn tương ứng.
   Một click vẫn gửi đúng một lệnh theo đường cũ. Không tự mở khóa lệnh.
3. Đổi lựa chọn liên tục: tile cũ bị loại khỏi layout ngay, không để khoảng trống
   do Destroy cuối frame. Không chọn: không còn tấm nền dưới lớn.
4. HP đầy/nửa/cạn hiển thị đúng thanh và số; nhóm vẫn ghi rõ HP đại diện.
5. Xem 1080p/720p, tên dài và nhóm nhiều đơn vị; kiểm tra chân dung, label và raycast.
6. So thanh trên với bản đã đạt. Màu sắc vẫn là backlog riêng.

Chủ dự án đã kiểm tra trong Unity và xác nhận nút/hình ảnh của bản compact đạt.
Không suy rộng xác nhận này thành nghiệm thu toàn bộ HP/lifecycle, player build
hoặc FPS; agent chưa chạy gameplay test cho bản compact.

## Bước tiếp theo — quyền nhận input HUD/placement

Đã triển khai sau khi chủ dự án xác nhận nút/hình ảnh đạt. Không sửa prefab,
palette hoặc các asset icon của bản đã nghiệm thu.

Quy tắc input:

- GameplayInputGate kiểm tra vị trí chuột hiện tại bằng EventSystem.RaycastAll,
  chỉ coi kết quả GraphicRaycaster là UI. Không lấy PhysicsRaycaster/world object
  làm HUD và không phụ thuộc vị trí cache từ EventSystem của frame trước.
  Dùng lại PointerEventData/list kết quả; đổi EventSystem thì tạo dữ liệu tương ứng.
- UnitController bỏ qua lệnh chuột phải/Delete khi con trỏ trên UI, đang nhập text,
  cửa sổ mất focus hoặc placement đang giữ input. Thứ tự Attack/Gather/Build/Move
  và đường CancelBuildingRequest được giữ nguyên.
- Chuột phải của UnitController dùng cạnh nhấn Input System trong Update,
  thay cho trạng thái JustPress từ FixedUpdate có thể kéo dài qua nhiều Update.
  Không sửa FixedUpdateInputTracker dùng chung hoặc Input System settings.
- BuildingPlacer dùng cùng Input System cho nút/position chuột và Escape.
  Click trái trên UI không đặt nhà; ghost tạm ẩn trên UI hoặc khi nhập text.
  Chuột phải hủy placement kể cả khi trỏ UI; Escape hủy khi không nhập text.
- Khi placement hoạt động, SelectManager không gửi yêu cầu click/drag selection,
  giữ những worker đang chọn cho PlanBuild. Khi đặt xong/hủy, input vẫn bị chặn
  trong frame đó và đến khi cả hai nút chuột đã thả. Quy tắc không phụ thuộc
  UnitController.Update chạy trước hay sau BuildingPlacer.Update.
- Tắt BuildingPlacer sẽ hủy ghost và kết thúc placement; kiểm tra trạng thái bằng
  IsPlacementActive không gọi getter Instance có khả năng tự tạo GameObject.
- Click/drag bắt đầu trên HUD vẫn bị bỏ qua đến lúc thả chuột, kể cả kéo ra bản đồ.
  Drag đã bắt đầu ngoài HUD giữ hành vi selection hiện tại; chưa đổi thuật toán
  hoặc thêm chế độ drag mới.
- Scroll trên UI không zoom camera; nhập vào TMP/uGUI InputField không đồng thời
  di chuyển camera bằng bàn phím. Giữ tốc độ, giới hạn zoom và simulation hiện có.

File sửa: GameplayInputGate.cs mới (kèm meta), UnitController.cs,
BuildingPlacer.cs, SelectManager.cs, IsometricTerrainCamera.cs, CameraScript.cs.
Helper là presentation/input, không thêm ECS state hoặc đổi framework.

Kiểm chứng đã thực hiện:

- Compiler đi kèm Unity 6000.3.13f1 biên dịch toàn bộ runtime, gồm file mới: exit 0.
  Còn cảnh báo obsolete ShootVictimAuthoring ngoài phạm vi.
- 15 assertion đạt cho PointerInputSuppression lấy từ source thật, chạy độc lập:
  mặc định không khóa, khóa frame hủy, nhiều consumer trong cùng frame,
  giữ chuột lâu, thả để mở khóa, thao tác tiếp theo, hủy bằng Escape,
  thay placement và reset state. Đây chỉ là kiểm tra policy managed,
  không phải kiểm tra raycast/input module hoặc Play Mode Unity.
- Hash prefab HUD/nút, Main, CommandButton, CommandQueue, ProductionSystem,
  SelectSystem và cấu hình ProjectSettings được đối chiếu để giữ nguyên.

Chờ nghiệm thu thao tác thực tế trong Main:

1. Chọn worker; chuột phải lên panel/nút train/build: không phát Move/Gather/Build/
   Attack xuống bản đồ. Thả chuột rồi click bản đồ: lệnh bình thường hoạt động.
2. Bắt đầu kéo trên HUD rồi đi ra bản đồ: không box-select. Click/drag bắt đầu
   ngoài HUD khi không placement vẫn chọn như trước.
3. Bắt đầu đặt nhà: click trái trên HUD không đặt; ghost ẩn trên HUD và trở lại
   khi ra bản đồ. Click vị trí hợp lệ chỉ đặt một nhà, không đổi nhóm worker.
4. Chuột phải hoặc Escape hủy placement: không phát thêm lệnh, không đổi selection.
   Giữ chuột sau khi hủy vẫn bị chặn; thả rồi click mới điều khiển bình thường.
5. Khi placement đang hoạt động, Delete không hủy đối tượng đang chọn.
   Ngoài placement, Delete trên bản đồ vẫn gửi CancelBuildingRequest như trước.
6. Scroll trên HUD không zoom, scroll bản đồ vẫn zoom (kể cả khi xem trước nhà).
   Nếu có InputField trong scene kiểm tra, nhập text/Delete/phím camera không
   kích hoạt gameplay; Main hiện chưa có màn hình nhập text được nghiệm thu.
7. Vào/thoát Play, mất/lấy lại focus, tắt/bật placer: không khóa input kéo dài
   hoặc còn ghost mồ côi. Chưa nghiệm thu player build/gameplay/hiệu năng.

## Menu lệnh — trạng thái động và tooltip

Đã triển khai theo yêu cầu tiếp tục. Chưa có xác nhận nghiệm thu Play Mode cho
bước input hoặc menu chỉ từ lời nhắn tiếp tục; giữ riêng các checklist tương ứng.

- CommandMenu giữ các instance nút khi chỉ đổi tài nguyên, unlock hoặc queue;
  cập nhật interactable và tooltip mỗi 0,1 giây theo unscaled time.
  Đổi selection/player/World/registry hoặc danh sách offer thì dựng lại menu.
- Khi click, kiểm tra lại source/player/World và availability trước khi gọi
  CommandButton.OnClick cũ. Chỉ nối một listener cho mỗi nút. Simulation vẫn
  quyết định cuối cùng và thanh toán, UI không tự trừ tài nguyên hoặc mở khóa.
- CommandAvailabilityReader chỉ đọc: dùng CommandAuthority, TechUnlockHelper và
  ResourcePayment.CanAfford hiện có. Chi phí xây lấy từ EntityResourceCost trên
  prefab ECS; train từ UnitBlob.ResourceCosts; research từ ResearchBlob.Tech.Cost.
  Kiểm tra producer/offer/prefab, công trình chưa xong, queue capacity, prerequisite,
  trạng thái research Pending/Completed và tài nguyên. Không tạo validator simulation mới.
- Build được phép vào placement khi đủ các điều kiện UI; địa hình/occupancy vẫn
  kiểm tra tại vị trí chọn bởi pipeline cũ, không khẳng định vị trí nào cũng hợp lệ.
- Đầy dân số không khóa train: Enqueue hiện cho phép xếp việc, ProductionSystem
  giữ unit hoàn thành chờ chỗ. Tooltip cảnh báo completion may wait for space.
  Chưa thêm UI queue hoặc lệnh hủy queue.
- Tooltip gồm tên hành động, chi phí, lý do khóa/Available, dân số cần dùng và
  duration ước tính cho production nếu rate hợp lệ. Duration là thời gian làm
  riêng công việc ở rate hiện tại, không bao gồm việc xếp hàng trước nó.
  Không ước tính thời gian xây từ một worker vì còn phụ thuộc nhóm thợ thực tế.
- Nút bị khóa vẫn nhận hover để xem lý do. Khôi phục màu icon gốc khi available;
  dùng tint xám/xanh Completed sẵn có, chưa đổi palette.
- CommandTooltip trong UICanvas rộng 300, cao theo text (72–240), đặt gần nút
  và kẹp trong canvas. CanvasGroup mặc định alpha 0, blocksRaycasts false;
  Image/TMP không raycast. Tooltip không tạo vùng UI chặn chuột mới.
- Xóa/ẩn nút, đổi selection, tắt panel hoặc hủy World không để tooltip mồ côi.
  Tên/ảnh, grid 72×72, panel compact và toàn bộ thanh trên giữ nguyên.

File: CommandMenu.cs; CommandAvailability.cs, CommandTooltipView.cs,
CommandHintTarget.cs mới (kèm meta); thêm CommandTooltip vào UICanvas.prefab.
Các chuỗi mới tập trung trong helper presentation, chưa nối hệ thống localization.

Kiểm chứng: biên dịch runtime bằng compiler Unity 6000.3.13f1 exit 0; kiểm tra
fileID/GUID, tooltip mặc định ẩn/không raycast và đối chiếu các block prefab cũ.
Giữ nguyên Button prefab, CommandButton, CommandQueue, ProductionJobs,
ProductionSystem, ResourcePayment, TechUnlockHelper, Main và input gate.
Chưa chạy gameplay/Play Mode, chưa đo allocation hoặc xác nhận tooltip trực quan.

Nghiệm thu trong Main:

1. Hover build/train, kể cả nút disabled: thấy đúng hành động, cost và lý do;
   không có tooltip ngay lúc chưa hover, tooltip không vượt mép ở 1080p/720p.
2. Giữ nguyên selection, chi/thu tài nguyên qua gameplay: nút khóa/mở trong khoảng
   0,1 giây. Không cần bỏ chọn rồi chọn lại, số lượng instance nút không tăng.
3. Giữ Town Hall đang chọn, xếp đủ job capacity: Production queue full; khi một
   job hoàn thành thì nút mở lại nếu đủ tài nguyên/unlock.
4. Công trình còn xây: Under construction, hoàn tất thì trạng thái đổi ngay.
5. Đầy dân số: nếu còn slot/tài nguyên thì vẫn train được; tooltip có cảnh báo.
6. Nếu production offers của scene có research: Locked → Available sau prerequisite;
   click → Research in progress; hoàn tất → Research completed. Không thêm research
   vào scene chỉ để mặc định coi nhánh này đã được nghiệm thu.
7. Đổi chọn nhanh, bỏ chọn, hủy đối tượng, tắt/bật HUD: không giữ tooltip cũ;
   một click hợp lệ chỉ gửi một lệnh. Kiểm tra lại click phải không xuyên HUD.

Trạng thái: mã và tham chiếu đã kiểm tra; chờ nghiệm thu trong Unity.

## Bổ sung backlog — màu sắc, khung viền và icon HUD

Chủ dự án đề xuất bổ sung sau khi xem ảnh tham khảo RTS. Phần polish màu trước
đây tạm hoãn nay được đưa vào kế hoạch để đánh giá và triển khai ở lượt riêng.
Lượt bổ sung này chỉ sửa tài liệu; chưa sửa prefab, script, màu hoặc tạo asset.

### Đánh giá ảnh tham khảo và hiện trạng

- Ảnh tham khảo dùng khung phân vùng để tách chân dung, thông tin lựa chọn và
  lệnh; icon có ô nền thống nhất, tài nguyên ở thanh trên dễ phân biệt bằng hình.
  Có thể áp dụng cách phân nhóm này cho HUD compact hiện có.
- Không sao chép nguyên HUD khoa học viễn tưởng hoặc đưa minimap, đội hình dạng
  lưới và queue vào phạm vi chỉ vì chúng xuất hiện trong ảnh. Giữ hướng tu tiên
  của các concept tại Assets/_Project/Art/Design/UI/XianxiaRTS/Concepts.
- Tài nguyên hiện dùng nhãn Gold/Wood/Food trong ResourceGroup.cs; icon của
  Resource Info.prefab đang ẩn theo bản HUD đầu. Đây là vị trí ưu tiên bổ sung
  icon nhận diện, vẫn giữ chữ/số và trạng thái dữ liệu không có.
- Đã tìm thấy Heart.png trong Art/UI/Icons và ảnh Worker/Soldier/TownHall cùng
  các ảnh đối tượng khác trong Art/UI/Sprites. Chưa xác nhận chúng cùng phong
  cách hoặc đọc tốt ở cỡ nhỏ; chưa tìm thấy bộ icon tài nguyên đồng bộ trong
  phạm vi thư mục Art/UI vừa kiểm kê. Cần kiểm kê thêm trước khi tạo ảnh mới.
- InfoPanel.SetInfoIcon hiện gán sprite.texture vào RawImage, không lấy vùng cắt
  sprite; nếu dùng sprite atlas có thể hiện cả texture. Khi triển khai icon phải
  xử lý đúng vùng sprite (ví dụ dùng Image.sprite tại riêng ô tài nguyên), không
  đổi component dùng chung hàng loạt khi chưa kiểm tra các nơi tham chiếu.
- Nút và chân dung đã được chủ dự án xác nhận đạt; giữ binding và kích thước
  compact làm mốc. Input/tooltip vẫn có checklist Unity chưa được nghiệm thu.
- Chưa đo tương phản, xem HUD trên các nền chiến trường hoặc kiểm tra icon nhỏ
  trong Game View ở lượt này. Mức độ dễ đọc của phương án mới chưa được xác nhận.

### Kế hoạch và ưu tiên

| Ưu tiên | Công việc / mục tiêu | File liên quan | Phụ thuộc | Tiêu chí nghiệm thu | Rủi ro |
| --- | --- | --- | --- | --- | --- |
| P1-A | Chốt mẫu màu và khung cho thanh trên, info card, một nút và tooltip | UICanvas.prefab; NonNavigationButton.prefab; concept XianxiaRTS | Chọn phong cách khung; giữ bố cục compact | Một mẫu thể hiện normal/hover/pressed/disabled và cảnh báo; đọc được trên nền sáng/tối, số liệu nổi hơn trang trí | Khung quá cầu kỳ làm HUD nặng và lấn vùng chơi |
| P1-B | Áp dụng palette và viền thống nhất | UICanvas.prefab; NonNavigationButton.prefab; CommandMenu.cs | Mẫu P1-A được chốt; rà tint runtime | Không xuất hiện màu cũ đè palette khi đổi trạng thái; viền không tăng hitbox/chặn input; không làm panel lớn thêm | Tint Image và Button transition có thể nhân màu, mất chi tiết icon |
| P1-C | Icon Gold/Wood/Food, dân số, thời gian; HP nếu hữu ích | Resource Info.prefab; ResourceGroup.cs; InfoPanel.cs; UICanvas.prefab; Art/UI | Kiểm kê asset; chốt ngôn ngữ hình ảnh và xử lý sprite | Icon không méo/cắt sai, nhận ra ở 720p/1080p; vẫn có nhãn hoặc tooltip; số không tràn; ảnh thiếu có fallback | Ảnh không đồng bộ, hiểu sai biểu tượng, atlas hiển thị sai |
| P1-D | Làm rõ trạng thái lệnh và cảnh báo | CommandMenu.cs; CommandAvailability.cs; CommandTooltipView.cs; prefab nút | P1-B; nghiệm thu availability/input | Available/hover/pressed/disabled/completed phân biệt được; khóa có lý do chữ và dấu hiệu ngoài màu; đầy dân số giữ đúng quy tắc train hiện có | Hiểu nhầm khóa lệnh; lớp viền/biểu tượng che ảnh hoặc bắt raycast |
| P1-E | Nghiệm thu tổng thể trong Main | Main.unity; UICanvas.prefab; checklist input/menu ở trên | P1-B/C/D hoàn tất | Xem 1920×1080 và 1280×720, 4:3 và ultrawide; nền sáng/tối; HP thấp, số dài, tooltip mép; click không xuyên HUD; không đổi gameplay | Co giãn làm viền nhòe, chữ nhỏ, HUD che vùng chơi |

Các file không ghi đường dẫn đầy đủ trong bảng nằm ở những thư mục đã liệt kê
trong các giai đoạn trên. P1 là mức ưu tiên về trải nghiệm, không đồng nghĩa cần
đổi toàn bộ mỹ thuật trước khi nghiệm thu input/menu. Chốt mẫu P1-A có thể làm
trước phần thông tin worker/xây dựng; thực hiện P1-E sau khi các vùng HUD liên
quan ổn định để tránh polish lặp lại. Minimap/menu/queue vẫn tạm hoãn.

### Quy chuẩn dự kiến để thử trên mẫu

- Nền than/ngọc tối, chữ chính ngà sáng, chữ phụ xám sáng; viền đồng/vàng trầm
  gợi tu tiên. Đây là hướng đề xuất, chưa phải palette đã duyệt hay đã áp dụng.
- Chỉ nhấn màu ở lựa chọn, lệnh tương tác và cảnh báo. HP/tình trạng nguy cấp có
  số hoặc biểu tượng kèm theo; không dùng màu làm dấu hiệu duy nhất.
- Một viền phân vùng mảnh 1–2 đơn vị ở canvas chuẩn 1080p; khung chân dung rõ
  hơn viền nền. Nếu tạo sprite khung thì dùng 9-slice, giữ lòng panel gọn; không
  thêm chi tiết trang trí lớn hoặc tăng chiều cao HUD chỉ để chứa viền.
- Icon tài nguyên khoảng 20–24, icon phụ 16–20 đơn vị để thử; ô lệnh giữ 72×72
  và ảnh 40×40 đang được chấp nhận. Chốt kích thước cuối sau khi xem ở 720p.
- Icon chức năng cùng nét, góc nhìn và độ tương phản; chân dung đối tượng tiếp
  tục dùng ảnh riêng. Không thêm chỉ số chiến đấu/trạng thái chưa có dữ liệu.
- Giữ Scale With Screen Size, reference 1920×1080 và anchors hiện có làm cơ sở;
  lớp viền/đồ họa trang trí không nhận raycast, không đổi vùng nhận input.
- Mục tiêu thiết kế: chữ thường tương phản ít nhất 4.5:1 trên nền panel thực tế,
  biểu tượng/viền mang thông tin ít nhất 3:1. Đây là tiêu chí đề xuất, chưa có
  kết quả đo; nền trong suốt phải được xem trên nhiều cảnh chiến trường.

Quyết định cần chốt trước khi làm asset: khung tu tiên tiết chế hay trang trí
nhiều; có chấp nhận bộ icon mới cùng phong cách nếu asset hiện có không đủ.
Bước đầu đề xuất là làm một mẫu HUD nhỏ với palette, khung và icon đại diện
để duyệt, rồi mới áp dụng rộng. Giữ toàn bộ nội dung prototype đến khi bản thay
thế được chấp nhận; chưa cần đổi framework hoặc thêm hệ thống theme lớn.

## Triển khai theo ưu tiên — thông tin theo ngữ cảnh và local player

Đợt này thực hiện phần mã có thể kiểm chứng của P0, cùng P1 mục 3–4. Không có
connector Unity hoặc điều khiển Unity trực tiếp trong phiên này; chưa chạy
Play Mode, nghiệm thu input/tooltip hoặc player build. Không đánh dấu P0 đạt
chỉ dựa trên biên dịch và rà mã.

### P0: rà menu lệnh

- CommandMenu.OffersMatch nay so sánh cả JobReference.CatalogKey và ID. Trước
  đây chỉ đổi tham chiếu job trong khi JobID/Kind/UnitPrefab giữ nguyên có thể
  không dựng lại nút, để CommandButton giữ tham chiếu cũ.
- TrySubmit kiểm tra lại danh sách offer ngay tại click, chặn nút cũ nếu danh
  sách đã thay đổi trước lượt Update kế tiếp. Không sửa simulation/enqueue.
- Rà input gate và tooltip trong mã; chưa có bằng chứng kiểm tra tương tác
  thực tế. Các checklist P0 ở hai phần trước vẫn còn nguyên hiệu lực.

### P1 mục 3: worker và tiến độ xây dựng

- UnitInfoPanel đọc WorkerGatherData và BuilderComponent, chỉ dùng state của
  component đang enabled để mô tả công việc. Builder đang hoạt động được ưu
  tiên; không mô tả mọi worker không có gather/build là đứng yên, vì họ có thể
  đang di chuyển hoặc thực hiện lệnh khác. Nhãn khi đó là No work task.
- Hiển thị To resource/Gathering/Returning hoặc To build site/Building.
  Đọc cargo kể cả khi gather bị disabled: lệnh move/build có thể giữ lượng
  tài nguyên mang theo. Cargo rỗng là Cargo: 0, không gán tên resource mặc định.
- Công trình đọc Phase, CompletedWork và WorkLoad: Planned/Building cùng phần
  trăm và thanh tiến độ; Completed/Destroyed theo phase. Không dự đoán thời
  gian hoàn thành. Thiếu workload hợp lệ thì --%, ẩn thanh; phần trăm công trình
  chưa hoàn thành tối đa 99 để không báo xong trước simulation.
- Nhóm vẫn dùng cùng đại diện với menu lệnh, thêm Rep.: trước thông tin ngữ
  cảnh; cargo/tiến độ không được coi là tổng của nhóm. Không đổi selection.
- Giữ info card 280×144, portrait/nút/thanh HP hiện có. Hai dòng ngữ cảnh nằm
  trong cột chữ; số lượng nhóm chuyển xuống dưới chân dung, thanh xây tách
  khỏi thanh HP. Chưa đổi palette hay tạo icon mới.
- Làm mới ngữ cảnh mỗi 0,1 giây theo unscaled time; đổi entity/registry/definition
  hoặc số lượng chọn thì làm mới ngay. Bỏ chọn/tắt panel/đổi World xóa thông tin
  và ẩn thanh. Soldier không có dữ liệu worker/construction không hiện nhãn giả.

### P1 mục 4: thông tin local player

- PlayerInfoText.cs mới (kèm meta), gắn vào Player info của UICanvas. Hiện Player
  theo ID thật và Industrial/Modern/Future từ PlayerContext.age; chưa đổi tên các
  thời đại sang tu tiên hoặc chuyển ID sang số thứ tự hiển thị khác.
- Thay IndustrialAge tĩnh bằng placeholder Player -- / Age -- trước khi có
  context. Cỡ chữ vùng này 18 để chứa hai dòng trong thanh trên hiện có.
- UIComponent có hook OnPlayerContextUpdated mặc định không làm gì; UIManager
  truyền context đã đọc mỗi LateUpdate cho component đang bật. PlayerInfoText
  chỉ cập nhật chữ khi ID/age đổi. Cách này đáp ứng age chưa có event riêng,
  không tạo query/Find mới mỗi frame hoặc thay snapshot/resource event cũ.
- Chuỗi mới tập trung ở serialized fields; không thêm localization framework,
  không suy diễn tên player/civilization khi chưa có mapping hiển thị.

Kiểm chứng: compiler đi kèm Unity 6000.3.13f1 biên dịch runtime với file mới,
exit 0; còn warning obsolete ShootVictimAuthoring và các thông báo SGFE009 cũ.
Prefab không trùng fileID, tham chiếu local đều có đích, GUID script mới duy
nhất. Đối chiếu snapshot: chỉ sáu block prefab cũ thay đổi như dự kiến, phần
còn lại được giữ nguyên; simulation CommandQueue/BuilderSystem/WorkerGatherSystem
và ProjectSettings.asset có hash giữ nguyên. git diff --check không có lỗi
whitespace. Đây chưa phải xác nhận render, input, ECS runtime hoặc hiệu năng.

Checklist bổ sung trong Main:

1. Chọn worker, giao thu thập rồi trả tài nguyên: hoạt động/cargo đổi đúng.
   Đổi sang lệnh move/build khi còn cargo: không hiện state gather cũ như đang
   hoạt động; lượng mang theo khớp simulation.
2. Giao xây: To build site → Building; chọn công trình Planned/UnderConstruction
   thì phần trăm tăng và chuyển Completed đúng lúc. HP và thanh xây độc lập.
3. Chọn nhóm: thấy tổng số chọn và Rep.:; đổi đại diện không giữ dữ liệu trước.
   Chọn Soldier rồi bỏ chọn: không còn cargo/tiến độ mồ côi.
4. Xem 1080p/720p, đặc biệt hai dòng ngữ cảnh, group count dưới chân dung và thanh
   xây; chưa khẳng định font/ellipsis đọc tốt trước khi xem trực tiếp.
5. Player info khớp local PlayerContext, age đổi thì chữ đổi; thiếu context hoặc
   tắt/bật HUD không giữ thông tin player cũ. Không cần thêm lệnh đổi age để test.
6. Thực hiện checklist P0 input/menu phía trên, nhất là click không xuyên vùng
   thông tin mới. Kiểm tra Enter/Exit Play và player build ở đợt nghiệm thu.

Trạng thái kế hoạch: P0 chờ nghiệm thu Unity; P1 mục 3–4 đã triển khai mã/prefab,
chờ nghiệm thu. P1 mục 5–7 (mẫu màu/khung/icon) chưa triển khai; P1 mục 8 mới
kiểm tra tham chiếu và bố cục tĩnh ở phạm vi sửa, chưa đạt nghiệm thu tổng thể.
P2 thông tin nhóm theo loại và khả năng tiếp cận/phím tắt vẫn còn trong backlog.

## Kế hoạch bổ sung sau đối chiếu toàn HUD mẫu — công trình, mỏ, minimap

Phần này thay danh sách ưu tiên hẹp trước đây. Chủ dự án yêu cầu đưa UI công
trình phòng thủ, mỏ và minimap vào kế hoạch. Chỉ khảo sát mã/asset và xem hai
ảnh concept XianxiaRTS trong lượt này; không chạy Unity/test hoặc sửa gameplay,
scene, prefab, script. Ảnh mẫu là hướng bố cục/mỹ thuật, không là bằng chứng
game đã có mana, kỹ năng, nhiệm vụ, fog of war hoặc các lệnh trong ảnh.

### Kết quả đối chiếu

- UnitInfoPanel có tên/ảnh/HP, nhóm đại diện, worker/cargo và construction;
  chưa có nhánh thông tin vũ khí, kho, mỏ hoặc queue. EntityPresentation chỉ
  resolve UnitComponent/BuildingComponent; UnitInfoPanel và SelectSystem chỉ
  nhận selection thuộc player hiện tại. Mỏ trung lập cần đường xem thông tin
  riêng, không mở quyền phát lệnh lên đối tượng đó.
- Có TowerDefend, TowerDefend2, TowerDefendEnemy, PlasmaTower, Barracks,
  ResourceDepot prefab và ảnh Tower/PlasmaTower/Barrack/ResourceDepot. Tuy nhiên
  GameReg.asset chỉ đăng ký TownHall ở Buildings, Build.asset trong
  Tests/Fixtures/TwoCivDogfood chỉ offer TownHall. Các prefab cũ còn tên component/
  field authoring kiểu cũ; BuildingAuthoring hiện yêu cầu BuildingSO và không
  được chung entity với UnitAuthoring. Không coi các prefab đó sẵn sàng runtime
  hoặc tự gán thông số mới chỉ vì tìm thấy model/prefab/ảnh.
- AttackAbilityDefinition có Range và Weapons (Damage/Cooldown/ProjectileSpeed);
  ShootAttack/ShootAttackSystem là đường dữ liệu chiến đấu hiện tại. Có thể dùng
  cho UI thông số khi đối tượng thực sự đã bake đúng attack definition. Không
  đọc damage/range từ field cũ trong prefab rồi coi là giá trị runtime.
- ResourceNodeData có Type/Amount; MapResourceIdentity có DefinitionId/Tier/
  ClusterRole; MapResourceDefinition và MapResourcePrefab có AmountPerMine.
  Dùng tên loại/stock còn lại trước, phần trăm chỉ khi resolve được stock gốc
  đúng node. Không coi Amount mặc định 0 trên template chưa config là mỏ cạn.
  ResourceNodeDepletedSystem hủy node cạn: UI phải bỏ tham chiếu/ẩn hoặc đóng
  trạng thái hết tài nguyên an toàn. ResourceNodeReference đang giúp đường
  giao gather; không đồng nghĩa mỏ đã chọn được trong bảng thông tin.
- Có MiniMap RawImage trong UICanvas gắn MiniMapTexture.renderTexture;
  BackGround là parent của nó, bị Main override m_IsActive=0. Camera có target
  RenderTexture và MinimapCam nằm trong GameScene; CameraPivot hiện không gắn
  target texture này. MinimapCam chỉ đi theo camera chính theo X/Z, không phải
  lời giải bản đồ toàn map. MiniMapIcon, MinimapRendererAuthoring và
  MiniMapUISystem có prototype marker; chưa xác nhận bindings/managed
  SpriteRenderer hoạt động với pipeline map/ECS hiện tại. Không chỉ bật panel
  rồi coi minimap hoàn thành.
- Concept HUD và UI kit còn có stats chiến đấu, nhóm portrait, queue train,
  nghiên cứu, nút kỹ năng và menu; HUD concept có objectives. Backend production
  queue/research tồn tại, nên queue là đề xuất có dữ liệu nền. Chưa xác nhận
  hệ thống mana/kỹ năng chủ động, nhiệm vụ hoặc thắng/thua để nối UI tương ứng.
- HealthBarSystem/HealthBarAuthoring đã có thanh HP trong world; hạng mục đó là
  kiểm kê và làm rõ điều kiện hiển thị, không mặc định xây từ đầu.
- Đợt style đã có thay đổi palette/frame/icon và sửa override tài nguyên trong
  Main, nhưng lượt kiểm tra bị dừng trước khi xem lại bản sửa. Mã
  Scripts/Editor/HudStyleAuditTemporary.cs vẫn tồn tại tại thời điểm khảo sát.
  Cần chốt/dọn đợt này; dòng trạng thái cũ nói style chưa triển khai phía trên
  chỉ phản ánh thời điểm trước đợt style, không phải trạng thái mới nhất.

### Thứ tự thực hiện mới

P0 là nền tảng cần chốt; P1 là các khoảng thiếu trọng tâm; P2 là hoàn thiện
và mở rộng có chọn lọc; P3 chỉ thiết kế/triển khai khi gameplay được chốt.
Các mục cùng ưu tiên thực hiện theo thứ tự dòng và phụ thuộc dưới đây.

| Mã | Ưu tiên / việc làm | File căn cứ hoặc phạm vi dự kiến | Phụ thuộc | Nghiệm thu | Rủi ro |
| --- | --- | --- | --- | --- | --- |
| A | P0 — Chốt style hiện tại, dọn audit tạm, cập nhật tiến độ | Main.unity, UICanvas.prefab, Resource Info.prefab, HudStyleAuditTemporary.cs | Kiểm tra lại bản sửa tài nguyên | Ba tài nguyên đủ icon/số, không lệch; không còn công cụ tạm; không có lỗi UI mới | Scene override đè prefab, ảnh thiếu reference |
| B | P0 — Ma trận đối tượng UI và mức sẵn sàng dữ liệu | GameReg.asset, Build.asset; TowerDefend/PlasmaTower/Barracks/ResourceDepot; BuildingAuthoring, UnitAuthoring | A; quyết định công trình dùng trong Main | Mỗi loại ghi rõ definition/name/icon/HP/owner/ability/offer, có chạy Main hay chỉ tham khảo | Prefab cũ thiếu definition hoặc không bake; sửa dữ liệu có thể ảnh hưởng gameplay |
| C | P1 — Xem thông tin đối tượng trung lập và phân quyền UI | SelectSystem.cs, SelectManager.cs, UnitInfoPanel.cs, EntityPresentation.cs, CommandMenu.cs | B | Xem được một mỏ; chỉ chủ sở hữu hợp lệ có nút điều khiển; click/drag chọn quân cũ giữ đúng quy tắc | Trộn đối tượng đang xem với nhóm đang nhận lệnh |
| D | P1 — Bảng thông tin mỏ | ResourceAuthoring.cs, MapGenConfig.cs, ResourceNodeDepletedSystem.cs; bảng thông tin/ảnh mỏ | C | Loại + ảnh/tên + lượng còn lại + tier nếu có; cập nhật khai thác; node cạn/hủy không gây lỗi; không hiện HP giả | Nhầm stock template, stock gốc, đơn vị/cụm mỏ |
| E | P1 — Thông tin chung và riêng theo chức năng công trình | UnitInfoPanel.cs, EntityPresentation.cs; BuildingComponent, EntityState.cs, registry/ảnh công trình | B | Công trình đã tích hợp hiện đúng tên/ảnh/HP/tiến độ/loại; kho nêu loại tài nguyên nhận, nhà nêu PopulationCapacity khi có; chỉ hiện dữ liệu hợp lệ | Asset trùng vai trò, suy diễn sức chứa kho chưa có dữ liệu |
| F | P1 — Nhánh tháp phòng thủ | AttackAbilityDefinition.cs, ShootAttackSystem.cs; prefab/definition tháp đã chọn ở B | E; công trình bake được với combat hiện tại | Damage theo vũ khí, range, cooldown đúng definition runtime; nhiều vũ khí không bị gộp sai; không bịa armor/nâng cấp | Thông số authoring cũ không phản ánh runtime; target hoặc cooldown khó diễn giải |
| G | P1 — Bố cục đáy HUD có chỗ cho minimap | UICanvas.prefab, CommandMenu.cs | D/E; kích thước minimap được chọn | Minimap góc trái dưới theo mẫu, selection cạnh nó, lệnh tiếp theo; không chiếm một dải nền lớn khi trống; không chồng ở 720p | Minimap mới đè panel đang ở x=0; giới hạn chiều rộng |
| H | P1 — Minimap hiển thị bản đầu | Main.unity, GameScene.unity (tham khảo), CameraPivot.prefab, UICanvas.prefab, MiniMapTexture.renderTexture, MinimapCam.cs, MiniMapUISystem.cs | G; kiểm kê camera/layer/marker và bounds map hiện tại | Thấy toàn map đúng tỷ lệ/hướng; phân biệt quân/công trình/mỏ; khung vùng camera; không thấy icon mồ côi; cập nhật vừa đủ | Minimap prototype chỉ bám camera, renderer cũ không tương thích, chi phí render, lộ thông tin nếu sau này có fog |
| I | P1 — Tương tác minimap | Input minimap dự kiến; GameplayInputGate.cs, camera | H | Click/kéo trái điều hướng camera, clamp map; không phát lệnh world hoặc box-select xuyên minimap | Xung đột placement/selection; tọa độ lệch vì map origin/terrain |
| J | P1 — Stats quân và phản hồi ngữ cảnh | UnitInfoPanel.cs, CommandMenu.cs, UnitController.cs, BuildingPlacer.cs, dữ liệu attack/movement hiện có | B/C | Quân chiến đấu có stats cần thiết; phân biệt lệnh đang có và không có; placement/thiếu điều kiện có phản hồi rõ | Đưa nút Stop/Guard/kỹ năng chưa có handler hoặc tốc độ UI sai ý nghĩa |
| K | P2 — Queue sản xuất và trạng thái chờ | ProductionAuthoring.cs, ProductionJobs.cs, ProductionSystem.cs; UI queue dự kiến | E; chốt scope queue | Job hiện tại, tiến độ + thứ tự chờ; nhận diện chờ dân số; không cho nút hủy nếu chưa có API hủy hợp lệ | Đo remaining work thành thời gian sai rate; refund/hủy là thay đổi simulation |
| L | P2 — Nhóm đơn vị, world HP và cảnh báo | Selection UI, HealthBarSystem.cs, HealthBarAuthoring.cs | C/J | Nhóm theo loại + số; HP/selection marker dễ đọc; cảnh báo chỉ dựa trên sự kiện có thật | Quá nhiều marker/portrait gây rối hoặc nặng; thiếu event cảnh báo |
| M | P2 — Menu và khả năng tiếp cận | MenuButton trong UICanvas; menu/settings dự kiến | Chốt pause; kiểm tra input | Menu mở/đóng được, focus đúng; cỡ chữ/UI scale/âm lượng chỉ nối setting thật; không tự đổi simulation khi chưa chốt pause | Game vẫn chạy nhưng menu khiến hiểu nhầm đã pause; mất focus/phím tắt |
| N | P2 — Nghiệm thu tổng thể / PC build | Main, các UI/camera/input đã thay đổi | Mỗi mốc chạy kiểm tra cục bộ; kiểm tra cuối sau H–M trong scope | 1080p/720p/4:3/ultrawide; dữ liệu dài; lifecycle; input; không tăng allocation đáng kể; build PC | Chỉ nhìn Editor không phát hiện lỗi player build |
| O | P3 — Nghiên cứu chi tiết, kỹ năng/mana, objectives, kết quả trận, bản địa hóa | Tech definitions/helpers hiện có; phần còn lại cần khảo sát gameplay tương ứng | Xác nhận feature thật và phạm vi | Chỉ có màn hình khi có dữ liệu, vòng đời và handler gameplay đã xác định | Mở rộng gameplay ngoài ý định chỉ để giống concept |

Không bắt buộc hoàn tất P2 mới kiểm tra độ phân giải: kiểm tra ở từng mốc D/F/H,
rồi nghiệm thu tích hợp ở N. Queue/research/menu là đề xuất mở phạm vi; chưa coi
chủ dự án đã yêu cầu triển khai tất cả chỉ từ yêu cầu kiểm kê này.

### Phạm vi kiến trúc hợp lý và quyết định cần chốt

- Giữ uGUI/Canvas. Dùng chung khung selection, resolve nội dung theo loại/khả
  năng (worker, combat, construction, storage, resource), không nhân bản HUD
  riêng cho từng prefab. Chỉ tách helper/presenter nhỏ khi các nhánh làm
  UnitInfoPanel quá khó bảo trì.
- Cần phân biệt đối tượng đang xem thông tin và source nhận lệnh, vì mỏ trung
  lập không thuộc selection điều khiển hiện tại. Không bỏ kiểm tra owner trong
  SelectHelper/CommandAuthority chỉ để hiện mỏ. Đây là lý do cụ thể cho thay đổi
  hẹp ở đường chọn/xem thông tin; không cần tái cấu trúc ECS toàn project.
- Chốt loại công trình cần đưa vào Main trước: đề xuất một tháp phòng thủ hoạt
  động, rồi kho/trại lính; không mặc định phải đưa tất cả prefab cũ vào registry.
- Đề xuất mỏ click trái để xem, click phải của worker giữ gather; việc xem mỏ
  có giữ nhóm quân làm source lệnh hay bỏ selection cần chốt trước mục C.
- Đề xuất minimap bản đầu gồm hiển thị + khung camera + click/kéo trái di chuyển
  camera. Chuột phải ra lệnh trên minimap, ping và fog of war tách bước sau;
  không tự tạo fog hoặc luật tầm nhìn.
- Chốt queue chỉ hiển thị trước, hủy queue sau nếu cần; menu mở UI trước và
  giữ simulation như hiện tại đến khi có quyết định pause rõ ràng.

Việc triển khai kế tiếp: A và B, rồi một lát cắt C→D cho mỏ. Tháp phòng thủ
E→F và minimap G→I là hai mốc P1 tiếp theo, có nghiệm thu riêng, không tiếp tục
coi chúng là phần polish hoặc để minimap ngoài kế hoạch vô thời hạn.

## Triển khai mốc HUD phục vụ báo cáo

Kế hoạch nghiệm thu và tài liệu trình bày mới nhất:
[UIReportReadiness.md](UIReportReadiness.md). Tài liệu này ghi rõ trạng thái mã,
nghiệm thu, dữ liệu công trình legacy và kịch bản demo; không coi compiler đạt
là toàn bộ UI đã hoàn tất.

Đã triển khai: ResourceInspection tách focus mỏ khỏi Selected; hiển thị mỏ và
summary/tooltip theo capability; TacticalMinimap từ GridTerrain/ECS kèm điều
hướng camera; ProductionQueuePanel chỉ đọc, cùng vị trí selection cạnh minimap.
Main bật lại container minimap. Giữ RenderTexture/camera/marker prototype cũ
làm tham khảo; bản mới không phụ thuộc chúng. Đã dọn HudStyleAuditTemporary
và meta được tạo ở đợt trước.

ResourceInspection không mở quyền điều khiển trung lập. Menu/queue ẩn khi xem
mỏ; Delete không tác động lựa chọn đang ẩn, còn worker right-click gather giữ
đường cũ. Escape/click chọn khác/drag rời inspection; node cạn/hủy và đổi world/
player không giữ entity lỗi thời. Chưa có thanh % mỏ khi chưa resolve stock gốc.

Nhánh thông số dùng StorageResource/PopulationCapacity và AttackBlob (damage
theo vũ khí, range/cooldown), không đọc field gameplay legacy. Queue dùng
RemainingWork/rate hiện tại, phân biệt chờ dân số; không thêm hủy/refund.
Minimap lấy mẫu địa hình 48×48, refresh 0,2 giây, cap 2048 marker, giữ tỷ lệ map,
phân màu local/team/other và resource, có khung camera trên mặt phẳng grid.
Click/kéo trái điều hướng; chưa có fog/ping/chuột phải ra lệnh.

Đã kiểm tra compiler runtime và tham chiếu prefab/GUID, hash các luật simulation
giữ nguyên. Play Mode cho bản mới vẫn chờ nghiệm thu do công cụ kích hoạt cửa
sổ Unity timeout và ảnh trả về không đáng tin cậy. Chưa player build/đo FPS/GC.
UI tháp/kho/trại lính chưa được coi là nghiệm thu trong Main: trước hết phải
tích hợp definition/authoring hợp lệ cho công trình thực sự cần demo.

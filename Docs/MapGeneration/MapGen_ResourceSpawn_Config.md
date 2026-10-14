# Tài nguyên trên bản đồ: danh mục, cụm và sinh đối tượng

Cập nhật 2026-10-10, gồm cấu hình trữ lượng, căn lưới vùng chiếm chỗ và vùng đệm núi/ramp. Tài liệu này mô tả mã Unity hiện tại để đọc và góp ý, không phải chứng nhận số dư kinh tế hay kết quả Play Mode.

Cấu hình nằm trong **MapGenConfig > tài nguyên** tại tài nguyên:
[MapGenConfig.asset](../../Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset).

tài nguyên đang nằm chung trong MapGenConfig để thử. Sau này có thể tách thành tài nguyên tài nguyên cấu hình riêng. PlayerBootstrapSettings.StartingResources là số dư đầu trận của người chơi, độc lập với các mỏ ngoài bản đồ.

## 1. Catalog: tài nguyên nào dùng mẫu đối tượng nào?

Catalog là bảng ánh xạ từng biến thể mỏ. Một ResourceType có thể có nhiều ID, ví dụ gold.common / gold.rich / gold.premium. ID biến thể không tạo thêm loại tiền mới: cả ba vẫn có thể cho Gold.

| Trường | Ý nghĩa |
| --- | --- |
| Id | ID duy nhất, không rỗng; tối đa 61 byte UTF-8 |
| Type | Gold/Wood/Food; phải khớp ResourceAuthoring ở gốc mẫu đối tượng |
| mẫu đối tượng | mẫu đối tượng thực sự được tạo bản sao đối tượng; cần tài nguyên thiết lập dữ liệu, GridSnapper và chặn lối |
| Tier | Cấp phân bố: Common, Rich, Premium |
| AllowedClusters | Những nhóm được phép chọn definition này, sau khi áp dụng giới hạn Tier |
| Weight | Trọng số chọn giữa các definition cùng tầng; không phải giá trị kinh tế |
| AmountPerMine | Trữ lượng bắt buộc > 0 cho mỗi mỏ sinh đối tượng; mẫu đối tượng không lưu trữ lượng |

ResourceAuthoring trên mẫu đối tượng chỉ giữ Type; không còn trường Amount. bộ chuyển dữ liệu sang ECS tạo mẫu Amount=0, chưa được cấu hình. Catalog cung cấp AmountPerMine khi sinh đối tượng. Không tạo mỏ Wood/Food bằng cách đổi nhãn mẫu đối tượng Crystal/Gold.

Asset hiện chỉ có:

| ID | Type | mẫu đối tượng | Tier | AllowedClusters | Weight | AmountPerMine |
| --- | --- | --- | --- | --- | --- | --- |
| gold.common | Gold | TempGold, bản sao Crystal có sẵn | Common | All | 1 | 1500, lấy từ danh mục |

Mỏ kiểm thử có vùng chiếm chỗ 1 ×1 ô. bản thử nghiệm JS dùng vùng chiếm chỗ 2 ×2; Unity lấy vùng chiếm chỗ thật sau chuyển dữ liệu sang ECS, không ép mọi mẫu đối tượng thành 2 ×2.

## 2. Giá trị, trữ lượng và tốc độ khai thác

Ba khái niệm hiện khác nhau:

- **Tier** quyết định mỏ được đặt ở nhóm nào. Designer gán tầng; mã không tự suy tầng từ Amount.
- **Amount** là tổng số đơn vị tài nguyên còn lại trong mỏ. AmountPerMine trong cấu hình cấp trữ lượng khi sinh đối tượng, không thay loại tài nguyên.
- **GatherRate** của thợ quyết định số đơn vị khai thác mỗi chu kỳ. thợ mang số đơn vị đó về kho; Tier không nhân tiền hay tốc độ khai thác.

Ví dụ có thể cấu hình cùng mẫu đối tượng Gold với hai ID, một mỏ chứa 1500 Gold và một mỏ chứa 6000 Gold. Nếu thợ có cùng GatherRate thì tốc độ kiếm Gold như nhau; mỏ 6000 tồn tại lâu hơn. Ví dụ này không phải hai definition đã được thêm vào tài nguyên.

Ý nghĩa “giá trị mỗi lần khai thác” còn cần chốt: tăng tốc độ thu hoạch, tăng lượng nhận trên mỗi đơn vị trữ lượng, hay tạo loại tài nguyên khác. Hiện chưa sửa cơ chế kinh tế này.

## 3. Tier nào vào nhóm nào?

| Tier | Main | Secondary | Advantage | Contested |
| --- | --- | --- | --- | --- |
| Common | Được | Được | Được | Được |
| Rich | Không | Không | Được | Được |
| Premium | Không | Không | Không | Được |

AllowedClusters có thể thu hẹp bảng này. Đặt Premium + All vẫn không cho Premium vào Main.

Trong mỗi nhóm:

1. Lọc các definition theo AllowedClusters và bảng Tier.
2. Chọn tầng cao nhất còn hợp lệ.
3. Trong tầng đó, chọn definition theo Weight.

Do đó contested có Premium thì dùng pool Premium; không có Premium thì có thể dùng Rich, rồi Common. Không trộn các tầng trong cùng pool hiện tại. Nếu nhóm có hạn ngạch nhưng không có definition hợp lệ, bộ lập kế hoạch báo lỗi thay vì tự dùng mẫu đối tượng khác.

Với danh mục hiện chỉ có gold.common + All, cả bốn nhóm vẫn dùng Gold thường. Muốn contested có mỏ tốt hơn, thêm definition Rich/Premium với prefab/trữ lượng phù hợp. Dùng cùng mẫu đối tượng cho nhiều biến thể được phép, nhưng hình ảnh giống nhau; MapResourceIdentity giúp inspect ID/tier/role trong ECS.

## 4. Main cụm: gói bắt buộc, cùng cấu hình cho mọi người chơi

| Trường | Ý nghĩa |
| --- | --- |
| MainClusters | Số chính cụm mỗi người chơi |
| Main.DistanceCells | Khoảng cách cố định từ mốc sinh đối tượng tới tâm xử lý logic cụm, theo số ô |
| Main.MinesPerCluster | Số mỏ cố định mỗi chính cụm |

Main không dùng ClusterMin/ClusterMax hoặc random khoảng bán kính. Chỉ mẫu góc trên vòng tròn có bán kính DistanceCells. Tâm xử lý logic giữ float để không lệch bán kính do làm tròn; các mẫu đối tượng thành viên vẫn căn lưới vào lưới.

Recipe được chọn từ pool Common bằng hạt giống và chỉ số chính cụm, không dùng người chơi ID. Mọi người chơi có cùng hạn ngạch sẽ nhận cùng ID mỏ, trữ lượng và số lượng cho cụm tương ứng. Weight không khiến công thức chính khác nhau giữa người chơi.

bộ lập kế hoạch giữ chỗ chính cho mọi người chơi trước các nhóm tùy chọn. Không đủ thành viên hoặc không đạt khả năng tiếp cận/kết nối thì thử vị trí khác với cùng khoảng cách/số lượng/công thức. Hết lượt thử: hoàn tác kế hoạch, khởi tạo Failed trước tạo bản sao đối tượng nhà/quân/mỏ; không bớt mỏ hoặc tự tăng khoảng cách để cho qua.

**Giới hạn của bảo đảm hiện tại:**

- DistanceCells đo tới tâm cụm, không tới từng mỏ thành viên.
- Mốc cơ sở hiện là `GridSpawnCell.cell`, không phải vị trí TownHall thực tế nếu nhà bị đặt nhà dịch khỏi sinh đối tượng.
- Khoảng cách thẳng bằng nhau không bảo đảm đường đi/tốc độ khai thác/độ an toàn bằng nhau.
- hạt giống mới hoặc cấu hình khác có thể làm chính không đặt được; hiện không tự regenerate địa hình sau lỗi chính.

Kiểm tra bổ sung 2026-10-10: Unity batchmode đã chạy ĐẠT dữ liệu kiểm thử bộ lập kế hoạch với bốn hướng dốc nối tầng, khoảng đệm một ô quanh vùng chiếm chỗ và đủ chính cụm cho bốn người chơi. Fixture phẳng vẫn đạt 16/16 cụm, 41 mỏ; dữ liệu kiểm thử núi vẫn đạt 41 mỏ. Log: `Artifacts/SpawnSmoke/ramp-clearance-unity-approved.log`; báo cáo: `Artifacts/SpawnSmoke/resource-planner.txt`. Đây là kiểm tra bộ lập kế hoạch riêng, chưa phải kiểm chứng hình ảnh trong Main.

## 5. khoảng nghĩa là gì?

Các Vector2 khoảng có **X = tối thiểu, Y = tối đa**, nhân với lưới.width (W). Đơn vị kết quả là ô lưới, không phải thế giới ECS lính và không phải độ dài đường đi.

| Nhóm | Đo quanh | khoảng mặc định | bản đồ W=512 |
| --- | --- | --- | --- |
| Main | Mốc sinh đối tượng | DistanceCells = 12, cố định | 12 ô |
| Secondary | Mốc sinh đối tượng của người chơi | 0.11–0.22 × W | 56.32–112.64 ô |
| Advantage | Mốc sinh đối tượng của người chơi | 0.23–0.40 × W | 117.76–204.8 ô |
| Contested | Tâm bản đồ, trong cung ứng với người chơi | 0.07–0.28 × W | 35.84–143.36 ô |

bộ sinh dữ liệu mẫu đều theo diện tích vành khăn:
r = sqrt(min² + u × (max² − min²)), với u trong 0,1).

Đây là khoảng của **tâm cụm**. thành viên có thể nằm gần/xa hơn vì được mẫu trong bán kính cụm. Secondary không phải khoảng loại tài nguyên; chọn loại mỏ do Catalog/Tier quyết định.

Contested hạn ngạch tính mỗi cung, không tính chủ sở hữu: mọi mỏ đều trung lập. rà soát bảo đảm các cơ sở cùng vùng liên thông và tiếp cận mỏ được; hiện không bảo đảm khoảng cách tiếp cận của hai người chơi bằng nhau hoặc tranh chấp kinh tế cân bằng.

## 6. hạn ngạch, bán kính và số lần thử

| Trường | Ý nghĩa |
| --- | --- |
| ClusterMin/ClusterMax | Số mẫu đối tượng ngẫu nhiên mỗi secondary/advantage/contested cụm |
| SecondaryClusters/AdvantageClusters | Số cụm yêu cầu mỗi người chơi |
| ContestedClusters | Số cụm yêu cầu mỗi cung của người chơi |
| MinimumClusterRadius | Bán kính cụm tối thiểu, ô |
| RadiusPerSqrtCount | bán kính = tối đa(tối thiểu, sqrt(số lượng) × hệ số) |
| ClusterGap | Tâm hai cụm cách nhau ít nhất tổng bán kính + phần còn thiếu |
| MemberSpacing | Khoảng cách tối thiểu giữa tâm thành viên trong cùng cụm |
| CenterAttempts | Số bản thử tâm tối đa mỗi cụm |
| MemberAttempts | Số lần mẫu thành viên tối đa cho mỗi bản thử |

Main bắt buộc đặt đủ. Secondary/advantage/contested hết lượt thử thì báo thiếu hạn ngạch và bỏ nguyên cụm. Không giữ cụm chỉ có một phần thành viên.

ảnh trạng thái tài nguyên lúc ghi tài liệu: 4 người chơi, MainClusters=3, SecondaryClusters=10, AdvantageClusters=50, ContestedClusters=50; Main.DistanceCells=12 và MinesPerCluster=3. Đây là cấu hình kiểm thử đang chỉnh trong Inspector, không phải khuyến nghị số dư.
ảnh trạng thái này yêu cầu 452 cụm: 12 chính bắt buộc chứa tổng 36 mỏ, 440 tùy chọn cụm. Nếu đặt đủ toàn bộ thì tổng 916–1796 mỏ. hạn ngạch yêu cầu không đồng nghĩa hạn ngạch thực tế đạt được; tài nguyên có thể được chỉnh tiếp sau ảnh trạng thái này.

## 7. Element mẫu đối tượng được sinh đối tượng thế nào?

1. bộ chuyển dữ liệu sang ECS đọc Catalog, kiểm ID/type/prefab và chuyển dữ liệu sang ECS thực thể tham chiếu vào MapResourcePrefab.
2. khởi tạo chờ terrain/chi phí/vùng liên thông sẵn sàng, phân giải roster và giữ chỗ tất cả nhà trên lưới tạm.
3. bộ lập kế hoạch chọn role, số thành viên và công thức. Recipe chọn trước khi tìm vị trí; thử lại không reroll loại mỏ.
4. mẫu tâm và thành viên; lấy kích thước từ GridFootprint/BlockageData của từng mẫu đối tượng.
5. Footprint phải trống, cùng HeightLevel với tâm cụm, không đè núi/cliff/ramp/nhà/unit; không sát mốc cơ sở trong 3 ô. Vùng mở rộng một ô quanh toàn vùng chiếm chỗ không được chứa núi hoặc ô có RampId >= 0, gồm cả góc chéo; giữ thoáng chân và đỉnh dốc nối tầng.
6. lượt thử chỉ ghi chi phí 255 vào vùng chiếm chỗ từng mỏ. Vòng tròn cụm và các ô trống giữa thành viên không bị khối.
7. rà soát giữ đúng 1 vùng liên thông, mọi cơ sở còn nguồn đi được, mọi tài nguyên đã đặt có ô tiếp cận cùng tầng. Private cụm cần có đường từ cơ sở; secondary/advantage còn kiểm ngân sách đường đi của bản thử nghiệm.
8. Sau khi toàn bộ main/nhà/worker kế hoạch hợp lệ mới tạo bản sao đối tượng từng mẫu đối tượng. Mỏ trung lập, không cộng dân số. AmountPerMine bắt buộc được ghi lên ResourceNodeData của instance và xóa ResourceNodePendingConfig trước khi mỏ hoạt động.
9. Gắn MapResourceIdentity, thêm vào hàng đợi chặn lối. khởi tạo chờ chi phí/vùng liên thông/population/setup xong rồi chuyển Ready; không sinh đối tượng lại mỗi khung hình.

Mỏ chỉ được cấu hình qua `MapGenConfig.Resources`. Không gắn MapGenConfig hoặc bật=false thì khởi tạo không tạo mỏ; không có phương án dự phòng trong PlayerBootstrapSettings.

## 8. Đọc báo cáo và kiểm tra

MapResourceReport lưu RequestedClusters, PlacedClusters, Nodes, MainPlacedClusters và MainNodes. Log ghi placed/requested/missing. MapResourceIdentity lưu DefinitionId, Tier, ClusterRole trên thực thể mỏ.

- Menu **`Tools > MapGen > Test resource planner only`**: dữ liệu kiểm thử phẳng riêng, không mở scene/vào Play; báo cáo Artifacts/SpawnSmoke/resource-planner.txt.
- Menu `Tools > MapGen > Test Main resource spawn and grid snapper`: kiểm cảnh Main, footprint/collider, hạn ngạch báo cáo, vùng liên thông, dân số và no-op lưới version; báo cáo Artifacts/SpawnSmoke/report.txt.

Build C# hiện đã qua. Fixture bộ lập kế hoạch mới đã ĐẠT: 16/16 cụm, 41 mỏ; hạt giống deterministic; chính cùng khoảng cách/số lượng/công thức; mapping tier/trữ lượng từ mẫu Amount=0; AmountPerMine=0 bị từ chối; chỉ vùng chiếm chỗ bị khối; thiếu chính reject toàn bộ; tùy chọn bị chặn báo thiếu hạn ngạch. Đây là dữ liệu kiểm thử phẳng riêng, không phải Play Mode Main. Main từng qua headless ở phiên bản tài nguyên trước; kết quả đó không chứng nhận bản danh mục hiện tại. Play Mode Main gần nhất chưa xác nhận Ready/PASS. Chưa có đối chiếu vị trí từng mỏ với JS Lab5 hoặc kiểm định số dư kinh tế.

## 9. mã liên quan

- [MapGenConfig.cs (nguồn lịch sử hiện không còn trên đĩa): lược đồ dữ liệu config/catalog/tier.
- PlayerBootstrapAuthoring.cs (nguồn lịch sử hiện không còn trên đĩa): validation/bake tham chiếu.
- MapResourcePlanner.cs (nguồn lịch sử hiện không còn trên đĩa): công thức, lấy mẫu, path/khả năng tiếp cận/kết nối kiểm tra.
- PlayerBootstrapSystem.cs (nguồn lịch sử hiện không còn trên đĩa): planning/instantiate/apply amount/Ready.
- ResourceAuthoring.cs (nguồn lịch sử hiện không còn trên đĩa): loại tài nguyên và mẫu đang chờ, không lưu trữ lượng.
- WorkerGatherSystem.cs (nguồn lịch sử hiện không còn trên đĩa): cơ chế khai thác hiện tại.


## 10. Trữ lượng tập trung và thư mục kiểm thử

Cập nhật khi chạy 2026-10-12: bộ lập kế hoạch vẫn chạy đồng bộ trước khi publish kế hoạch sinh đối tượng; khởi tạo ghi toàn bộ lệnh vào ECB rồi Playback một lần, remap sổ theo dõi. [luồng xử lý](EcsMapPipeline.md), [bản vá sự cố dừng đột ngột](BootstrapCrashFix.md), [đo hiệu năng](../Maintenance/VerificationHistory.md). Main512 mới nhất: 661/904 cụm, 1.952 tài nguyên, planner20,62 giây; hai lượt Burst bật hoàn thành 600 frame/lượt, sổ theo dõi đúng và không trùng. Các lượt thử dưới đây giữ làm lịch sử; chưa có tối ưu jobs/Burst riêng cho bộ lập kế hoạch.

Theo thay đổi 2026-10-06, nguồn trữ lượng duy nhất là cấu hình sinh đối tượng:
- `MapGenConfig.Resources.Catalog[].AmountPerMine` bắt buộc > 0, không có giá trị 0 để phương án dự phòng về mẫu đối tượng.
- PlayerBootstrapSettings không cấu hình mỏ; AmountPerMine lấy từ danh mục của bản đồ.
- ResourceAuthoring không còn Amount. Prefab/template chuyển dữ liệu sang ECS có ResourceNodeData.Amount=0 và ResourceNodePendingConfig.
- Mỏ live có ResourceNodeData.Amount là trữ lượng còn lại, được giảm khi khai thác; trường khi chạy này vẫn cần thiết.
- khởi tạo gán AmountPerMine rồi bỏ PendingConfig khi tạo bản sao đối tượng. Mỏ đặt tay chưa được cấp cấu hình giữ trạng thái đang chờ, không bị xóa như mỏ cạn; chưa khai thác được. Scene GameScene cũ cần nối config/spawn trước khi dùng mỏ đặt tay.
- Số dư StartingResources và giá mua unit/building vẫn là Amount riêng, không thuộc trữ lượng mỏ.

Helper test đã xóa. Các asset gameplay được Main/registry tham chiếu vẫn ở Assets/_Project/Tests/Fixtures/MapGeneration.
Folder/asset/script được chuyển kèm meta, giữ GUID để Main/registry vẫn tham chiếu đúng.
mã lối chơi dùng chung ở Assets/_Project/Scripts, tài liệu MapGen ở Docs/MapGeneration, báo cáo vẫn ở Artifacts/SpawnSmoke.

### Cách chỉnh trữ lượng sau thay đổi

1. Mở Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset.
2. Trong tài nguyên > Catalog, tìm đúng Id và nhập AmountPerMine > 0 (ví dụ gold.common = 1500).
3. Không tìm Amount trong Inspector mẫu đối tượng: trường thiết lập dữ liệu này đã được bỏ.
4. Rebake/Refresh subscene, rồi bắt đầu trận mới để mỏ sinh đối tượng nhận cấu hình mới.
5. Trong ECS, ResourceNodeData.Amount là lượng còn lại của mỏ đang chơi; đổi cấu hình không tự ghi đè mỏ đang được khai thác.

Nhập AmountPerMine trong danh mục tài nguyên của MapGenConfig; khởi tạo không có MapGenConfig sẽ không tạo mỏ.
AmountPerMine=0 là lỗi cấu hình, không còn nghĩa “lấy lượng từ mẫu đối tượng”.
Bộ kiểm thử được chuyển kèm meta và giữ GUID; scene/registry không cần gán lại tài nguyên chỉ vì đổi đường dẫn.
Kiểm tra sau di chuyển: 19 asset/prefab/scene tham chiếu đúng GUID, không trùng lặp; các khối ResourceAuthoring trong prefab/scene không còn Amount serialized; 56 link tài liệu đã được kiểm tra.

## Khoảng cách với núi

Mỗi tài nguyên căn lưới theo vùng chiếm chỗ lưới. Toàn vùng chiếm chỗ phải là đất đi được cùng tầng, không núi/vách/dốc nối tầng; các ô trong vùng mở rộng một ô quanh vùng chiếm chỗ cũng không được là núi hoặc dốc nối tầng (RampId >= 0), kể cả góc chéo. Quy tắc này áp dụng mọi nhóm tài nguyên, giữ thoáng chân/đỉnh và cạnh dốc nối tầng. Chỉ vùng chiếm chỗ mỏ được giữ chỗ chi phí; vùng đệm không chặn điều hướng. ảnh sprite núi neo vào từng ô và rộng một diamond, không phóng to nhiều ô.

Artwork núi mới phải giữ chân ảnh phù hợp một ô. Vùng đệm1 ô không bảo đảm mọi ảnh quá rộng/cao sẽ không che mỏ; kiểm tra bộ mới trong Main. Thay PNG không đổi vị trí/footprint tài nguyên. [Hướng dẫn ảnh vật liệu](../TerrainVisualizer/05_Atlas_ArtPipeline.md), [hướng dẫn Unity](MapGen_Unity_ReadingGuide.md).

# Tài nguyên trên map: catalog, cluster và spawn

Cập nhật 2026-10-06, sau khi chuyển trữ lượng sang config và gom bộ test. Tài liệu này mô tả code Unity hiện tại để đọc và góp ý, không phải chứng nhận balance kinh tế hay kết quả Play Mode.

Cấu hình nằm trong **MapGenConfig > Resources** tại asset:
[MapGenConfig.asset](../../Assets/Temp/MapGenTest/MapGenConfig.asset).

Resources đang nằm chung trong MapGenConfig để thử. Sau này có thể tách thành asset resource config riêng. PlayerBootstrapSettings.StartingResources là số dư đầu trận của player, độc lập với các mỏ ngoài map.

## 1. Catalog: tài nguyên nào dùng prefab nào?

Catalog là bảng ánh xạ từng biến thể mỏ. Một ResourceType có thể có nhiều ID, ví dụ gold.common / gold.rich / gold.premium. ID biến thể không tạo thêm loại tiền mới: cả ba vẫn có thể cho Gold.

| Trường | Ý nghĩa |
| --- | --- |
| Id | ID duy nhất, không rỗng; tối đa 61 byte UTF-8 |
| Type | Gold/Wood/Food; phải khớp ResourceAuthoring ở root prefab |
| Prefab | Prefab thực sự được instantiate; cần resource authoring, GridSnapper và blockage |
| Tier | Cấp phân bố: Common, Rich, Premium |
| AllowedClusters | Những nhóm được phép chọn definition này, sau khi áp dụng giới hạn Tier |
| Weight | Trọng số chọn giữa các definition cùng tier; không phải giá trị kinh tế |
| AmountPerMine | Trữ lượng bắt buộc > 0 cho mỗi mỏ spawn; prefab không lưu trữ lượng |

ResourceAuthoring trên prefab chỉ giữ Type; không còn trường Amount. Baker tạo template Amount=0, chưa được cấu hình. Catalog cung cấp AmountPerMine khi spawn. Không tạo mỏ Wood/Food bằng cách đổi nhãn prefab Crystal/Gold.

Asset hiện chỉ có:

| ID | Type | Prefab | Tier | AllowedClusters | Weight | AmountPerMine |
| --- | --- | --- | --- | --- | --- | --- |
| gold.common | Gold | TempGold, bản sao Crystal có sẵn | Common | All | 1 | 1500, lấy từ catalog |

Mỏ test có footprint 1×1 ô. Lab JS dùng footprint 2×2; Unity lấy footprint thật sau bake, không ép mọi prefab thành 2×2.

## 2. Giá trị, trữ lượng và tốc độ khai thác

Ba khái niệm hiện khác nhau:

- **Tier** quyết định mỏ được đặt ở nhóm nào. Designer gán tier; code không tự suy tier từ Amount.
- **Amount** là tổng số đơn vị tài nguyên còn lại trong mỏ. AmountPerMine trong config cấp trữ lượng khi spawn, không thay loại tài nguyên.
- **GatherRate** của worker quyết định số đơn vị khai thác mỗi chu kỳ. Worker mang số đơn vị đó về depot; Tier không nhân tiền hay tốc độ khai thác.

Ví dụ có thể cấu hình cùng prefab Gold với hai ID, một mỏ chứa 1500 Gold và một mỏ chứa 6000 Gold. Nếu worker có cùng GatherRate thì tốc độ kiếm Gold như nhau; mỏ 6000 tồn tại lâu hơn. Ví dụ này không phải hai definition đã được thêm vào asset.

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
2. Chọn tier cao nhất còn hợp lệ.
3. Trong tier đó, chọn definition theo Weight.

Do đó contested có Premium thì dùng pool Premium; không có Premium thì có thể dùng Rich, rồi Common. Không trộn các tier trong cùng pool hiện tại. Nếu nhóm có quota nhưng không có definition hợp lệ, planner báo lỗi thay vì tự dùng prefab khác.

Với catalog hiện chỉ có gold.common + All, cả bốn nhóm vẫn dùng Gold thường. Muốn contested có mỏ tốt hơn, thêm definition Rich/Premium với prefab/trữ lượng phù hợp. Dùng cùng prefab cho nhiều biến thể được phép, nhưng hình ảnh giống nhau; MapResourceIdentity giúp inspect ID/tier/role trong ECS.

## 4. Main cluster: gói bắt buộc, cùng cấu hình cho mọi player

| Trường | Ý nghĩa |
| --- | --- |
| MainClusters | Số main cluster mỗi player |
| Main.DistanceCells | Khoảng cách cố định từ mốc spawn tới tâm logic cluster, theo số ô |
| Main.MinesPerCluster | Số mỏ cố định mỗi main cluster |

Main không dùng ClusterMin/ClusterMax hoặc random khoảng bán kính. Chỉ sample góc trên vòng tròn có bán kính DistanceCells. Tâm logic giữ float để không lệch bán kính do làm tròn; các prefab thành viên vẫn snap vào grid.

Recipe được chọn từ pool Common bằng seed và index main cluster, không dùng player ID. Mọi player có cùng quota sẽ nhận cùng ID mỏ, trữ lượng và số lượng cho cluster tương ứng. Weight không khiến recipe main khác nhau giữa player.

Planner reserve main cho mọi player trước các nhóm optional. Không đủ member hoặc không đạt access/connectivity thì thử vị trí khác với cùng distance/count/recipe. Hết lượt thử: rollback plan, bootstrap Failed trước instantiate nhà/quân/mỏ; không bớt mỏ hoặc tự tăng khoảng cách để cho qua.

**Giới hạn của bảo đảm hiện tại:**

- DistanceCells đo tới tâm cluster, không tới từng mỏ thành viên.
- Mốc base hiện là GridSpawnCell.cell, không phải vị trí TownHall thực tế nếu nhà bị placement dịch khỏi spawn.
- Khoảng cách thẳng bằng nhau không bảo đảm đường đi/tốc độ khai thác/độ an toàn bằng nhau.
- Seed mới hoặc cấu hình khác có thể làm main không đặt được; hiện không tự regenerate terrain sau lỗi main.

## 5. Range nghĩa là gì?

Các Vector2 Range có **X = min, Y = max**, nhân với grid.width (W). Đơn vị kết quả là ô grid, không phải world unit và không phải độ dài đường đi.

| Nhóm | Đo quanh | Range mặc định | Map W=512 |
| --- | --- | --- | --- |
| Main | Mốc spawn | DistanceCells = 12, cố định | 12 ô |
| Secondary | Mốc spawn của player | 0.11–0.22 × W | 56.32–112.64 ô |
| Advantage | Mốc spawn của player | 0.23–0.40 × W | 117.76–204.8 ô |
| Contested | Tâm map, trong cung ứng với player | 0.07–0.28 × W | 35.84–143.36 ô |

Generator sample đều theo diện tích vành khăn:
r = sqrt(min² + u × (max² − min²)), với u trong [0,1).

Đây là range của **tâm cluster**. Member có thể nằm gần/xa hơn vì được sample trong bán kính cluster. Secondary không phải range loại tài nguyên; chọn loại mỏ do Catalog/Tier quyết định.

Contested quota tính mỗi cung, không tính owner: mọi mỏ đều trung lập. Audit bảo đảm các base cùng island và tiếp cận mỏ được; hiện không bảo đảm khoảng cách tiếp cận của hai player bằng nhau hoặc tranh chấp kinh tế cân bằng.

## 6. Quota, bán kính và số lần thử

| Trường | Ý nghĩa |
| --- | --- |
| ClusterMin/ClusterMax | Số prefab ngẫu nhiên mỗi secondary/advantage/contested cluster |
| SecondaryClusters/AdvantageClusters | Số cluster yêu cầu mỗi player |
| ContestedClusters | Số cluster yêu cầu mỗi cung của player |
| MinimumClusterRadius | Bán kính cluster tối thiểu, ô |
| RadiusPerSqrtCount | radius = max(minimum, sqrt(count) × hệ số) |
| ClusterGap | Tâm hai cluster cách nhau ít nhất tổng radius + gap |
| MemberSpacing | Khoảng cách tối thiểu giữa tâm member trong cùng cluster |
| CenterAttempts | Số candidate tâm tối đa mỗi cluster |
| MemberAttempts | Số lần sample member tối đa cho mỗi candidate |

Main bắt buộc đặt đủ. Secondary/advantage/contested hết lượt thử thì báo thiếu quota và bỏ nguyên cluster. Không giữ cluster chỉ có một phần member.

Snapshot asset lúc ghi tài liệu: 4 player, MainClusters=3, SecondaryClusters=10, AdvantageClusters=50, ContestedClusters=50; Main.DistanceCells=12 và MinesPerCluster=3. Đây là cấu hình test đang chỉnh trong Inspector, không phải khuyến nghị balance.
Snapshot này yêu cầu 452 cluster: 12 main bắt buộc chứa tổng 36 mỏ, 440 optional cluster. Nếu đặt đủ toàn bộ thì tổng 916–1796 mỏ. Quota yêu cầu không đồng nghĩa quota thực tế đạt được; asset có thể được chỉnh tiếp sau snapshot này.

## 7. Element prefab được spawn thế nào?

1. Baker đọc Catalog, kiểm ID/type/prefab và bake entity reference vào MapResourcePrefab.
2. Bootstrap chờ terrain/cost/island sẵn sàng, resolve roster và reserve tất cả nhà trên lưới tạm.
3. Planner chọn role, số member và recipe. Recipe chọn trước khi tìm vị trí; retry không reroll loại mỏ.
4. Sample tâm và member; lấy kích thước từ GridFootprint/BlockageData của từng prefab.
5. Footprint phải trống, cùng HeightLevel với tâm cluster, không đè núi/cliff/ramp/nhà/unit; không sát mốc base trong 3 ô.
6. Trial chỉ ghi cost 255 vào footprint từng mỏ. Vòng tròn cluster và các ô trống giữa member không bị block.
7. Audit giữ đúng 1 island, mọi base còn nguồn đi được, mọi resource đã đặt có ô tiếp cận cùng tầng. Private cluster cần có đường từ base; secondary/advantage còn kiểm budget đường đi của lab.
8. Sau khi toàn bộ main/nhà/worker plan hợp lệ mới instantiate từng prefab. Mỏ trung lập, không cộng population. AmountPerMine bắt buộc được ghi lên ResourceNodeData của instance và xóa ResourceNodePendingConfig trước khi mỏ hoạt động.
9. Gắn MapResourceIdentity, enqueue blockage. Bootstrap chờ cost/island/population/setup xong rồi chuyển Ready; không spawn lại mỗi frame.

Nếu không gắn MapGenConfig, bootstrap còn hỗ trợ StartingNodes cũ. Nếu có config, Resources thay StartingNodes hoàn toàn; Enabled=false tắt mỏ trên map, không fallback.

## 8. Đọc báo cáo và kiểm tra

MapResourceReport lưu RequestedClusters, PlacedClusters, Nodes, MainPlacedClusters và MainNodes. Log ghi placed/requested/missing. MapResourceIdentity lưu DefinitionId, Tier, ClusterRole trên entity mỏ.

- Menu **Tools > MapGen > Test resource planner only**: fixture phẳng riêng, không mở scene/vào Play; báo cáo Artifacts/SpawnSmoke/resource-planner.txt.
- Menu **Tools > MapGen > Test Main resource spawn and grid snapper**: kiểm scene Main, footprint/collider, quota report, island, population và no-op grid version; báo cáo Artifacts/SpawnSmoke/report.txt.

Build C# hiện đã qua. Fixture planner mới đã PASS: 16/16 cluster, 41 mỏ; seed deterministic; main cùng distance/count/recipe; mapping tier/trữ lượng từ template Amount=0; AmountPerMine=0 bị từ chối; chỉ footprint bị block; thiếu main reject toàn bộ; optional bị chặn báo thiếu quota. Đây là fixture phẳng riêng, không phải Play Mode Main. Main từng qua headless ở phiên bản resource trước; kết quả đó không chứng nhận bản catalog hiện tại. Play Mode Main gần nhất chưa xác nhận Ready/PASS. Chưa có đối chiếu vị trí từng mỏ với JS Lab5 hoặc kiểm định balance kinh tế.

## 9. Code liên quan

- [MapGenConfig.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MapGen/MapGenConfig.cs>): schema config/catalog/tier.
- [PlayerBootstrapAuthoring.cs](<../../Assets/Scripts/Game(New Simulation Logic)/System/PlayerContext/PlayerBootstrapAuthoring.cs>): validation/bake references.
- [MapResourcePlanner.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MapGen/MapResourcePlanner.cs>): recipe, sampling, path/access/connectivity audit.
- [PlayerBootstrapSystem.cs](<../../Assets/Scripts/Game(New Simulation Logic)/System/PlayerContext/PlayerBootstrapSystem.cs>): planning/instantiate/apply amount/Ready.
- [ResourceAuthoring.cs](<../../Assets/Scripts/Game(New Simulation Logic)/CoreECS/Authoring/Resource/ResourceAuthoring.cs>): loại resource và template pending, không lưu trữ lượng.
- [WorkerGatherSystem.cs](<../../Assets/Scripts/Game(New Simulation Logic)/System/Economy/WorkerGatherSystem.cs>): cơ chế khai thác hiện tại.


## 10. Trữ lượng tập trung và thư mục test

Theo thay đổi 2026-10-06, nguồn trữ lượng duy nhất là config spawn:
- MapGenConfig.Resources.Catalog[].AmountPerMine bắt buộc > 0, không có giá trị 0 để fallback về prefab.
- StartingNodes fallback cũng có AmountPerMine > 0 trong PlayerBootstrapSettings.
- ResourceAuthoring không còn Amount. Prefab/template bake có ResourceNodeData.Amount=0 và ResourceNodePendingConfig.
- Mỏ live có ResourceNodeData.Amount là trữ lượng còn lại, được giảm khi khai thác; field runtime này vẫn cần thiết.
- Bootstrap gán AmountPerMine rồi bỏ PendingConfig khi instantiate. Mỏ đặt tay chưa được cấp config giữ trạng thái pending, không bị xóa như mỏ cạn; chưa khai thác được. Scene GameScene cũ cần nối config/spawn trước khi dùng mỏ đặt tay.
- Số dư StartingResources và giá mua unit/building vẫn là Amount riêng, không thuộc trữ lượng mỏ.

Bộ test tạm được gom ở Assets/Temp/MapGenTest: prefab/definition/config/README và Editor/BootstrapSpawnSmokeTest.cs.
Folder/asset/script được chuyển kèm meta, giữ GUID để Main/registry vẫn tham chiếu đúng.
Code gameplay dùng chung vẫn ở Assets/Scripts, docs vẫn ở Docs/Multiplayer, báo cáo vẫn ở Artifacts/SpawnSmoke.

### Cách chỉnh trữ lượng sau thay đổi

1. Mở Assets/Temp/MapGenTest/MapGenConfig.asset.
2. Trong Resources > Catalog, tìm đúng Id và nhập AmountPerMine > 0 (ví dụ gold.common = 1500).
3. Không tìm Amount trong Inspector prefab: trường authoring này đã được bỏ.
4. Rebake/Refresh subscene, rồi bắt đầu trận mới để mỏ spawn nhận config mới.
5. Trong ECS, ResourceNodeData.Amount là lượng còn lại của mỏ đang chơi; đổi config không tự ghi đè mỏ đang được khai thác.

Nếu dùng bootstrap không có MapGenConfig, nhập AmountPerMine trong từng StartingNodes của BootstrapSettings.
AmountPerMine=0 là lỗi cấu hình, không còn nghĩa “lấy lượng từ prefab”.
Bộ test được chuyển kèm meta và giữ GUID; scene/registry không cần gán lại asset chỉ vì đổi đường dẫn.
Kiểm tra sau di chuyển: 19 asset/prefab/scene tham chiếu đúng GUID, không duplicate; các block ResourceAuthoring trong prefab/scene không còn Amount serialized; 56 link tài liệu đã được kiểm tra.

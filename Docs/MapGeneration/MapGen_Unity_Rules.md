# Các rule của địa hình Unity

**Cập nhật 2026-10-12:** đã chuyển sang smart cơ sở, vách chuyển dữ liệu sang ECS, heap ramp/quota, scratch tái sử dụng, pocket sửa và influence đặt nhà; bổ sung sinh dữ liệu khi chạy ECS, khởi tạo ECB, diameter mô hình căn lưới, flat ảnh vật liệu và camera cục bộ. Xem [UnityOptimizationPort.md](UnityOptimizationPort.md), [luồng xử lý ECS](EcsMapPipeline.md) và [đo hiệu năng](../Maintenance/VerificationHistory.md). Các phần placement/ramp lịch sử phía dưới nhường cho rule mới nếu có khác biệt.

Đối chiếu mã C# ngày 2026-10-09. Tra cứu theo [mục lục](MapGen_INDEX.md). địa hình chính thức dùng lưới lối chơi phẳng và độ cao hiển thị riêng; các bản thử nghiệm HTML đã gỡ.

<a id="lưới-lược đồ dữ liệu"></a>
## 1. lưới và dữ liệu một ô

- `index = y * width + x`; bộ đệm địa hình có `width * height` phần tử. lưới `(x, y)` nằm trên thế giới ECS `(X, Z)`.
- Các phép đổi tọa độ ô ↔ chỉ số dùng `GridHelper.GetNodeIndex` và `GetGridPosFromIndex`. Overload nhận row width dùng cho bộ đệm đỉnh (`width + 1`) và lưới khối (`columns`), không lấy nhầm width của địa hình.
- `GridTerrain` gồm `heightLevel`, `walkable`, `isMountain`, `isCliff`, `RampId`, `RampDirection`. ID/direction mặc định là `-1`.
- `walkable` là quyết định đi được dùng chung cho mọi lính. Không có khoảng trống, physicalWalkable hoặc version riêng của địa hình.
- `heightLevel` mô tả tầng xử lý logic; navigation/collider/LocalTransform giữ Y=0. Mọi đỉnh lưới hình học địa hình giữ Y=0; chỉ LocalToWorld của đối tượng được độ lệch trong Presentation theo TerrainVisualSurface.
- `GridAuthoring` lấy giới hạn của plane vuông, có Renderer; `cellsize = bounds.size.x / số ô`. bộ sinh dữ liệu chấp nhận width/height từ 16 đến 512; Inspector hiện có preset 64/128/256/512. Kích thước hợp lệ vẫn có thể thiếu chỗ cho sinh đối tượng và địa hình.

mã: `GridComponent.cs → GridTerrain`; `GridAuthoring.GetGridDefinition`; `TerrainGeneration.ValidateSettings`.

<a id="lưới-helper"></a>
### Quy ước dùng GridHelper để tính chỉ số

bộ sinh dữ liệu và bộ dựng hình dùng chung GridHelper.cs (nguồn lịch sử hiện không còn trên đĩa). Không viết lại `y * width + x`, `% width` hoặc `/ width` tại từng chỗ đổi tọa độ và chỉ số.

| Dữ liệu | Đổi tọa độ sang chỉ số | Chiều rộng hàng |
|---|---|---|
| Ô terrain/chi phí/vùng liên thông | `GetNodeIndex(cell, grid)` | `grid.width` |
| đỉnh độ cao | `GetNodeIndex(vertex, stride)` | `stride = grid.width + 1` |
| Chunk dựng hình | `GetNodeIndex(chunk, columns)` | `columns = ceil(grid.width / side)` |

Đổi ngược dùng `GetGridPosFromIndex(index, grid)` hoặc overload nhận chiều rộng hàng tương ứng. Renderer đổi ô chỉ số → ô, chia tọa độ ô cho `side` để ra tọa độ khối, rồi tính khối chỉ số với `columns`.

`WorldToGrid` dùng origin/cellsize và floor. `GridToWorld` trả tâm ô trên mặt phẳng XZ với Y = 0; caller xử lý độ lệch Y của mẫu đối tượng hoặc layer dựng hình khi cần.

Helper tính chỉ số không kiểm tra giới hạn. Caller phải kiểm tra tọa độ trước khi đọc bộ đệm; không clamp một tọa độ ngoài bản đồ thành ô trong bản đồ. Các độ lệch `+1`, `+stride` khi đọc bốn đỉnh kề nhau vẫn là độ lệch trong bộ đệm đỉnh, không phải một quy ước chỉ số khác.

Phạm vi đã gom: lấy mẫu đỉnh, smoothing/fallback địa hình, đọc ô khi dựng lưới hình học, đổi ô chỉ số trong Quad và ngăn chỉ số của khối. Công thức và kết quả không đổi.

<a id="nhiễu-độ cao"></a>
## 2. RNG, nhiễu và tầng cao

- Giữ RNG/permutation C# hiện có. Không yêu cầu cùng hạt giống phải tạo bản đồ giống JavaScript.
- nhiễu dùng fBm ba octave, lacunarity 2, persistence 0,5. Wrapper đưa kết quả về miền signed để kết hợp landscape/chi tiết.
- bộ sinh dữ liệu duy nhất dùng shared-đỉnh terraces: lấy độ cao ở đỉnh, lượng tử hóa, relaxation khoảng≤1 và loại saddle5/10, rồi chọn tầng majority để tạo bậc địa hình.

- Sau dựng height/núi, LimitHeightSteps hạ mức đến khi mọi cặp ô không phải núi kề tám hướng lệch tối đa một tầng. Đây là quy tắc bắt buộc, không còn flag lượt thử.

mã: `MapGenerator.Perlin2D`, `fbm2D`, `MapGenRNG.GetPermatureList`; `SharedVertexTerrainGeneration.Generate`, `TerrainGeneration.Noise`.

<a id="sinh đối tượng"></a>
## 3. Spawn và vùng bảo vệ

- Chia góc quanh tâm thành các sector cho người chơi; dao động góc và khoảng cách tạo vị trí khác nhau giữa hạt giống.
- Mỗi người chơi thử tối đa 64 vị trí. Tâm sinh đối tượng cách nhau ít nhất `2 * (protectedRadius + 2) + 1` ô.
- hòa trộn đỉnh độ cao quanh sinh đối tượng: vùng phẳng tới `protectedRadius + 3`, chuyển tiếp trong 8 ô; kiểm tra core sau relaxation. phương án dự phòng từ lượt thử 8 ép mức ô trong bán kính `protectedRadius + 4`.
- Vùng được kiểm tra bảo vệ cuối cùng là bán kính `protectedRadius + 2`: cùng mức với sinh đối tượng, đi được và không có núi. Portal dốc nối tầng cũng tránh vùng này.
- Đây là bảo vệ địa hình sinh đối tượng; không phải thuật toán cân bằng tuyệt đối giữa các đội.

mã: `GenerateSpawnCells`, `Protected`, `SharedVertexTerrainGeneration.Generate`, `Validate`.

<a id="núi"></a>
## 4. Núi

- nhiễu núi có mountainWavelength19/mountainThreshold0,20/mountainDensity1/mountainSpawnBuffer4 mặc định; threshold hiệu dụng là `threshold + (1-density)*0.35`, bộ đệm cộng protectedRadius. Preset Main sau bản thử nghiệm dùng26/0,20/0,85/8, lọc cụm nhỏ hơn6 ô. [cấu hình và lab4–6](../TerrainVisualizer/README.md).
- Lấp khe một ô khi ô đó có núi đối diện nhau theo trục ngang hoặc dọc, ngoài vùng bảo vệ.
- Mỗi khối núi liên thông bốn hướng được đưa về mức trung vị và bị chặn.
- Không đào dốc nối tầng xuyên núi; núi không được đi được hoặc mang RampId.

SharedVertexSlopes gọi `BuildMountainClusters` trước kết nối lưới cliff/ramp. mã: `BuildMountainClusters`, `UnifyMountainLevels`, `FindPortal`, `Validate`.

<a id="vách"></a>
## 5. Cliff ở phía cao

- Hai ô kề bốn hướng khác mức tạo ranh giới vách, nếu cả hai không phải núi.
- Đánh `isCliff = true`, `walkable = false` ở phía cao. Bề dày band là `chênh level * cliffCells`, mở rộng theo khoảng cách Manhattan và chỉ đánh các ô cùng mức với ô cao.
- Góc lõm cũng đánh ở ô cao: không có hàng xóm theo bốn hướng chính thấp hơn, nhưng có ô chéo thấp hơn và hai ô theo bốn hướng chính kề góc cùng mức.
- Band vách quyết định đi được; bộ dựng hình dựng vách theo endpoint độ cao của hai ô. Mặt cao bị chặn có thể sâu hơn cạnh vách hiển thị.

mã: `BakeCliffs`, `BakeInnerCorners`, `LowerMask`; `TerrainVisualCompiler.Compile`.

<a id="dốc nối tầng"></a>
## 6. Ramp: hướng, vị trí, chiều rộng và hạn ngạch

- lượt chạy là dãy mặt vách thẳng phía cao, cùng hướng, cùng high/low mức. Mỗi mặt chỉ có một hướng theo bốn hướng chính thấp hơn; mặt góc không được tính vào lượt chạy.
- `RampDirection` là hướng đi từ thấp lên cao: `0 = (0,-1)`, `1 = (1,0)`, `2 = (0,1)`, `3 = (-1,0)`. Nó được ghi trên hai ô high/low tại mặt crossing; không mặc định mọi ô sâu trong corridor đều có hướng.
- Portal chỉ nối high/low chênh đúng một tầng.
- Portal bắt đầu từ ô thấp đi được, xuyên band vách vào phía cao và phải tới được ô cao đi được ngoài band. Không vượt biên, chạm núi, core bảo vệ, dốc nối tầng đã có hoặc vùng reserved.
- Việc mở cổng không được tạo crossing khác mức ngoài mặt high/low đã chọn. Các ô mở mang cùng RampId, vẫn giữ cờ isCliff nếu trước đó là vách.
- `rampWidth` là chiều rộng ưu tiên; thuật toán có thể giảm xuống một ô để tìm cổng hợp lệ.
- Hai nhóm dốc nối tầng khác ID có khoảng cách tối thiểu một ô quanh mặt crossing, kể cả theo đường chéo. Các lane trong cùng nhóm được đứng cạnh nhau.
- Với lượt chạy có `n` ô, hạn ngạch mục tiêu là `max(1, ceil(n/12))` **ô mặt dốc nối tầng**, không phải số nhóm dốc nối tầng. Một nhóm rộng ba ô đóng góp ba vào hạn ngạch.
- Kết nối đảo được xử lý trước, hạn ngạch được bổ sung sau. **mã hiện tại không từ chối bản thử chỉ vì thiếu hạn ngạch khi không còn cổng hợp lệ**; điều kiện một đảo và an toàn địa hình vẫn bắt buộc.

mã: `StraightRuns`, `FindPortal`, `Open`, `OpenCount`, `Generate`, `Validate`; `GridTerrain.RampDirections`.

<a id="kết nối"></a>
## 7. Kết nối đảo và xử lý túi nhỏ

- Island địa hình được flood-fill theo bốn hướng trên đi được.
- Ưu tiên mở cổng nối ít nhất hai thành phần khác nhau; dùng union để cập nhật các thành phần đã nối.
- Nếu vẫn còn đảo rời, tất cả sinh đối tượng phải ở thành phần của sinh đối tượng đầu tiên. Island nhỏ không chứa sinh đối tượng được chuyển thành núi nếu không nối được bằng cổng hợp lệ. Không giới hạn kích thước từng vùng liên thông; tổng lấp tối đa `smallIslandFillBudget` mặc định 2% số ô bản đồ (làm tròn xuống). Không lấp vùng liên thông chạm protected sinh đối tượng core; kiểm tra toàn bộ giới hạn trước khi sửa ô.
- Túi bị lấp chuyển thành núi, bỏ cliff/ramp và không đi được. Sau đó thống nhất lại mức núi và tính lại lượt chạy.
- Kết quả được chấp nhận phải có đúng một đảo đi được.

mã: `ComponentLabels`, `Connect`, `UnifyMountainLevels`, `Generate`.

<a id="thử lại-kiểm tra hợp lệ"></a>
## 8. thử lại, phương án dự phòng và điều kiện nhận bản đồ

- Tối đa 16 bản thử; spawn/mountain dùng hạt giống + lượt thử × 7919; độ cao nhiễu giữ permutation từ hạt giống và dịch mẫu theo lượt thử.
- Từ bản thử thứ chín, dùng trung vị mức theo khối có cạnh `max(6, rampWidth + 3)` để tăng khả năng có ranh giới thẳng; bước này diễn ra trước khi chuyển dữ liệu sang ECS núi/cliff.
- Validate bắt buộc: một vùng liên thông; sinh đối tượng core được bảo vệ; núi không bị đào; vách đi được phải có RampId; crossing khác tầng phải qua dốc nối tầng trên mặt thẳng; nhóm dốc nối tầng giữ spacing.
- Candidate lỗi kết nối hoặc kiểm tra hợp lệ bị bỏ. Hết 16 lần thì báo lỗi, không trả một bản đồ vi phạm các điều kiện trên.
- Giới hạn thiết lập: 2–10 người chơi; protectedRadius ≥ 1; wavelength, levelStep, landscapeHeight > 0; detailAmplitude ≥ 0; cliffCells 1–3; rampWidth 1–12.

mã: `Generate`, `ValidateSettings`, `SharedVertexTerrainGeneration.Generate`, `Validate`.

<a id="lối chơi-lưới"></a>
## 9. chuyển dữ liệu sang ECS, chi phí và chuyển động

- bộ chuyển dữ liệu sang ECS ghi công thức và bộ đệm rỗng vào lưới ECS; MapTerrainGenerationSystem sinh terrain/spawn ở khi chạy rồi publish TerrainReady. Plane gốc dùng DisableRendering nhưng giữ khối va chạm. [luồng xử lý ECS](EcsMapPipeline.md).
- GridInit chờ TerrainReady, tạo chi phí: đi được → 1, blocked → 255; khởi tạo vùng liên thông bộ đệm và đánh dấu initialized theo từng lưới, không tắt toàn hệ thống sau lần đầu. Không live baking lưới.
- CostChange giữ các ô địa hình bị chặn ở chi phí 255, kể cả khi một obstacle động được gỡ bỏ. HeightLevel không tự quyết định chi phí.
- Lệnh đổi đích đánh dấu mục tiêu resolution cần xử lý lại; assignment tạo/chọn trường cho đích điều hướng. TargetChangeRequest được dọn dẹp cuối FixedStep sau assignment/actuator.
- Unit khởi tạo ở ô bị chặn được xử lý một lần bằng facility thiết lập vị trí hiện có. Đây không phải dịch chuyển lính mỗi khung hình.

mã: `GridAuthoring.Baker`, `GridInitSystem`, `CostChangeSystem`, `MovementAgentAPI.SetTarget`, `FlowFieldAssignmentSystem`, `TargetRequestCleanupSystem`, `SetupUnitDefaultPositionSystem.cs`.

<a id="hình ảnh"></a>
## 10. Compiler và hiển thị

Compiler đọc địa hình, ghi metadata ramp/cliff; không sửa điều hướng. bề mặt dùng cùng metadata cho lấy mẫu và chọn bằng tia. Rise theo constants sprite128/32 và camera30°. MapTerrainPresentationSystem bản dựng ảnh sprite meshes/atlas theo phiên bản; thử nghiệm ép phẳng thành một ảnh vật liệu đã xóa. [Đường dựng hình hiện hành](EcsMapPipeline.md).

CanonicalIsometricSprites chọn PNG theo mặt nạ; mỗi ảnh sprite núi neo vào một tâm ô, rộng đúng một diamond. địa hình lưới hình học local/world Y=0; gốc lính có địa hình height/pivot qua PendingHeight. lối chơi dùng XZ; chọn bằng tia dùng dữ liệu logic/surface và khối va chạm hiện hành, không đọc pixel ảnh vật liệu. Legacy độ lệch chỉ còn dùng trong kiểm thử lịch sử. Alignment/occlusion tự nhiên của lính trên plateau vẫn cần hình ảnh kiểm thử.

Xem [luồng xử lý](EcsMapPipeline.md), [đẳng trục](../TerrainVisualizer/02_PhepChieu_RenderOrder.md) và [lược đồ dữ liệu](../TerrainVisualizer/05_Atlas_ArtPipeline.md).

<a id="bộ giao diện-khối"></a>
## 11. bộ giao diện và vòng đời dựng hình

Main dùng XianxiaPolishedTheme với PNG top/cliff,16 mặt đỉnh núi ghép shared-góc và4 accent nhỏ; bộ giao diện chuẩn/cũ giữ phương án dự phòng mountainArtwork. Sort toàn bản đồ theo face độ sâu rồi chia batch2048 ảnh sprite; atlas/material chung. Mọi lưới hình học giữ Y=0, không khối va chạm địa hình nâng cao. Clear giải phóng mesh/material/atlas và deactivate bề mặt.

## 12. Tài nguyên

tài nguyên bộ lập kế hoạch căn lưới theo vùng chiếm chỗ mẫu đối tượng; toàn vùng chiếm chỗ là đất đi được cùng tầng, không núi/vách/dốc nối tầng. Các điều kiện chính balance/quota/accessibility vẫn áp dụng.

Quy tắc bổ sung 2026-10-10: **tài nguyên không được sinh đối tượng ở chân hoặc đỉnh dốc nối tầng**.

- Vùng mở rộng một ô quanh toàn vùng chiếm chỗ của từng mỏ không được chứa ô dốc nối tầng (`RampId >= 0`), kể cả góc chéo. Quy tắc giữ thoáng cả chân, đỉnh và cạnh dốc nối tầng, áp dụng cho mọi nhóm tài nguyên và mọi kích thước vùng chiếm chỗ.
- Nhận diện dốc nối tầng bằng `RampId`, không chỉ dựa vào `isCliff` hoặc `RampDirection`, vì các ô trong corridor có thể không có hướng.
- Vùng mở rộng này cũng không được chứa núi, giữ quy tắc vùng đệm núi hiện có.
- Chỉ vùng chiếm chỗ mỏ được giữ chỗ chi phí; vùng đệm không chặn điều hướng và không thay đổi địa hình.
- Không đủ vị trí hợp lệ thì thử vị trí khác theo bộ lập kế hoạch hiện có; không bỏ khoảng đệm để đạt hạn ngạch. Thiếu chính bắt buộc vẫn từ chối toàn bộ kế hoạch; tùy chọn có thể báo thiếu hạn ngạch.

Kiểm tra bộ lập kế hoạch đã ĐẠT bốn hướng dốc nối tầng ngày 2026-10-10; kiểm tra hình ảnh Main vẫn cần thực hiện riêng.

Xem [tài nguyên](MapGen_ResourceSpawn_Config.md) và [kiểm tra Unity](MapGen_Unity_ReadingGuide.md).

## 13. Quy trình thay đổi

Đổi seed/noise/topology trong cấu hình hoặc mã cần chuyển dữ liệu sang ECS lại và kiểm tra sinh đối tượng, crossing, núi và vùng liên thông. Đổi phong cách hình ảnh mỹ thuật giữ nguyên canvas/mask/pivot và chọn bộ giao diện mới; không sửa đi được để chữa lỗi hình. [Hướng dẫn Unity](MapGen_Unity_ReadingGuide.md) và [ảnh vật liệu thiết lập dữ liệu](../TerrainVisualizer/05_Atlas_ArtPipeline.md) mô tả các bước.

Hợp đồng ảnh sprite hiện128/32, camera30°/45°. Tỷ lệ mới hoặc núi autotile cần cập nhật data/runtime/baker/validation cùng nhau. Unit3D che khuất sau vách chưa hoàn chỉnh; đừng coi meshY0 là bằng chứng che khuất đã đúng.

Trong đợt cải thiện hình ảnh giai đoạn 1–3, giữ quy tắc tài nguyên tránh dốc nối tầng làm điều kiện nghiệm thu: lưu ảnh chân/đỉnh dốc nối tầng ở mốc Main ban đầu; kiểm tra quân đi qua hai chiều và tài nguyên không lấn vùng đệm sau khi chỉnh đất/ramp/vách; chạy lại dữ liệu kiểm thử tài nguyên bộ lập kế hoạch khi áp dụng toàn bản đồ. Thay bộ giao diện không được đổi vùng chiếm chỗ hoặc quy tắc đặt tài nguyên.

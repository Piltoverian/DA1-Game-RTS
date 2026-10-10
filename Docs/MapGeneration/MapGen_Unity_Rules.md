# Các rule của terrain Unity

Đối chiếu code C# ngày 2026-10-09. Tra cứu theo [mục lục](MapGen_INDEX.md). Terrain chính thức dùng grid gameplay phẳng và độ cao hiển thị riêng; các lab HTML đã gỡ.

<a id="grid-schema"></a>
## 1. Grid và dữ liệu một ô

- `index = y * width + x`; buffer terrain có `width * height` phần tử. Grid `(x, y)` nằm trên world `(X, Z)`.
- Các phép đổi tọa độ ô ↔ index dùng `GridHelper.GetNodeIndex` và `GetGridPosFromIndex`. Overload nhận row width dùng cho buffer vertex (`width + 1`) và grid chunk (`columns`), không lấy nhầm width của terrain.
- `GridTerrain` gồm `heightLevel`, `walkable`, `isMountain`, `isCliff`, `RampId`, `RampDirection`. ID/direction mặc định là `-1`.
- `walkable` là quyết định đi được dùng chung cho mọi unit. Không có clearance, physicalWalkable hoặc version riêng của terrain.
- `heightLevel` mô tả tầng logic; navigation/collider/LocalTransform giữ Y=0. Mọi vertex mesh terrain giữ Y=0; chỉ LocalToWorld của đối tượng được offset trong Presentation theo TerrainVisualSurface.
- `GridAuthoring` lấy bounds của plane vuông, có Renderer; `cellsize = bounds.size.x / số ô`. Generator chấp nhận width/height từ 16 đến 512; Inspector hiện có preset 64/128/256/512. Kích thước hợp lệ vẫn có thể thiếu chỗ cho spawn và terrain.

Code: `GridComponent.cs → GridTerrain`; `GridAuthoring.GetGridDefinition`; `TerrainGeneration.ValidateSettings`.

<a id="grid-helper"></a>
### Quy ước dùng GridHelper để tính index

Generator và renderer dùng chung [GridHelper.cs](<../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Helpers/GridHelper.cs>). Không viết lại `y * width + x`, `% width` hoặc `/ width` tại từng chỗ đổi tọa độ và index.

| Dữ liệu | Đổi tọa độ sang index | Chiều rộng hàng |
|---|---|---|
| Ô terrain/cost/island | `GetNodeIndex(cell, grid)` | `grid.width` |
| Vertex height | `GetNodeIndex(vertex, stride)` | `stride = grid.width + 1` |
| Chunk render | `GetNodeIndex(chunk, columns)` | `columns = ceil(grid.width / side)` |

Đổi ngược dùng `GetGridPosFromIndex(index, grid)` hoặc overload nhận chiều rộng hàng tương ứng. Renderer đổi cell index → cell, chia tọa độ cell cho `side` để ra tọa độ chunk, rồi tính chunk index với `columns`.

`WorldToGrid` dùng origin/cellsize và floor. `GridToWorld` trả tâm ô trên mặt phẳng XZ với Y = 0; caller xử lý offset Y của prefab hoặc layer render khi cần.

Helper tính index không kiểm tra bounds. Caller phải kiểm tra tọa độ trước khi đọc buffer; không clamp một tọa độ ngoài map thành ô trong map. Các offset `+1`, `+stride` khi đọc bốn vertex kề nhau vẫn là offset trong buffer vertex, không phải một quy ước index khác.

Phạm vi đã gom: sampling vertex, smoothing/fallback terrain, đọc cell khi dựng mesh, đổi cell index trong Quad và bucket index của chunk. Công thức và kết quả không đổi.

<a id="noise-height"></a>
## 2. RNG, noise và tầng cao

- Giữ RNG/permutation C# hiện có. Không yêu cầu cùng seed phải tạo map giống JavaScript.
- Noise dùng fBm ba octave, lacunarity 2, persistence 0,5. Wrapper đưa kết quả về miền signed để kết hợp landscape/detail.
- Main dùng SharedVertexSlopes: lấy height ở vertex, lượng tử hóa, relaxation range≤1 và loại saddle5/10, rồi chọn tầng majority để tạo terrace. Nhánh Legacy dùng landscape/domain warp/ridge.
- Landscape Legacy gồm domain warp, thành phần địa hình lớn và ridge. Wavelength của địa hình lớn phụ thuộc kích thước map; `wavelength` trong settings điều khiển noise chi tiết.
- Trong Legacy, chi tiết giảm dần gần spawn. Height thô lấy ở vertex, làm tròn theo bước `1/256`; height mỗi ô là trung bình bốn vertex, rồi lượng tử hóa theo `levelStep`.
- Legacy có ba lượt làm mượt chọn level xuất hiện nhiều nhất trong lân cận 3×3, giữ level hiện tại nếu nó đồng hạng tốt nhất.

- Sau dựng height/núi, LimitHeightSteps hạ level đến khi mọi cặp ô không phải núi kề tám hướng lệch tối đa một tầng. Đây là quy tắc bắt buộc, không còn flag trial.

Code: `MapGenerator.Perlin2D`, `fbm2D`, `MapGenRNG.GetPermatureList`; `TerrainGeneration.Noise`, `BuildLevelsAndMountains`.

<a id="spawn"></a>
## 3. Spawn và vùng bảo vệ

- Chia góc quanh tâm thành các sector cho player; jitter góc và khoảng cách tạo vị trí khác nhau giữa seed.
- Mỗi player thử tối đa 64 vị trí. Tâm spawn cách nhau ít nhất `2 * (protectedRadius + 2) + 1` ô.
- Blend height quanh spawn, rồi ép cùng level trong bán kính `protectedRadius + 4`. Viền thêm này giúp cliff phía cao và góc lõm nằm ngoài core.
- Vùng được kiểm tra bảo vệ cuối cùng là bán kính `protectedRadius + 2`: cùng level với spawn, walkable và không có núi. Portal ramp cũng tránh vùng này.
- Đây là bảo vệ địa hình spawn; không phải thuật toán cân bằng tuyệt đối giữa các đội.

Code: `GenerateSpawnCells`, `Protected`, `BuildLevelsAndMountains`, `Validate`.

<a id="mountain"></a>
## 4. Núi

- Noise núi có mountainWavelength19/mountainThreshold0,20/mountainDensity1/mountainSpawnBuffer4 mặc định; threshold hiệu dụng là `threshold + (1-density)*0.35`, buffer cộng protectedRadius. Preset Main sau lab dùng26/0,20/0,85/8, lọc cụm nhỏ hơn6 ô. [Config và lab4–6](TerrainPolish_Remaining.md).
- Lấp khe một ô khi ô đó có núi đối diện nhau theo trục ngang hoặc dọc, ngoài vùng bảo vệ.
- Mỗi khối núi liên thông bốn hướng được đưa về level median và bị chặn.
- Không đào ramp xuyên núi; núi không được walkable hoặc mang RampId.

Cả Legacy và SharedVertexSlopes gọi `BuildMountainClusters` trước topology cliff/ramp. Code: `BuildMountainClusters`, `UnifyMountainLevels`, `FindPortal`, `Validate`.

<a id="cliff"></a>
## 5. Cliff ở phía cao

- Hai ô kề bốn hướng khác level tạo ranh giới cliff, nếu cả hai không phải núi.
- Đánh `isCliff = true`, `walkable = false` ở phía cao. Bề dày band là `chênh level * cliffCells`, mở rộng theo khoảng cách Manhattan và chỉ đánh các ô cùng level với ô cao.
- Góc lõm cũng đánh ở ô cao: không có hàng xóm cardinal thấp hơn, nhưng có ô chéo thấp hơn và hai ô cardinal kề góc cùng level.
- Band cliff quyết định đi được; renderer dựng vách theo endpoint height của hai ô. Mặt cao bị chặn có thể sâu hơn cạnh vách hiển thị.

Code: `BakeCliffs`, `BakeInnerCorners`, `LowerMask`; `TerrainVisualCompiler.Compile`.

<a id="ramp"></a>
## 6. Ramp: hướng, vị trí, chiều rộng và quota

- Run là dãy mặt cliff thẳng phía cao, cùng hướng, cùng high/low level. Mỗi mặt chỉ có một hướng cardinal thấp hơn; mặt góc không được tính vào run.
- `RampDirection` là hướng đi từ thấp lên cao: `0 = (0,-1)`, `1 = (1,0)`, `2 = (0,1)`, `3 = (-1,0)`. Nó được ghi trên hai ô high/low tại mặt crossing; không mặc định mọi ô sâu trong corridor đều có direction.
- Portal chỉ nối high/low chênh đúng một tầng.
- Portal bắt đầu từ ô thấp walkable, xuyên band cliff vào phía cao và phải tới được ô cao walkable ngoài band. Không vượt biên, chạm núi, core bảo vệ, ramp đã có hoặc vùng reserved.
- Việc mở portal không được tạo crossing khác level ngoài mặt high/low đã chọn. Các ô mở mang cùng RampId, vẫn giữ cờ isCliff nếu trước đó là cliff.
- `rampWidth` là chiều rộng ưu tiên; thuật toán có thể giảm xuống một ô để tìm portal hợp lệ.
- Hai nhóm ramp khác ID có khoảng cách tối thiểu một ô quanh mặt crossing, kể cả theo đường chéo. Các lane trong cùng nhóm được đứng cạnh nhau.
- Với run có `n` ô, quota mục tiêu là `max(1, ceil(n/12))` **ô mặt ramp**, không phải số nhóm ramp. Một nhóm rộng ba ô đóng góp ba vào quota.
- Kết nối đảo được xử lý trước, quota được bổ sung sau. **Code hiện tại không từ chối candidate chỉ vì thiếu quota khi không còn portal hợp lệ**; điều kiện một đảo và an toàn terrain vẫn bắt buộc.

Code: `StraightRuns`, `FindPortal`, `Open`, `OpenCount`, `Generate`, `Validate`; `GridTerrain.RampDirections`.

<a id="connectivity"></a>
## 7. Kết nối đảo và xử lý túi nhỏ

- Island terrain được flood-fill theo bốn hướng trên walkable.
- Ưu tiên mở portal nối ít nhất hai component khác nhau; dùng union để cập nhật các component đã nối.
- Nếu vẫn còn đảo rời, tất cả spawn phải ở component của spawn đầu tiên. Mỗi túi khác được lấp tối đa 64 ô; tổng lấp không vượt 2% số ô của map.
- Túi bị lấp chuyển thành núi, bỏ cliff/ramp và không đi được. Sau đó unify lại level núi và tính lại run.
- Kết quả được chấp nhận phải có đúng một đảo walkable.

Code: `ComponentLabels`, `Connect`, `UnifyMountainLevels`, `Generate`.

<a id="retry-validation"></a>
## 8. Retry, fallback và điều kiện nhận map

- Tối đa 16 candidate, mỗi candidate dùng seed đã mix từ seed đầu vào và số lần thử.
- Từ candidate thứ chín, dùng median level theo block có cạnh `max(6, rampWidth + 3)` để tăng khả năng có ranh giới thẳng; bước này diễn ra trước khi bake núi/cliff.
- Validate bắt buộc: một island; spawn core được bảo vệ; núi không bị đào; cliff walkable phải có RampId; crossing khác tầng phải qua ramp trên mặt thẳng; nhóm ramp giữ spacing.
- Candidate lỗi kết nối hoặc validation bị bỏ. Hết 16 lần thì báo lỗi, không trả một map vi phạm các điều kiện trên.
- Giới hạn settings: 2–10 player; protectedRadius ≥ 1; wavelength, levelStep, landscapeHeight > 0; detailAmplitude ≥ 0; cliffCells 1–3; rampWidth 1–12.

Code: `Generate`, `ValidateSettings`, `BuildLevelsAndMountains`, `Validate`.

<a id="gameplay-grid"></a>
## 9. Bake, cost và movement

- Baker ghi terrain và spawn vào grid ECS. Nó ẩn plane gốc bằng DisableRendering nhưng giữ collider.
- GridInit tạo cost: walkable → 1, blocked → 255; khởi tạo island buffer và tắt sau lần khởi tạo đầu. Không live baking grid.
- CostChange giữ các ô terrain bị chặn ở cost 255, kể cả khi một obstacle động được gỡ bỏ. HeightLevel không tự quyết định cost.
- Lệnh đổi đích đánh dấu target resolution cần xử lý lại; assignment tạo/chọn field cho đích navigation. TargetChangeRequest được cleanup cuối FixedStep sau assignment/actuator.
- Unit khởi tạo ở ô bị chặn được xử lý một lần bằng facility setup vị trí hiện có. Đây không phải dịch chuyển unit mỗi frame.

Code: `GridAuthoring.Baker`, `GridInitSystem`, `CostChangeSystem`, `MovementAgentAPI.SetTarget`, `FlowFieldAssignmentSystem`, `TargetRequestCleanupSystem`, `SetupUnitDefaultPositionSystem.cs`.

<a id="visual"></a>
## 10. Compiler và presentation

Compiler đọc terrain, ghi metadata ramp/cliff; không sửa navigation. Surface dùng cùng metadata cho sampling và picking. Rise theo constants sprite128/32 và camera30°; không còn slider cao độ hoặc nhánh render khác.

CanonicalIsometricSprites chọn PNG theo mask; mỗi sprite núi neo vào một tâm ô, rộng đúng một diamond. Terrain mesh local/world Y=0. LocalTransform/physics giữ Y=0; Offset/Restore chỉ thay ma trận render của đối tượng. Picking trả tọa độ logic phẳng.

Xem [pipeline](MapGen_TerrainPresentation.md), [isometric](MapGen_CanonicalIsometricSprites.md) và [schema](MapGen_TextureSchema.md).

<a id="theme-chunk"></a>
## 11. Theme và vòng đời render

Main dùng XianxiaPolishedTheme với PNG top/cliff,16 cap núi ghép shared-corner và4 accent nhỏ; theme chuẩn/cũ giữ fallback mountainArtwork. Sort toàn map theo face depth rồi chia batch2048 sprite; atlas/material chung. Mọi mesh giữ Y=0, không collider terrain nâng cao. Clear giải phóng mesh/material/atlas và deactivate surface.

## 12. Tài nguyên

Resource planner snap theo footprint prefab; toàn footprint là đất walkable cùng tầng, không mountain/cliff/ramp. Các điều kiện main balance/quota/accessibility vẫn áp dụng.

Quy tắc bổ sung 2026-10-10: **resource không được spawn ở chân hoặc đỉnh ramp**.

- Vùng mở rộng một ô quanh toàn footprint của từng mỏ không được chứa ô ramp (`RampId >= 0`), kể cả góc chéo. Quy tắc giữ thoáng cả chân, đỉnh và cạnh ramp, áp dụng cho mọi nhóm tài nguyên và mọi kích thước footprint.
- Nhận diện ramp bằng `RampId`, không chỉ dựa vào `isCliff` hoặc `RampDirection`, vì các ô trong corridor có thể không có direction.
- Vùng mở rộng này cũng không được chứa núi, giữ quy tắc vùng đệm núi hiện có.
- Chỉ footprint mỏ được reserve cost; vùng đệm không chặn navigation và không thay đổi terrain.
- Không đủ vị trí hợp lệ thì thử vị trí khác theo planner hiện có; không bỏ khoảng đệm để đạt quota. Thiếu main bắt buộc vẫn từ chối toàn bộ plan; optional có thể báo thiếu quota.

Kiểm tra planner đã PASS bốn hướng ramp ngày 2026-10-10; kiểm tra hình ảnh Main vẫn cần thực hiện riêng.

Xem [resource](MapGen_ResourceSpawn_Config.md) và [kiểm tra Unity](MapGen_Unity_ReadingGuide.md).

## 13. Quy trình thay đổi

Đổi seed/noise/topology trong config hoặc code cần rebake và kiểm tra spawn, crossing, núi và island. Đổi phong cách art giữ nguyên canvas/mask/pivot và chọn theme mới; không sửa walkable để chữa lỗi hình. [Hướng dẫn Unity](MapGen_Unity_ReadingGuide.md) và [texture authoring](MapGen_TextureAuthoring.md) mô tả các bước.

Hợp đồng sprite hiện128/32, camera30°/45°. Tỷ lệ mới hoặc mountain autotile cần cập nhật data/runtime/baker/validation cùng nhau. Unit3D occlusion sau cliff chưa hoàn chỉnh; đừng coi meshY0 là bằng chứng che khuất đã đúng.

Trong đợt cải thiện hình ảnh giai đoạn 1–3, giữ quy tắc resource tránh ramp làm điều kiện nghiệm thu: lưu ảnh chân/đỉnh ramp ở mốc Main ban đầu; kiểm tra quân đi qua hai chiều và resource không lấn vùng đệm sau khi chỉnh đất/ramp/vách; chạy lại fixture resource planner khi áp dụng toàn map. Thay theme không được đổi footprint hoặc quy tắc đặt resource.

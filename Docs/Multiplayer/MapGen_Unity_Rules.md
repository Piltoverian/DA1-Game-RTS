# Các rule của terrain Unity

Đối chiếu code C# ngày 2026-10-06. Tra cứu theo [mục lục](MapGen_INDEX.md). File này giải thích bản Unity đang dùng trong gameplay; lab HTML là nguồn tham khảo và có thể khác quy ước collision hiện tại.

<a id="grid-schema"></a>
## 1. Grid và dữ liệu một ô

- `index = y * width + x`; buffer terrain có `width * height` phần tử. Grid `(x, y)` nằm trên world `(X, Z)`.
- Các phép đổi tọa độ ô ↔ index dùng `GridHelper.GetNodeIndex` và `GetGridPosFromIndex`. Overload nhận row width dùng cho buffer vertex (`width + 1`) và grid chunk (`columns`), không lấy nhầm width của terrain.
- `GridTerrain` gồm `heightLevel`, `walkable`, `isMountain`, `isCliff`, `RampId`, `RampDirection`. ID/direction mặc định là `-1`.
- `walkable` là quyết định đi được dùng chung cho mọi unit. Không có clearance, physicalWalkable hoặc version riêng của terrain.
- `heightLevel` mô tả tầng logic; mesh gameplay vẫn phẳng, không nâng unit theo tầng.
- `GridAuthoring` lấy bounds của plane vuông, có Renderer; `cellsize = bounds.size.x / số ô`. Generator chấp nhận width/height từ 16 đến 512; Inspector hiện có preset 64/128/256/512. Kích thước hợp lệ vẫn có thể thiếu chỗ cho spawn và terrain.

Code: `GridComponent.cs → GridTerrain`; `GridAuthoring.GetGridDefinition`; `TerrainGeneration.ValidateSettings`.

<a id="grid-helper"></a>
### Quy ước dùng GridHelper để tính index

Generator và renderer dùng chung [GridHelper.cs](<../../Assets/Scripts/Game(New Simulation Logic)/MovementAgent/Helpers/GridHelper.cs>). Không viết lại `y * width + x`, `% width` hoặc `/ width` tại từng chỗ đổi tọa độ và index.

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
- Landscape gồm domain warp, thành phần địa hình lớn và ridge. Wavelength của địa hình lớn phụ thuộc kích thước map; `wavelength` trong settings điều khiển noise chi tiết.
- Chi tiết giảm dần gần spawn. Height thô lấy ở vertex, làm tròn theo bước `1/256`; height mỗi ô là trung bình bốn vertex, rồi lượng tử hóa theo `levelStep`.
- Ba lượt làm mượt chọn level xuất hiện nhiều nhất trong lân cận 3×3, giữ level hiện tại nếu nó đồng hạng tốt nhất.

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

- Noise núi có wavelength 19, threshold `> 0.20`; không đặt núi trong bán kính bảo vệ `protectedRadius + 4`.
- Lấp khe một ô khi ô đó có núi đối diện nhau theo trục ngang hoặc dọc, ngoài vùng bảo vệ.
- Mỗi khối núi liên thông bốn hướng được đưa về level median và bị chặn.
- Không đào ramp xuyên núi; núi không được walkable hoặc mang RampId.

Code: `BuildLevelsAndMountains`, `UnifyMountainLevels`, `FindPortal`, `Validate`.

<a id="cliff"></a>
## 5. Cliff ở phía cao

- Hai ô kề bốn hướng khác level tạo ranh giới cliff, nếu cả hai không phải núi.
- Đánh `isCliff = true`, `walkable = false` ở phía cao. Bề dày band là `chênh level * cliffCells`, mở rộng theo khoảng cách Manhattan và chỉ đánh các ô cùng level với ô cao.
- Góc lõm cũng đánh ở ô cao: không có hàng xóm cardinal thấp hơn, nhưng có ô chéo thấp hơn và hai ô cardinal kề góc cùng level.
- Sprite mặt cliff nằm ở ô cao; với band dày hơn một ô, collision có thể kéo sâu hơn phần sprite mặt cliff.

Code: `BakeCliffs`, `BakeInnerCorners`, `LowerMask`; `TerrainVisualCompiler.Compile`.

<a id="ramp"></a>
## 6. Ramp: hướng, vị trí, chiều rộng và quota

- Run là dãy mặt cliff thẳng phía cao, cùng hướng, cùng high/low level. Mỗi mặt chỉ có một hướng cardinal thấp hơn; mặt góc không được tính vào run.
- `RampDirection` là hướng đi từ thấp lên cao: `0 = (0,-1)`, `1 = (1,0)`, `2 = (0,1)`, `3 = (-1,0)`. Nó được ghi trên hai ô high/low tại mặt crossing; không mặc định mọi ô sâu trong corridor đều có direction.
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
## 10. Compiler và sprite

- Compiler chỉ đọc terrain và xuất cell/tile/rotation/layer; không sửa walkable hoặc level.
- ID sprite: 0 straight, 1 outer corner, 2 inner corner; 3 ramp single, 4 left, 5 middle, 6 right; 7 grass, 8 soil.
- Ramp được vẽ ở ô cao tại crossing; số lane quyết định single hoặc left/middle/right. Cliff dùng cardinal mask và trường hợp góc chéo.
- Ô thường có lớp grass; soil hiện được dùng trong ảnh ramp, chưa có rule sinh biome soil độc lập. Núi render bằng màu riêng.
- Các mặt cliff phức hợp có thể cần nhiều phần sprite chồng trên một ô; bộ art hiện tại không có biến thể riêng cho mọi tổ hợp.

Code: `TerrainVisualCompiler.Compile`, `AddPart`; `TerrainMapRenderer.BuildTerrain`.

<a id="theme-chunk"></a>
## 11. Theme, chunk và vòng đời render

- Theme dùng chín biến Sprite có tên, chia nhóm Cliffs/Ramps/Ground. GetSprite ánh xạ ID nội bộ sang biến tương ứng; designer không phải nhớ thứ tự array.
- Sprite cần dùng texture riêng, không packed; giữ hướng gốc phù hợp với rotation của compiler. Đổi theme không sinh lại terrain hoặc đổi navigation.
- Renderer chờ grid của subscene đã bake rồi dựng map; không gen một map thứ hai. Nó gom mesh theo chunk, mỗi loại sprite là một submesh dùng material riêng.
- `chunkSize` là số ô mỗi cạnh, được clamp trong 4–32. Mặc định 16: chunk tối đa 256 ô; map 256×256 có 256 chunk không gian. Chunk chỉ ảnh hưởng render.
- Mesh/material tạo lúc render được giải phóng khi component bị disable hoặc destroy. Có thể dựng lại khi component được bật lại.

Code: `TerrainTheme.GetSprite`; `TerrainMapRenderer.Update`, `BuildTerrain`, `Quad`, `MakeSpriteChunk`, `Clear`.

Thao tác trong Inspector và Main: [hướng dẫn gameplay](MapGen_Unity_ReadingGuide.md).

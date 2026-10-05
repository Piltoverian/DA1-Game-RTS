# Kế Hoạch Triển Khai Chi Tiết: Các Function Cốt Lõi Từ 1 Đến 4 Của MapGenerator

> **Cập nhật 2026-10-03:** Thuật toán Lab 5 đã được người dùng chốt. Bước tiếp theo là port nguyên thuật toán sang Unity theo [kế hoạch port](Archive/2026-10-04/MapGen_NextSteps_2026-10-03.md). Các mô tả thuật toán cũ bên dưới mâu thuẫn với [Lab 5](MapGen_Lab5_AcceptedAlgorithm.md) chỉ còn là lịch sử tham khảo, không dùng để triển khai. Kiểm tra tiếp theo nhằm đối chiếu bản C# và tích hợp gameplay; không yêu cầu thiết kế lại, sweep balance hoặc làm hash trước khi port.

> **Ngày lập kế hoạch:** 2026-10-01  
> **Trạng thái:** Sẵn sàng thi công (Ready for Implementation)  
> **Tài liệu liên quan:** 
> - [Kế hoạch tổng thể MapGen](MapGen_ImplementationPlan.md)
> - [Đặc tả Data Texture RGBA32](MapGen_DataTexture_Specification.md)
> - [Giáo trình Toán & Thuật toán MapGen](Archive/2026-10-04/MapGen_Noise_RNG_Curriculum.md)

---

## 1. Mục Tiêu & Kiến Trúc Tổng Quan

Tài liệu này đóng vai trò là **Bản thiết kế thi công chi tiết (Coding Blueprint)** để lập trình viên và trợ lý AI cùng nhau viết mã nguồn cho 4 hàm nghiệp vụ cốt lõi trong `MapGenerator.cs`, kết hợp chặt chẽ với module toán học `MapNoiseAndRng.cs` và mô hình dữ liệu `MapGenData.cs`.

### Nguyên tắc kiến trúc:
1. **Hoàn toàn độc lập (Pure C#):** Không phụ thuộc vào `UnityEngine.Random` hay `Mathf.PerlinNoise` nhằm đảm bảo tính tất định 100% trong môi trường mạng Multiplayer.
2. **Không sửa đổi Core có sẵn:** Tuyệt đối không can thiệp vào `ResourceType.cs`, `GridIslandSystem.cs` hay các hệ thống ECS đang chạy.
3. **Tuân thủ quy tắc hình học nghiêm ngặt:** Đảm bảo 100% người chơi có căn cứ thông thoáng, đường đi thẳng tới mỏ tài nguyên khởi đầu và chỉ có đúng một vùng đất walkable liên thông duy nhất trên toàn bản đồ.

---

## 2. Tiền Đề Cần Thiết (Prerequisites)

Trước khi viết 4 Function của `MapGenerator.cs`, hai module nền tảng sau bắt buộc phải được tạo trước:

### 2.1 File `MapGenData.cs`
Định nghĩa toàn bộ các Struct dữ liệu và hằng số:
* `MapTileType`:
  * `0`: `Empty_Ground` (Đất trống có thể đi lại, `MovementCost = 1` hoặc `10`).
  * `1`: `Protected_Base_Core` (Sân trong Nhà chính `TownHallClearRadius`, cấm núi và tài nguyên).
  * `2`: `Protected_Base_Outer` (Đai bảo vệ căn cứ `BaseProtectedRadius`, cấm núi Perlin).
  * `3`: `Exit_Corridor` (Hành lang thoát hiểm mở ra tâm bản đồ, cấm núi Perlin).
  * `4`: `Obstacle_Rock` (Núi đá chướng ngại vật, `MovementCost = 255`).
  * `10..49`: Dải TileId động cho các Slot tài nguyên (`Resource_Slot_Start` đến `Resource_Slot_End`).
* `MapCellData`: Struct chứa `TileId`, `MovementCost`, `Elevation`, `Moisture`, `IsProtectedGround`.
* `MapResourceSlotConfig`: Struct cấu hình tài nguyên động (gắn enum `ResourceType`, `Density`, `MaxNodesPerCluster`, `MinAccessibleBoundaryCells`).
* `MapModeConfig`: Cấu hình chế độ chơi (Kích thước map, `TownHallClearRadius` `R_hall`, `BaseProtectedRadius` `R_base`, `CorridorWidth`, tần số `f₀`, `RockThreshold`, số tầng fBm `Octaves`, `ExpansionClustersPerBase` - số cụm mỏ phụ độc lập cho từng Base, `ContestedClustersPerSector` - số cụm mỏ tranh chấp cho mỗi Cung/Sector hướng tâm).
* `MapGenerationResult`: Chứa mảng dữ liệu tế bào, danh sách vị trí Spawn và danh sách Node tài nguyên.

### 2.2 File `MapNoiseAndRng.cs`
Cung cấp các công cụ toán học tất định:
* `DeterministicRng`: PRNG dựa trên **SplitMix32** (Steele et al., 2014) và **Xorshift32** (George Marsaglia, 2003) với các hàm:
  * `NextUInt()`, `NextFloat()`, `NextRangeInt(min, max)`, `NextFloat(min, max)`.
  * `SampleAnnulusPoint(center, rMin, rMax)`: Lấy mẫu vành khuyên chuẩn căn bậc hai `r = √(r_min² + U ⋅ (r_max² - r_min²))` để mật độ điểm phân bố đồng đều theo diện tích.
* `SeededPerlinNoise2D`:
  * Bảng hoán vị 512 phần tử `p[512]` được xáo trộn bằng thuật toán **Fisher-Yates Shuffle** (1938) theo Seed.
  * 8 vector hướng gradient chuẩn `(1,1), (-1,1), (1,-1), (-1,-1), (1,0), (-1,0), (0,1), (0,-1)`.
  * Hàm làm mượt **Quintic Fade** của Ken Perlin (2002): `6t⁵ - 15t⁴ + 10t³`.
  * Hàm nội suy song tuyến tính **Bilinear Interpolation** từ 4 tích vô hướng.
  * Hàm chồng sóng **`SampleFBM`** nhân hệ số chuẩn hóa `1 / A_Σ` với `A_Σ = Σᵢ₌₀..K₋₁ Gⁱ` đảm bảo giá trị luôn nằm trọn trong đoạn `[0, 1]`.

---

## 3. Thiết Kế Chi Tiết 4 Function Cốt Lõi Trong `MapGenerator.cs`

```text
MapGenerator.GenerateMap(seed, modeConfig)
  │
  ├──► Function 1: SetupProtectedBasesAndExitCorridors(...)
  │      └── Bảo vệ Base 2 lớp & Hành lang thoát hiểm tâm bản đồ
  │
  ├──► Function 2: GenerateMountainsAndFilterIslands(...)
  │      └── Sinh núi đá fBm & BFS 8 hướng lọc 1 đảo liên thông chính
  │
  ├──► Function 3: SpawnStartingBaseResources(...)
  │      └── Rải mỏ khởi đầu vành khuyên Base (Worker đi thẳng 100%)
  │
  └──► Function 4: SpawnNeutralResourcesAndBuildResult(...)
         └── Rải mỏ trung lập theo Quota & Đóng gói dữ liệu kết quả
```

---

### FUNCTION 1: `SetupProtectedBasesAndExitCorridors`
**Khởi tạo Base 2 lớp và đào hành lang thoát hiểm**

#### 1. Chữ ký hàm (Method Signature):
```csharp
private static void SetupProtectedBasesAndExitCorridors(
    MapCellData[,] grid, 
    int width, 
    int height, 
    MapModeConfig config, 
    SpawnCandidateData[] spawns, 
    Vector2Int mapCenter
);
```

#### 2. Đầu vào & Đầu ra:
* **Đầu vào:**
  * Mảng 2D `grid` đã được khởi tạo toàn bộ là `Empty_Ground` (`TileId = 0`, `MovementCost = 10`, `IsProtectedGround = false`).
  * `spawns`: Tọa độ tâm các căn cứ người chơi (Ví dụ 2 Base: Base 0 góc Tây-Nam, Base 1 góc Đông-Bắc).
  * `mapCenter`: Tọa độ tâm bản đồ `(width / 2, height / 2)`.
  * `config`: Chứa `TownHallClearRadius` (`R_hall`), `BaseProtectedRadius` (`R_base`), `ExitCorridorWidth` (`W`).
* **Đầu ra:** Mảng `grid` được cập nhật cờ `IsProtectedGround = true` cho toàn bộ các ô thuộc Base và Corridor.

#### 3. Thuật toán & Toán học áp dụng:
Duyệt qua từng ô `P(x, y)` trên bản đồ:

1. **Kiểm tra Bán kính Base 2 lớp:**
   Với mỗi căn cứ `Base_i`:
   * Tính bình phương khoảng cách Euclid để tối ưu hiệu năng (tránh hàm căn bậc hai):
```text
distSq = (P_x - Base_ix)² + (P_y - Base_iy)²
```
   * Nếu `distSq ≤ R_hall²`:
     * Gán `TileId = MapTileType.Protected_Base_Core` (Sân trong Nhà chính).
     * Gán `IsProtectedGround = true`, `MovementCost = 10`.
   * Ngược lại, nếu `distSq ≤ R_base²`:
     * Gán `TileId = MapTileType.Protected_Base_Outer` (Đai bảo vệ căn cứ).
     * Gán `IsProtectedGround = true`, `MovementCost = 10`.

2. **Kiểm tra Hành lang thoát hiểm (Exit Corridor):**
   Với mỗi căn cứ `Base_i` (điểm **A**) hướng về tâm bản đồ `mapCenter` (điểm **B**):
   * Áp dụng công thức khoảng cách từ điểm **P** đến đoạn thẳng **AB**:
```text
v = B - A
w = P - A

t = clamp( (w ⋅ v) / (v ⋅ v), 0, 1 )
H = A + t ⋅ v
distance(P, AB) = ‖P - H‖
```
   * Nếu `distance(P, AB) ≤ W / 2`:
     * Nếu ô này chưa thuộc `Protected_Base_Core`: Gán `TileId = MapTileType.Exit_Corridor`.
     * Gán `IsProtectedGround = true`, `MovementCost = 10`.

#### 4. Tiêu chí nghiệm thu (Test Assertions):
* Mọi ô nằm trong bán kính `R_hall` quanh Base bắt buộc có `TileId == Protected_Base_Core`.
* Mọi ô nằm trong `[R_hall .. R_base]` quanh Base bắt buộc có `IsProtectedGround == true`.
* Toàn bộ hành lang từ Base ra tâm bản đồ có bề rộng tối thiểu `W` ô và có `IsProtectedGround == true`.

---

### FUNCTION 2: `GenerateMountainsAndFilterIslands`
**Sinh núi đá Perlin fBm và BFS 8 hướng lọc duy nhất một đảo Walkable chính**

#### 1. Chữ ký hàm (Method Signature):
```csharp
private static void GenerateMountainsAndFilterIslands(
    MapCellData[,] grid, 
    int width, 
    int height, 
    MapModeConfig config, 
    SeededPerlinNoise2D noise, 
    SpawnCandidateData[] spawns, 
    Vector2Int mapCenter
);
```

#### 2. Đầu vào & Đầu ra:
* **Đầu vào:**
  * Mảng 2D `grid` sau khi đã chạy Function 1.
  * `noise`: Thể hiện của `SeededPerlinNoise2D` đã nạp Seed trận đấu.
  * `config`: Chứa `BaseFrequency` (`f₀`), `RockThreshold`, `Octaves`, `Persistence`, `Lacunarity`.
* **Đầu ra:** Địa hình núi đá được khắc tạc hoàn chỉnh. Toàn bộ ô walkable đều thuộc về 1 đảo duy nhất. Mọi hốc núi cụt bị biến thành núi đá.

#### 3. Thuật toán & Toán học áp dụng:

1. **Khắc tạc độ cao và núi đá fBm:**
   Duyệt qua từng ô `(x, y)` trên bản đồ:
   * Tính độ cao chuẩn hóa `[0, 1]` qua hàm chồng sóng đa tầng:
```text
elevation = noise.SampleFBM(x, y, config.BaseFrequency, config.Octaves, config.Persistence, config.Lacunarity)
grid[x, y].Elevation = elevation
```
   * Nếu ô **KHÔNG** thuộc diện bảo vệ (`IsProtectedGround == false`):
     * Nếu `elevation > config.RockThreshold`:
       * Gán `TileId = MapTileType.Obstacle_Rock`.
       * Gán `MovementCost = 255` (Tường cản tuyệt đối cho FlowField).

2. **Lọc 1 Đảo Walkable Duy Nhất bằng BFS 8 Hướng (Flood Fill):**
   * Khởi tạo mảng đánh dấu `bool[,] visited = new bool[width, height]`.
   * Khởi tạo hàng đợi `Queue<Vector2Int> queue = new Queue<Vector2Int>()`.
   * Bắt đầu loang từ `mapCenter` (nếu tâm bị đá thì chọn ô Base 0):
     * `visited[startX, startY] = true`.
     * `queue.Enqueue(new Vector2Int(startX, startY))`.
   * Trong vòng lặp BFS:
     * Lấy ô `current` ra khỏi hàng đợi.
     * Duyệt 8 hướng láng giềng (Bắc, Nam, Đông, Tây, Đông-Bắc, Tây-Bắc, Đông-Nam, Tây-Nam).
     * **Kiểm tra chống lách góc chéo (Corner-Cutting Prevention):** Khi đi chéo sang `(x+1, y+1)`, bắt buộc cả 2 ô bên cạnh `(x+1, y)` và `(x, y+1)` không được đồng thời là vật cản (`Cost >= 255`).
     * Nếu láng giềng hợp lệ, chưa `visited`, và `grid[nx, ny].MovementCost < 255`:
       * Đánh dấu `visited[nx, ny] = true`.
       * Cho vào hàng đợi.

3. **Hóa đá các vùng đất cô lập (Isolate Pockets Removal):**
   * Quét lại toàn bộ bản đồ `(x, y)`:
   * Nếu `grid[x, y].MovementCost < 255` mà `visited[x, y] == false`:
     * Ô đất này nằm trong một hốc núi kẹt, không thể đi ra giữa map!
     * Gán `TileId = MapTileType.Obstacle_Rock`.
     * Gán `MovementCost = 255`.

4. **Kiểm tra Invariant sống còn:**
   * Kiểm tra tất cả các vị trí Base trong `spawns`: bắt buộc `visited[base.x, base.y] == true`. Nếu bất kỳ Base nào không liên thông với tâm bản đồ, ném ngoại lệ `MapGenerationException` yêu cầu sinh lại seed khác!

#### 4. Tiêu chí nghiệm thu (Test Assertions):
* Toàn bộ các ô có `IsProtectedGround == true` tuyệt đối không bị biến thành `Obstacle_Rock`.
* Sau khi lọc đảo, toàn bộ bản đồ chỉ tồn tại **đúng 1 vùng liên thông Walkable duy nhất** (100% các ô có `Cost < 255` đều đi tới được nhau).
* Tất cả người chơi kết nối được với nhau mà không bị núi đá chia cắt thành hai nửa thế giới.

---

### FUNCTION 3: `SpawnStartingBaseResources`
**Rải tài nguyên khởi đầu nằm gọn trong Bán Kính Base 2 Lớp**

#### 1. Chữ ký hàm (Method Signature):
```csharp
private static List<ResourceNodeData> SpawnStartingBaseResources(
    MapCellData[,] grid, 
    int width, 
    int height, 
    MapModeConfig config, 
    SpawnCandidateData[] spawns, 
    Vector2Int mapCenter, 
    ref DeterministicRng rng
);
```

#### 2. Đầu vào & Đầu ra:
* **Đầu vào:**
  * Mảng 2D `grid` sau khi đã chạy xong Function 2.
  * Danh sách các Base `spawns`.
  * Cấu hình các Slot tài nguyên `MapResourceSlotConfig[]`.
  * Bộ sinh số tất định `DeterministicRng`.
* **Đầu ra:** Danh sách `ResourceNodeData` các mỏ khởi đầu đã sinh; cập nhật `grid` với `TileId` của Slot tài nguyên và `MovementCost = 255`.

#### 3. Thuật toán & Toán học áp dụng:
Với mỗi Base `Base_i` và với mỗi Slot tài nguyên khởi đầu (Ví dụ: Slot 0 = Crystal/Gold, Slot 1 = Gas/Oil):

1. **Xác định Vành Khuyên An Toàn:**
   * Bán kính trong: `r_min = config.TownHallClearRadius` (`4` ô — nằm ngoài sân trong).
   * Bán kính ngoài: `r_max = config.BaseProtectedRadius - 2` (`8` ô — nằm lọt thỏm trong vùng an toàn, cách mép 2 ô).

2. **Lấy Mẫu Điểm và Kiểm Tra Hướng Né Hành Lang:**
   * Tính vector đơn vị hướng từ Base ra tâm map:
```text
d_corridor = normalize(mapCenter - Base_i)
```
   * Vòng lặp thử tối đa 100 lần:
     * Dùng thuật toán lấy mẫu vành khuyên chuẩn căn bậc hai:
```text
r = √(r_min² + U ⋅ (r_max² - r_min²))
θ = 2π ⋅ V
P_candidate = Base_i + (r ⋅ cos θ,  r ⋅ sin θ)
```
     * Tính vector đơn vị từ Base chỉ tới điểm mỏ:
```text
ô = (cos θ,  sin θ)
```
     * **Kiểm tra tích vô hướng né hành lang thoát hiểm:**
```text
ô ⋅ d_corridor ≤ 0.25
```
       * Nếu tích vô hướng `> 0.25`: Hướng này đâm thẳng vào cửa ngõ hành lang thoát hiểm ➔ Loại bỏ, thử góc khác!
       * Nếu `≤ 0.25`: Mỏ nằm nép về bên sườn hoặc phía sau lưng Base ➔ Chấp nhận làm tâm mỏ (`Cluster Center`)!

3. **Rải Cụm Mỏ (Resource Cluster Spreading):**
   * Bắt đầu từ `P_candidate`, loang ra 4 hướng láng giềng để đặt đủ số lượng node (`NodesPerCluster`).
   * Điều kiện ô đặt mỏ:
     * Phải nằm trong vành khuyên `[r_min .. r_max]`.
     * `TileId != MapTileType.Protected_Base_Core` (không lấn sân nhà chính).
     * `TileId < 10` (chưa bị mỏ khác chiếm chỗ).
   * Khi đặt node thành công:
     * Gán `grid[x, y].TileId = slot.TileId` (`10..49`).
     * Gán `grid[x, y].MovementCost = 255` (Mỏ tài nguyên đóng vai trò vật cản tự nhiên).
     * Ghi nhận vào danh sách trả về.

#### 4. Tiêu chí nghiệm thu (Test Assertions):
* Mọi cụm mỏ khởi đầu bắt buộc có khoảng cách Euclid tới Base nằm nghiêm ngặt trong `[R_hall .. R_base - 2]`.
* Không có bất kỳ node tài nguyên nào nằm trong sân trong `TownHallClearRadius`.
* Mọi Base của các người chơi nhận đúng 100% cùng số lượng cụm và số node mỏ giống hệt nhau (Cân bằng tuyệt đối).
* Do toàn bộ vùng `R_base` đã được dọn sạch núi từ Function 2, Worker di chuyển từ TownHall tới mỏ là **đường thẳng 100%, không bao giờ bị núi chắn ngang**.

---

### FUNCTION 4: `SpawnNeutralResourcesAndBuildResult`
**Rải tài nguyên trung lập theo Quota và đóng gói kết quả bản đồ**

#### 1. Chữ ký hàm (Method Signature):
```csharp
private static MapGenerationResult SpawnNeutralResourcesAndBuildResult(
    MapCellData[,] grid, 
    int width, 
    int height, 
    MapModeConfig config, 
    SpawnCandidateData[] spawns, 
    List<ResourceNodeData> startingResources, 
    ref DeterministicRng rng
);
```

#### 2. Đầu vào & Đầu ra:
* **Đầu vào:**
  * Mảng 2D `grid` sau khi hoàn tất Function 3.
  * Danh sách mỏ khởi đầu `startingResources`.
  * Cấu hình Quota tài nguyên trung lập `NeutralClusterQuota` của từng Slot.
  * Bộ sinh số tất định `DeterministicRng`.
* **Đầu ra:** Đối tượng `MapGenerationResult` hoàn chỉnh chứa mảng tế bào phẳng 1D/2D, danh sách spawn và toàn bộ node tài nguyên.

#### 3. Thuật toán & Toán học áp dụng:
Áp dụng **Cơ Chế Cân Bằng Khu Vực 2 Tầng (Fair 2-Tier Regional Balancing)** để loại bỏ hoàn toàn tình trạng một người chơi bị thiên vị quá nhiều mỏ:

1. **TẦNG 1: Mỏ Phụ Tự Nhiên (Natural Expansions — Độc Lập Cho Từng Base):**
   * Số lượng mỏ phụ cho mỗi Base: `config.ExpansionClustersPerBase` (Mặc định `1` hoặc `2` cụm/người chơi).
   * **Không tính vào Quota tranh chấp!** Đây là phần tài nguyên kinh tế độc lập được chia đều cho từng Base.
   * Với mỗi Base `Base_i` của người chơi:
     * Sinh đúng `ExpansionClustersPerBase` cụm mỏ phụ trong vành khuyên mở rộng an toàn:
```text
r_exp ∈ [config.BaseProtectedRadius + 2.0 .. config.BaseProtectedRadius + 5.5]
```
     * Kiểm tra góc né hành lang thoát hiểm (`ô ⋅ d_corridor ≤ 0.35`).
     * Kiểm tra `MinAccessibleBoundaryCells` (ít nhất 4–5 ô láng giềng là đất trống Walkable).
   * *Ý nghĩa chiến thuật:* 100% người chơi đều có cùng số lượng mỏ phụ ở cự ly mở rộng an toàn như nhau.

2. **TẦNG 2: Mỏ Tranh Chấp Cung Lợi Thế (Radial Sector Contested — Ưu Tiên Gần Tâm):**
   * Số lượng mỏ tranh chấp cho mỗi cung: `config.ContestedClustersPerSector` (Mặc định `1` hoặc `2` cụm/cung, tổng cộng `N × ContestedClustersPerSector` cụm trên toàn map).
   * **Chia map thành N Cung Lợi Thế (Advantage Sectors):**
     * Với `N` người chơi, góc mở mỗi cung là: `Δθ = 2π / N`.
     * Người chơi thứ `s` có trục chính nối từ Tâm ra Base ở góc:
```text
θ_s = atan2(Base_sy - cy, Base_sx - cx)
```
     * Cung lợi thế của người chơi `s` trải rộng trong đoạn:
```text
θ ∈ [ θ_s - 0.75 ⋅ (π / N)  ..  θ_s + 0.75 ⋅ (π / N) ]
```
   * **Quy tắc Ưu Tiên Gần Trung Tâm:**
     Bán kính tính từ tâm bản đồ được khống chế nghiêm ngặt trong dải:
```text
r_mỏ ∈ [ R_tâm_trống (7.0)  ..  0.28 ⋅ min(MapWidth, MapHeight) ]
```
     Càng gần tâm, các cung càng khép lại gần nhau, biến khu vực này thành **Chiến trường trung tâm rực lửa (Central Hotspot)** mà mỗi người chơi đều có đúng cùng số lượng mỏ trong cánh quạt của mình, không ai bị thiên vị hay bỏ đói!
   * Khoảng cách đệm an toàn giữa các mỏ `spacing ≥ 6.5` ô.
   * Kiểm tra biên tiếp cận `MinAccessibleBoundaryCells ≥ 5` ô đất trống xung quanh để công nhân khai thác thuận tiện.

3. **Đóng Gói Kết Quả `MapGenerationResult`:**
   * Chuyển đổi dữ liệu `grid` sang mảng 1D hoặc giữ 2D tối ưu.
   * Tổng hợp danh sách `SpawnCandidateData[]`.
   * Tổng hợp danh sách toàn bộ `ResourceNodeData[]`.
   * Gắn cờ trạng thái `IsSuccess = true`.

#### 4. Tiêu chí nghiệm thu (Test Assertions):
* Mỗi Base nhận đúng chính xác `ExpansionClustersPerBase` mỏ phụ Natural Expansion ở cự ly công bằng như nhau.
* Mỗi Cung Lợi Thế của người chơi nhận đúng `ContestedClustersPerSector` mỏ tranh chấp ưu tiên gần tâm, đảm bảo cân bằng tuyệt đối `100%` giữa tất cả người chơi.
* Mọi cụm mỏ đều có ít nhất `MinAccessibleBoundaryCells` ô đất trống tiếp giáp để khai thác.
* Trả về kết quả hoàn chỉnh sẵn sàng nạp vào Texture Encoder hoặc Spawner.

---

## 4. Kế Hoạch Thi Công Từng Bước (Implementation Workflow)

Để đảm bảo quy tắc dự án và chất lượng code cao nhất, hai chúng ta sẽ làm việc theo chu trình Pair-Programming chuẩn:

```text
[BƯỚC 1] Viết MapNoiseAndRng.cs (PRNG SplitMix32/Xorshift32 + SeededPerlinNoise2D)
    ↓
[BƯỚC 2] Viết MapGenData.cs (Toàn bộ Struct dữ liệu, Enum và Config)
    ↓
[BƯỚC 3] Viết MapGenerator.cs - Function 1: SetupProtectedBasesAndExitCorridors
    ↓
[BƯỚC 4] Viết MapGenerator.cs - Function 2: GenerateMountainsAndFilterIslands
    ↓
[BƯỚC 5] Viết MapGenerator.cs - Function 3: SpawnStartingBaseResources
    ↓
[BƯỚC 6] Viết MapGenerator.cs - Function 4: SpawnNeutralResourcesAndBuildResult
    ↓
[BƯỚC 7] Lắp ghép tổng thể hàm MapGenerator.GenerateMap(...) và nghiệm thu
```

Mỗi bước đều tuân thủ nguyên tắc: **Nêu rõ file tạo/sửa ➔ Viết mã nguồn sạch sẽ, không comment rườm rà ➔ Xác thực logic cùng User trước khi sang bước tiếp theo.**



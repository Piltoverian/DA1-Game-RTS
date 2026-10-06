# Đặc Tả Kỹ Thuật: Sinh Bản Đồ RTS, Mã Hóa Data-Packed Texture & Quy Chuẩn Player Spawn

> **Cập nhật 2026-10-06:** MapGen Unity đã chạy ổn theo xác nhận của chủ project. Lab/archive đã xóa. Tra cứu thuật toán hiện hành tại [MapGen_Unity_Rules](MapGen_Unity_Rules.md) và [mục lục](MapGen_INDEX.md); các đề xuất cũ bên dưới không thay thế code hiện tại.

> **Mục tiêu**: Xây dựng thuật toán sinh bản đồ theo seed và mode, cân bằng người chơi bằng gói tài nguyên khởi đầu tương đương quanh mỗi base thay vì bắt buộc địa hình đối xứng, mã hóa dữ liệu địa hình, tài nguyên và vị trí xuất phát vào Texture RGBA, hỗ trợ chọn Spawn trực tiếp trên Minimap và thiết lập ván đấu tại Frame đầu tiên thông qua Pipeline Request Spawn chuẩn.

> [!IMPORTANT]
> Đây là đặc tả hành vi đích. Kế hoạch triển khai đã đối chiếu với source, thứ tự thực hiện và tiêu chí nghiệm thu nằm tại [MapGen_ImplementationPlan.md](MapGen_ImplementationPlan.md). Tại thời điểm cập nhật 2026-09-27, MapGen chưa được triển khai hoàn chỉnh.

---

## 🧭 1. Triết Lý Thiết Kế & Thứ Tự Sinh Map Chuẩn RTS (Core Architecture)

### A. Triết Lý Tài Nguyên Tự Nhiên & Quyền Phong Tỏa Của Người Chơi
* **Quy luật tự nhiên**: Mỗi **cluster tài nguyên** sinh ra ngoài tự nhiên phải có biên tiếp cận được từ vùng walkable chính; không được bị đồi núi hoặc các cluster tài nguyên khác bao kín. Các ô tài nguyên **cùng loại** được phép nằm sát nhau để tạo thành một cluster và không bị xem là chặn lẫn nhau.
* **Quyền phong tỏa thuộc về người chơi**: Bản đồ thiên nhiên luôn rộng mở; mỏ tài nguyên chỉ bị cản trở khi chính người chơi (hoặc kẻ thù) chủ động xây công trình, dựng tháp canh hoặc xây tường để "bo mỏ", bảo vệ cứ điểm (`BlockageData` với `CustomCost = 255`).
* **Vùng tiếp xúc khai thác (Mining Clearance)**: Kiểm tra khoảng trống và khả năng tiếp cận trên **biên của toàn cluster**, không bắt buộc từng node bên trong cluster có ô trống riêng. Số ô tiếp cận tối thiểu sẽ là cấu hình theo loại tài nguyên.
* **Giới hạn kích thước cluster**: Mỗi loại tài nguyên có cấu hình `MaxNodesPerCluster` riêng. Giá trị cụ thể sẽ do designer chốt sau; generator không hardcode một giới hạn chung cho vàng, dầu hoặc các loại tài nguyên tương lai.
* **Công bằng theo gói khởi đầu, không theo hình học**: Bản đồ và vị trí base không cần đối xứng. Mỗi base phải nhận cùng số lượng, loại và tổng trữ lượng tài nguyên khởi đầu theo cấu hình mode.
* **Tài nguyên trung lập theo quota của mode**: Phần tài nguyên còn lại được sinh tự do trên vùng walkable chính, nhưng phải nằm ngoài bán kính tối thiểu tới mọi base. Quota, khoảng cách tới base và khoảng cách giữa các cụm đều do designer cấu hình cho từng mode.

---

### B. Thứ Tự Sinh 4 Bước Bất Biến (The 4-Step Generation Hierarchy)

Để đảm bảo: **"Quanh Spawn luôn mở, núi đá và tài nguyên không bao giờ chặn lối ra đảo chính"**, thuật toán sinh map tuân thủ nghiêm ngặt thứ tự sau:

```
[BƯỚC 1: BASE 2 LỚP & HÀNH LANG] ➔ Khóa bán kính bảo vệ Base (R_base), sân trong TownHall (R_hall) & mở đại lộ ra đảo chính
                 │
                 ▼
[BƯỚC 2: NÚI ĐÁ & SINGLE ISLAND] ➔ Sinh núi bằng Perlin fBm, cấm vi phạm Bước 1 (kể cả đai mỏ Base), lọc thành 1 Đảo duy nhất
                 │
                 ▼
[BƯỚC 3: TÀI NGUYÊN TỰ NHIÊN]    ➔ Đặt mỏ khởi đầu NẰM GỌN TRONG R_base (nép sườn/lưng Base), rải mỏ trung lập ở thung lũng mở
                 │
                 ▼
[BƯỚC 4: ĐÓNG GÓI TEXTURE RGBA]  ➔ Xuất Texture2D RGBA32 nén PNG phục vụ Minimap & ECS
```

#### 🥇 Bước 1: Base 2 Lớp & Hành Lang Lối Mở Ra Đảo Chính (ĐẦU TIÊN)
1. Đặt các vị trí Base hợp lệ theo preset hoặc rule của mode. Không bắt buộc đối xứng; mỗi vị trí phải đủ chỗ cho TownHall, worker và gói tài nguyên khởi đầu.
2. Tạo **Bán Kính Bảo Vệ Base 2 Lớp (2-Zone Base Protection):**
   * **Lớp 1 — Sân Trong Nhà Chính (`TownHallClearRadius`, ký hiệu `R_hall`, mặc định `4 - 5` ô):** Vùng đất trống hoàn toàn ở tâm Base để đặt TownHall và `InitWorker` nông dân đứng, cấm cả núi đá lẫn tài nguyên.
   * **Lớp 2 — Vòng Bảo Vệ Toàn Khu Base (`BaseProtectedRadius`, ký hiệu `R_base`, mặc định `10 - 12` ô):** Toàn bộ hình tròn bán kính `R_base` quanh Base được đánh dấu `Protected_Open_Ground = true` (đất phẳng `WalkCost = 1`, cấm tuyệt đối mọi chướng ngại vật núi đá ở Bước 2).
   * *Ý nghĩa sống còn:* Đai từ `R_hall` đến `R_base` thuộc quyền sở hữu nội khu của Base, đảm bảo khi đặt tài nguyên khởi đầu vào đai này ở Bước 3 sẽ **không bao giờ có núi đá mọc xen giữa TownHall và mỏ khởi đầu**.
3. **Mở Hành Lang Lối Ra (Exit Corridor):**
   * Đánh dấu một dải đất rộng `6 - 8` ô hướng thẳng từ Base về phía trung tâm bản đồ (hoặc hướng ra đảo chính) là `Protected_Open_Ground = true`.
   * *Ý nghĩa:* Đảm bảo tuyệt đối không có bất kỳ thứ gì có thể bít kín con đường hành quân của người chơi.

#### 🥈 Bước 2: Sinh Núi Đá & Đảm Bảo 1 Đảo Duy Nhất (Single Island Guarantee)
1. Sinh các rặng núi đá (`cost = 255`) bằng Perlin fBm noise từ seed (sử dụng bảng hoán vị trộn theo seed). Địa hình không bắt buộc đối xứng.
2. **Quy tắc cấm vi phạm:** Mọi ô thuộc `Protected_Open_Ground` (toàn bộ hình tròn `R_base` của các Base, vùng tâm bản đồ và các Hành Lang Lối Mở ở Bước 1) tuyệt đối không được phép có núi.
3. **Khử đảo cụt (Island Pruning via BFS C# thuần):**
   * Chạy Flood-Fill 1 lần duy nhất từ trung tâm để xác định **Đảo Chính (Main Island)** (có kiểm tra chống lách góc chéo).
   * Toàn bộ các hốc kẹt, thung lũng nhỏ bị núi vây kín cô lập sẽ được tự động lấp đầy thành núi đá (`cost = 255`).
   * *Kết quả:* Toàn bộ đất đi được (`cost = 1`) trên bản đồ nối liền thành **ĐÚNG 1 ĐẢO DUY NHẤT**. Mọi vị trí đất trống đều tự động liên thông 100%!

#### 🥉 Bước 3: Sinh Tài Nguyên Thông Minh Theo Slot Tổng Quát (THỨ BA)
1. **Cấu trúc Slot Tài nguyên động (`MapResourceSlotConfig`):**
   * Không hardcode cố định tên tài nguyên vào thuật toán sinh map. Mỗi loại tài nguyên được cấu hình qua một Slot ánh xạ `TileId` (`10..49` trên Kênh G) với enum `ResourceType` tương ứng trong gameplay, kèm `Density`, `MaxNodesPerCluster` và `MinAccessibleBoundaryCells`.
2. **Tài nguyên khởi đầu của Base (Starter Resources — Bắt buộc nằm trong `R_base`):**
   * Mỗi base nhận đúng cùng một gói theo `MapModeConfig`: cùng số cụm, số node mỗi cụm, loại tài nguyên (`Slot 0`, `Slot 1`...) và tổng trữ lượng tương đương.
   * **Điều kiện bắt buộc (Chống cắt mỏ & chống đi xa):** Toàn bộ cụm tài nguyên khởi đầu và biên khai thác 1 ô của nó phải nằm **HOÀN TOÀN BÊN TRONG bán kính bảo vệ Base (`R_base`)**, cụ thể trong vành khuyên nội khu:
     ```
     R_hall ≤ r_starter ≤ R_base - 2
     ```
   * Ưu tiên đặt ở **phía sau hoặc hai bên sườn** của Base (ngược hướng với Hành Lang Lối Mở ở Bước 1, kiểm tra bằng tích vô hướng `ô ⋅ d̂_corridor ≤ 0.25`).
   * Các node cùng loại có thể liền nhau thành cluster. Mỗi cluster phải còn biên walkable cho worker tiếp cận từ TownHall theo đường thẳng thông thoáng và **không chắn ngang lối đi ra đảo chính**.
3. **Tài nguyên trung lập trên bản đồ (Neutral Resources):**
   * Thả vào các ô đất mở thuộc Đảo Chính.
   * Tổng số node/cụm và trữ lượng được lấy từ quota của mode cho từng Slot tài nguyên.
   * Mọi node/cụm trung lập phải nằm ngoài `NeutralResourceMinDistanceFromBase` (`> R_base`) đối với tất cả base; giá trị này do designer đặt cho từng mode.
   * Các ô cùng loại được phép ghép thành cluster liên thông, nhưng số node không vượt `MaxNodesPerCluster` của loại đó.
   * Biên cluster phải có đủ ô walkable (`MinAccessibleBoundaryCells`) có thể tiếp cận từ Đảo Chính; đồi núi hoặc cluster tài nguyên khác không được bao kín cluster.
   * Tuân thủ khoảng cách tối thiểu giữa các cụm nếu mode có cấu hình để tránh dồn tài nguyên vào một điểm.
   * Không bao giờ đặt tài nguyên vào các khe hẹp (Chokepoints) để tránh biến tài nguyên thành vật cản đường.
4. **Không được sinh thiếu âm thầm:** Nếu không thể đặt đủ 100% gói tài nguyên khởi đầu ở mọi Base hoặc quota trung lập trong giới hạn số lần thử, generator trả về lỗi cấu hình/generation rõ ràng.

#### 🏁 Bước 4: Đóng Gói Ra Texture RGBA32 (CUỐI CÙNG)
* Nén mảng dữ liệu thành ảnh PNG siêu nhẹ (~15 KB).

---

## 🎨 2. Quy Chuẩn Mã Hóa 4 Kênh Màu (Data-Packed Texture RGBA32)

Kích thước chuẩn: `128 × 128` hoặc `256 × 256` (khớp với `GridAuthoring.MapType`). Mỗi pixel đại diện chính xác cho 1 ô lưới ECS:

| Kênh Màu | Trường Dữ Liệu | Dải Giá Trị (`0 - 255`) | Quy Ước Chi Tiết & Hành Vi ECS |
| :--- | :--- | :--- | :--- |
| **Kênh R** | **Độ cao địa hình (Height)** | `0 - 255` | • `0 - 60`: Vực sâu / Vùng trũng<br>• `100 - 150`: Mặt đất phẳng chuẩn thi đấu<br>• `180 - 255`: Đỉnh đồi / Cao nguyên (lợi thế tầm nhìn) |
| **Kênh G** | **Loại Ô / ID Thực Thể (Tile/Entity ID)** | `0 - 255` | • `0`: Đất trống bình thường (Walkable Ground)<br>• `1`: Vùng nước / Đầm lầy không thể đi qua<br>• `2`: Dãy núi đá chướng ngại vật (Rock Obstacle)<br>• `10 .. 49`: **Dải ID Slot Tài Nguyên động** (ánh xạ qua `MapResourceSlotConfig` sang enum `ResourceType`; mặc định `10` = Slot 0, `11` = Slot 1)<br>• `50 .. 53`: Vị trí Spawn Candidate 0 .. 3 |
| **Kênh B** | **Chi phí di chuyển (Walk Cost)** | `1 - 255` | • **`1`**: Đi lại bình thường (Đất cứng tiêu chuẩn)<br>• **`2 - 254`**: Vùng đi chậm (Bùn lầy, dốc cao)<br>• **`255`**: **BỊ CHẶN HOÀN TOÀN (Unwalkable / Vật cản)**<br>*(Khớp 100% với `cost >= 255` trong `IntegrationFieldSystem`, `UnitMovementMath`, `MovementAgentTargetSystem`)* |
| **Kênh A** | **Trữ lượng tài nguyên (Resource Density)** | `0 - 255` | • `0`: Không có tài nguyên<br>• `1 - 255`: Hệ số nhân trữ lượng (Nhân `× 100`)<br>Ví dụ: `A = 15` ➔ `1,500` tài nguyên nạp vào `ResourceNodeData.Amount` |

---

## 🎯 3. Quy Trình Chọn Vị Trí Xuất Phát (Spawn Picking Flow)

```
[BƯỚC 1: HOST TẠO MAP] ➔ Sinh Map theo thứ tự 4 bước, đảm bảo 1 Đảo duy nhất & lối mở an toàn
           │
           ▼
[BƯỚC 2: MÃ HÓA TEXTURE] ➔ Ghi nhận Kênh G = 50..53 & Kênh B (Cost 255 cho vật cản)
           │
           ▼
[BƯỚC 3: BAN/PICK & SPAWN PICK UI] ➔ Hiển thị Minimap Texture trên RawImage, 2 bên chọn vị trí xuất phát
           │
           ▼
[BƯỚC 4: SETUP FRAME (FRAME ĐẦU)] ➔ Gửi Request Spawn qua Grid Pipeline chuẩn (Miễn phí)
```

1. **Hiển thị Minimap:** Texture RGBA được gán thẳng vào `RawImage` của Minimap UI.
2. **Tương tác:** Các vị trí Spawn hợp lệ (A, B, C, D) hiển thị marker trực quan trên Minimap.
3. **Lựa chọn:** Người chơi click vào marker để chọn vị trí xuất phát:
   * Khi Người chơi 0 chọn Cặp Tây Bắc, Người chơi 1 chọn Cặp Đông Nam đối diện để đảm bảo cự ly hành quân cân bằng.
   * Lựa chọn được đóng gói vào `PlayerSlotData.SpawnIndex`.

---

## ⚡ 4. Quy Trình Khởi Tạo Trận Đấu Tại Frame Đầu Tiên (Setup Frame)

Tại **Setup Frame (Frame 0)**, hệ thống kích hoạt trực tiếp **Pipeline Request Chuẩn của Game** (`PlaceBuildingRequest` / `JobRequest`) với cờ **InitialSetup (Miễn phí)**:

### 1. Nạp Lưới & Cập Nhật Đảo:
* Đọc Kênh B của Texture ghi vào `DynamicBuffer<GridNodeCost>`. Mọi ô có `B == 255` được gán `cost = 255`.
* `GridIslandSystem` tự động tính toán lại các `islandID` cho toàn bộ bản đồ.

### 2. Sinh Mỏ Tài Nguyên Theo Slot Cấu Hình:
* Quét các ô có `G ∈ [10 .. 49]` và tra cứu trong danh mục `MapResourcePrefabEntry` tương ứng với `TileId = G`:
* Khởi tạo Entity từ Prefab của Slot đó, gán `ResourceNodeData` với `Type = slot.ResourceType` và `Amount = Pixel.A * 100`.
* Đặt `BlockageData` (`CustomCost = 255`) để `BlockageGridBakeSystem` tự động đăng ký chiếm dụng ô lưới, ngăn công trình xây đè lên mỏ.

### 3. Sinh Nhà Chính (TownHall) Qua Request Chuẩn:
* Gửi `PlaceBuildingRequest` tại tọa độ Spawn đã chọn:
  * `BuildingID`: TownHall.
  * `Position`: Tọa độ tâm điểm Spawn.
  * `PlayerID`: `SlotId` của người chơi (0 hoặc 1).
  * **Cờ Miễn Phí (`IsFreeSetup = true`)**: `BuildingPlacementSystem` bỏ qua bước kiểm tra trừ tài nguyên trong `PlayerContext`.
  * Tự động đặt diện tích chiếm dụng lưới và gán `BlockageNeedBakeTag` chuẩn mực.

### 4. Sinh Nông Dân Khởi Đầu (`InitWorker`):
* Mỗi người chơi khởi đầu với đúng số lượng nông dân quy định bởi cấu hình **`InitWorker`** (mặc định = 4):
* Gửi lệnh sinh `InitWorker` worker xếp hình vòng cung phía trước TownHall, mang `EntityOwner { PlayerID = slotId }`.

### 5. Căn Chỉnh Camera & Mở Khóa Mô Phỏng:
* Camera của mỗi người chơi tự động lia về đúng tâm TownHall của mình.
* Kết thúc Setup Frame: Chuyển trạng thái trận đấu sang `Playing`.

---

## 📦 5. Danh Sách Các File Cốt Lõi & Giáo Trình Học Tập

* 🧪 **Phòng thí nghiệm tương tác (HTML Labs):**

| STT | Tên File | Vị Trí | Nhiệm Vụ |
| :---: | :--- | :--- | :--- |
| 1 | `MapNoiseAndRng.cs` | `Game(New Simulation Logic)/MapGen/` | Bộ toán học độc lập: `DeterministicRng` (SplitMix32 + Xorshift32) và `SeededPerlinNoise2D` (Perlin 2D + fBm) |
| 2 | `MapGenData.cs` | `Game(New Simulation Logic)/MapGen/` | Hằng số `MapTileType`, `MapResourceSlotConfig` (gài enum `ResourceType` động), `MapModeConfig` (Bán kính Base 2 lớp `R_hall` & `R_base`), ECS components |
| 3 | `MapGenerator.cs` | `Game(New Simulation Logic)/MapGen/` | Thuật toán 4 bước: Base 2 lớp & Exit Corridor ➔ Perlin fBm Núi & Lọc 1 Đảo ➔ Tài nguyên khởi đầu trong `R_base` & trung lập ➔ Mảng Cell |
| 4 | `MapTextureEncoder.cs` | `Game(New Simulation Logic)/MapGen/` | Đóng gói/Giải nén `Texture2D` RGBA32, mảng byte PNG và `PreviewTexture` |
| 5 | `MapSpawnerConfigAuthoring.cs` | `Game(New Simulation Logic)/MapGen/` | Bake các Prefab (`TownHall`, `Worker`, danh sách `ResourceSlot -> Prefab`) trong SubScene sang Entity |
| 6 | `MapRuntimeSpawner.cs` | `Game(New Simulation Logic)/MapGen/` | Gửi request spawn chuẩn vào Grid tại Setup Frame (miễn phí), sinh `TownHall` và `InitWorker` worker |
| 7 | `MapGenTestAndSpawnUI.cs` | `ClientSide (Presentation)/UI/` | UI test trực quan trong Editor: Minimap Texture preview, nút chọn Spawn và bấm bắt đầu trận |



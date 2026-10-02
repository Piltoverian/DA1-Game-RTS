# Giáo Trình Toàn Diện: Toán Học RNG, Perlin Noise & Thuật Toán Sinh Bản Đồ RTS

> **Mục đích tài liệu:** Tài liệu học tập và tra cứu kỹ thuật dành riêng cho dự án RTS, được thiết kế theo trình tự 6 bậc từ cơ bản đến nâng cao (không yêu cầu nền tảng toán cao cấp). Tài liệu này đi kèm với 3 phòng thí nghiệm tương tác (`Labs/`) để người học trực tiếp kéo thanh trượt kiểm chứng từng phép tính trước và trong khi viết code C#.

---

## 🧪 Danh Mục Phòng Thí Nghiệm Tương Tác (HTML Labs Đi Kèm)

Khi sang session mới, bạn có thể mở trực tiếp các file HTML dưới đây hoặc yêu cầu AI nhúng (`<agent-embed>`) từng Lab vào khung chat để tiếp tục học và tinh chỉnh giáo trình:

1. **[Lab 1 — Bitwise, PRNG & Vành Khuyên Nội Khu Base](Labs/Lab1_Bitwise_PRNG_Annulus.html)**
   * Thực hành phép dịch bit (`≪, ≫`), phép XOR (`⊕`), hàm băm `SplitMix32`, bộ sinh `Xorshift32`, và bài toán lấy mẫu đều theo diện tích hình vành khuyên (`r = √(...)`) bên trong bán kính bảo vệ Base (`R_base`).
2. **[Lab 2 — Lerp, Quintic Fade, Bilinear Interpolation & La Bàn Tích Vô Hướng](Labs/Lab2_Lerp_Fade_Bilinear_DotProduct.html)**
   * Thực hành nội suy 1 chiều (`Lerp`), đường cong chữ S bậc 5 (`Quintic Fade`), kỹ thuật bắc cầu 3 lần Lerp (`Bilinear Interpolation`) trong ô vuông 4 góc, và dùng Tích Vô Hướng (`â ⋅ b̂`) để lọc hướng đặt mỏ.
3. **[Lab 3 — Hai Loại Vector Trong Perlin Noise, Chồng Sóng fBm & 4 Bước RTS MapGen](Labs/Lab3_PerlinVectors_fBm_RTSMapGen.html)**
   * Thực hành kéo điểm `(u, v)` bên trong 1 ô Perlin để quan sát Vector hướng dốc (`ĝ`) nhân vô hướng với Vector khoảng cách (`d`), chồng nhiều tầng sóng `fBm`, thuật toán loang nước `BFS Flood-Fill` lọc 1 đảo duy nhất, và kiểm tra 4 kênh màu `RGBA32`.

---

## 🗺️ Sơ Đồ Tư Duy 6 Bậc (Từ Con Số Đến Bản Đồ RTS)

```
[BẬC 1: CON SỐ NGẪU NHIÊN (PRNG)]
 Phép dịch bit (≪, ≫) & XOR (⊕) ➔ SplitMix32 + Xorshift32 ➔ Xáo trộn bảng perm[512]
                                │
                                ▼
[BẬC 2: NỐI ĐIỂM THÀNH MẶT PHẲNG MƯỢT]
 Nội suy 1 chiều (Lerp) ➔ Đường cong chữ S (Quintic Fade) ➔ Bắc cầu 2 chiều (Bilinear Interpolation)
                                │
                                ▼
[BẬC 3: CHIẾC LA BÀN HƯỚNG (VECTOR & DOT PRODUCT)]
 Tích vô hướng (a ⋅ b = aₓ⋅bₓ + aᵧ⋅bᵧ) ➔ Đo độ cùng chiều (>0), vuông góc (=0), ngược chiều (<0)
                                │
                                ▼
[BẬC 4: KẾT TINH THÀNH PERLIN NOISE 2D & CHỒNG SÓNG fBm]
 Ghép Bậc 1 + Bậc 2 + Bậc 3: 4 Vector dốc góc (ĝ) ⋅ 4 Vector khoảng cách (d) ➔ Bilinear ➔ Chồng K tầng fBm
                                │
                                ▼
[BẬC 5: THUẬT TOÁN SINH MAP RTS 4 BƯỚC & BÁN KÍNH BASE 2 LỚP]
 Base 2 lớp (R_hall, R_base) & Hành lang ➔ Cắt ngưỡng núi fBm & BFS 1 Đảo ➔ Mỏ trong R_base ➔ Texture RGBA32
                                │
                                ▼
[BẬC 6: LỘ TRÌNH CÙNG CODE 5 PHẦN TRONG DỰ ÁN]
```

---

## 🔢 BẬC 1: Con Số Ngẫu Nhiên Từ Đâu Ra? (Bitwise & PRNG)

### 1.1. Tại sao không dùng `UnityEngine.Random` và `Mathf.PerlinNoise`?
1. **`UnityEngine.Random` dùng trạng thái toàn cục (Global Shared State):** Nếu bất kỳ hệ thống UI, âm thanh hay hiệu ứng hạt nào gọi `Random.Range` xen vào giữa lúc sinh map, chuỗi số ngẫu nhiên sẽ bị lệch, làm hỏng tính tất định (Determinism) giữa Host và Client.
2. **`Mathf.PerlinNoise(x, y)` không có tham số `seed`:** Nếu cộng một độ lệch lớn từ `seed` (ví dụ `offset = seed * 10000f`), số thực `float` 32-bit sẽ mất độ chính xác phần lẻ (floating-point precision loss), làm biến dạng vân noise.

Vì vậy, chúng ta xây dựng bộ **PRNG độc lập theo struct (`DeterministicRng`)** và **Perlin Noise trộn bảng hoán vị trực tiếp từ Seed (`SeededPerlinNoise2D`)**.

### 1.2. Ba phép toán trên Bit cần nắm
Một số nguyên không dấu `uint` 32-bit gồm 32 bóng đèn nhị phân (`0` và `1`). Ba phép toán thao tác trực tiếp trên bit gồm:
* **Dịch trái (`x ≪ k`) và Dịch phải (`x ≫ k`):** Đẩy toàn bộ dãy bit sang trái hoặc phải `k` vị trí, các ô trống mới lấp bằng `0`.
* **Phép XOR (`a ⊕ b`, trong C# viết là `a ^ b`):** So sánh từng cặp bit ở cùng vị trí: **hai bit khác nhau thì ra `1`, giống nhau thì ra `0`**. Phép XOR bảo toàn xác suất 50% ra `0` và 50% ra `1`, không làm dãy bit bị hội tụ về toàn `0` (như phép AND) hay toàn `1` (như phép OR).
* **Phép Mặt nạ Bit (`x & 255`):** Số `255` trong hệ nhị phân là `11111111` (8 bit cuối bằng `1`). Phép `x & 255` giữ lại đúng 8 bit cuối cùng, cho kết quả luôn nằm trong `0..255` nhanh hơn phép chia lấy dư `% 256` và không bao giờ bị lỗi ra số âm khi `x < 0`.

### 1.3. Quy trình 4 bước của `DeterministicRng`
1. **Khởi tạo trạng thái bằng hàm băm `SplitMix32`** (*Nguồn: Sebastiano Vigna & Austin Appleby - MurmurHash3 finalizer*):
   Giúp hai `seed` liền kề (như `1` và `2`) tạo ra hai trạng thái khởi đầu khác nhau hoàn toàn (Hiệu ứng tuyết lở — Avalanche Effect):
```
z ← seed + 0x9E3779B9
z ← (z ⊕ (z ≫ 16)) × 0x85EBCA6B
z ← (z ⊕ (z ≫ 13)) × 0xC2B2AE35
state₀ ← z ⊕ (z ≫ 16)
(Nếu state₀ == 0 thì gán state₀ = 0x6D2B79F5 vì Xorshift32 không được có trạng thái bằng 0)
```
2. **Sinh số nguyên 32-bit tiếp theo bằng `Xorshift32`** (*Nguồn: George Marsaglia, "Xorshift RNGs", Journal of Statistical Software, 2003*):
```
x ← state
x ← x ⊕ (x ≪ 13)
x ← x ⊕ (x ≫ 17)
x ← x ⊕ (x ≪ 5)
state ← x
```
3. **Chuyển sang số thực `float` trong nửa khoảng `[0, 1)`:**
   Kiểu `float` chuẩn IEEE-754 có 24 bit định trị (mantissa). Ta dịch phải 8 bit để giữ đúng 24 bit cao nhất rồi nhân với `2⁻²⁴` (`1.0f / 16777216.0f`):
```
u = (NextUInt() ≫ 8) × (1.0 / 16777216.0)   ∈ [0, 1)
```
4. **Thuật toán xáo bài `Fisher-Yates Shuffle`** (*Nguồn: Ronald Fisher & Frank Yates, 1938; Donald Knuth, TAOCP Vol. 2*):
   Khởi tạo mảng `p[0..255]` với `p[i] = i`. Chạy `i` từ `255` giảm về `1`, rút chỉ số ngẫu nhiên `j ∈ [0, i]` và hoán đổi `p[i] ↔ p[j]`. Cuối cùng nhân đôi thành mảng `perm[0..511]` (`perm[i] = p[i & 255]`) để tra cứu cho Perlin Noise.

---

## 📐 BẬC 2: Nối Điểm Thành Mặt Phẳng Mượt (Lerp, Fade & Bilinear Interpolation)

### 2.1. Nội suy tuyến tính 1 chiều (`Lerp`)
Cho hai giá trị **a** (tại điểm đầu `t = 0`) và **b** (tại điểm cuối `t = 1`). Giá trị tại vị trí tỷ lệ `t ∈ [0, 1]` trên đoạn thẳng nối từ **a** sang **b** là:
```
lerp(a, b, t) = a + t ⋅ (b - a)
```

### 2.2. Đường cong làm mượt bậc 5 (`Quintic Fade Curve`)
Nếu chỉ dùng `lerp` thẳng, tại ranh giới tiếp giáp giữa hai ô lưới (`t = 0` và `t = 1`), độ dốc thay đổi đột ngột tạo thành vết gãy chữ V.
Ken Perlin (*"Improving Noise", ACM Transactions on Graphics / SIGGRAPH 2002*) đưa `t` qua hàm đa thức bậc 5 có đạo hàm bậc nhất và bậc hai đều bằng `0` tại `t = 0` và `t = 1`:
```
fade(t) = 6t⁵ - 15t⁴ + 10t³ = t³ ⋅ (t ⋅ (t ⋅ 6 - 15) + 10)
```
Hàm này uốn tỷ lệ di chuyển thành hình chữ S mượt mà: rời điểm đầu êm ái, tăng tốc ở giữa và giảm tốc êm ái khi chạm điểm cuối.

### 2.3. Nội suy song tuyến tính 2 chiều (`Bilinear Interpolation`)
Khi có **1 ô vuông** với 4 giá trị ở 4 góc (**V₀₀** dưới-trái, **V₁₀** dưới-phải, **V₀₁** trên-trái, **V₁₁** trên-phải) và cần tính giá trị tại điểm `(u, v)` nằm bên trong ô (`u, v ∈ [0, 1)`):
Ta thực hiện **"Bắc cầu bằng 3 lần Lerp"** với `sₓ = fade(u)` và `sᵧ = fade(v)`:
```
1. Nội suy ngang dọc theo cạnh đáy (nối góc Dưới-Trái và Dưới-Phải):
   nx₀ = lerp(V₀₀, V₁₀, sₓ)

2. Nội suy ngang dọc theo cạnh đỉnh (nối góc Trên-Trái và Trên-Phải):
   nx₁ = lerp(V₀₁, V₁₁, sₓ)

3. Nội suy dọc bắc cầu giữa hai điểm vừa tìm được:
   V(u, v) = lerp(nx₀, nx₁, sᵧ)
```

---

## 🧭 BẬC 3: Chiếc La Bàn 2 Chiều — Vector & Tích Vô Hướng (`Dot Product`)

Trong mặt phẳng 2D, với hai vector **a = (aₓ, aᵧ)** và **b = (bₓ, bᵧ)**, **Tích Vô Hướng (`a ⋅ b`)** được tính bằng:
```
a ⋅ b = aₓ ⋅ bₓ + aᵧ ⋅ bᵧ
```
Khi hai vector đã chuẩn hóa độ dài bằng `1` (**â** và **b̂**), giá trị `â ⋅ b̂ = cos(θ)` cho biết mối quan hệ hướng giữa chúng:
* `â ⋅ b̂ > 0` (tối đa `+1`): Hai mũi tên **cùng hướng** (góc nhọn `< 90°`).
* `â ⋅ b̂ = 0`: Hai mũi tên **vuông góc** (`90°` — nằm ngang bên sườn).
* `â ⋅ b̂ < 0` (tối thiểu `-1`): Hai mũi tên **ngược hướng** (góc tù `> 90°`, quay lưng vào nhau).

**Ứng dụng trực tiếp trong MapGen:**
1. **Kiểm tra hướng đặt mỏ khởi đầu:** Gọi **d̂_corridor** là vector đơn vị chỉ từ Base ra tâm bản đồ, và **ô** là vector đơn vị chỉ từ Base tới vị trí thử đặt mỏ. Điều kiện `ô ⋅ d̂_corridor ≤ 0.25` đảm bảo mỏ chỉ nằm ở hai bên sườn hoặc phía sau lưng Base, tuyệt đối không chắn trước cửa hành lang hành quân.
2. **Khoảng cách từ điểm `P` tới đoạn thẳng hành lang `AB` (Point-to-Segment Distance):**
```
v = B - A,   w = P - A
t = clamp( (w ⋅ v) / (v ⋅ v), 0, 1 )
H = A + t ⋅ v
distance(P, AB) = ‖P - H‖
```

---

## ⛰️ BẬC 4: 2D Gradient Perlin Noise & Chồng Sóng `fBm`

### 4.1. Tại sao dùng Vector ở 4 góc mà không đặt 4 độ cao ngẫu nhiên?
Nếu đặt 4 con số độ cao ngẫu nhiên ở 4 góc ô vuông (**Value Noise**), các đỉnh đồi và đáy vực luôn bị khóa cứng tại các góc lưới (hiện tượng vỉ trứng gà).
Trong **Perlin Gradient Noise** (*Ken Perlin, SIGGRAPH 1985 & 2002*), độ cao tại mọi góc lưới luôn bằng `0`, còn thứ ngẫu nhiên ở mỗi góc là **Vector hướng nghiêng của mặt đất (`ĝ`)**.

### 4.2. Hai loại Vector bên trong 1 ô Perlin Noise
Khi xét điểm thực `(x, y)` rơi vào ô lưới có góc nguyên dưới-trái `x₀ = ⌊x⌋, y₀ = ⌊y⌋` và tọa độ lẻ trong ô `u = x - x₀, v = y - y₀`:

1. **Vector Hướng Dốc tại 4 góc (`Gradient Vector ĝ₀₀, ĝ₁₀, ĝ₀₁, ĝ₁₁`):**
   * Tra bảng băm `perm` với `X = x₀ & 255`, `Y = y₀ & 255`:
```
h₀₀ = perm[X + perm[Y]] & 7
h₁₀ = perm[X + 1 + perm[Y]] & 7
h₀₁ = perm[X + perm[Y + 1]] & 7
h₁₁ = perm[X + 1 + perm[Y + 1]] & 7
```
   * Mỗi chỉ số `0..7` chọn 1 trong 8 vector hướng cố định:
     `(+1,+1), (-1,+1), (+1,-1), (-1,-1), (+1,0), (-1,0), (0,+1), (0,-1)`.
2. **Vector Khoảng Cách từ 4 góc tới điểm `(u, v)` (`Offset Vector d₀₀, d₁₀, d₀₁, d₁₁`):**
```
d₀₀ = ( u,     v     )    (từ góc 0,0 tới u,v)
d₁₀ = ( u - 1, v     )    (từ góc 1,0 tới u,v)
d₀₁ = ( u,     v - 1 )    (từ góc 0,1 tới u,v)
d₁₁ = ( u - 1, v - 1 )    (từ góc 1,1 tới u,v)
```

### 4.3. Ghép 2 Vector bằng Tích Vô Hướng và Bilinear Interpolation
Tại mỗi góc, độ cao ảnh hưởng lên điểm `(u, v)` bằng tích vô hướng giữa hướng dốc **ĝ** và quãng đường **d**:
```
n₀₀ = ĝ₀₀ ⋅ d₀₀
n₁₀ = ĝ₁₀ ⋅ d₁₀
n₀₁ = ĝ₀₁ ⋅ d₀₁
n₁₁ = ĝ₁₁ ⋅ d₁₁
```
Sau đó trộn 4 giá trị `n₀₀, n₁₀, n₀₁, n₁₁` bằng **Quintic Fade + Bilinear Interpolation (Bậc 2)**:
```
sₓ = fade(u),   sᵧ = fade(v)
nx₀ = lerp(n₀₀, n₁₀, sₓ)
nx₁ = lerp(n₀₁, n₁₁, sₓ)
Perlin2D(x, y) = lerp(nx₀, nx₁, sᵧ)
```

### 4.4. Chồng nhiều tầng sóng `fBm` (Fractal Brownian Motion)
Cộng chồng **K** tầng (`Octaves`) với tần số nhân lên theo `Lacunarity` (`L ≈ 2.0`) và biên độ giảm dần theo `Persistence` (`G ≈ 0.5`):
```
fBm(x, y) = (1 / A_Σ) ⋅ Σᵢ₌₀..K₋₁ [ Gⁱ ⋅ Perlin2D(x ⋅ f₀ ⋅ Lⁱ, y ⋅ f₀ ⋅ Lⁱ) ]
với A_Σ = Σᵢ₌₀..K₋₁ Gⁱ
```

---

## 🏰 BẬC 5: Thuật Toán Sinh Map RTS 4 Bước & Quy Tắc Bán Kính Base 2 Lớp

### 5.1. Quy tắc Bán Kính Base 2 Lớp (`R_hall` & `R_base`) — Chống Cắt Mỏ & Chống Đi Xa
Nếu chỉ bảo vệ vùng đặt nhà chính mà đặt mỏ tài nguyên khởi đầu ở ngoài vùng bảo vệ, Perlin Noise ở Bước 2 có thể sinh núi đá đè mất chỗ đặt mỏ hoặc tạo vách núi chắn giữa TownHall và mỏ, khiến Worker phải đi vòng rất xa.
Do đó, mỗi Base được thiết kế **2 vòng tròn đồng tâm**:
```
[Tâm Base (0)] ──► [R_hall (4..5 ô)] ──► [Đai Tài Nguyên Khởi Đầu] ──► [R_base (10..12 ô)]
   TownHall &         Sân trống cho         Nằm GỌN BÊN TRONG            Biên bảo vệ Base
  Spawn Marker       Worker đứng quanh     vùng cấm núi R_base!        (Cấm Perlin mọc núi)
```
* **Toàn bộ hình tròn `r ≤ R_base` (`BaseProtectedRadius`):** Được khóa `Protected_Open_Ground = true` ngay từ Bước 1 (cấm núi đá 100%).
* **Vùng tâm `r < R_hall` (`TownHallClearRadius`):** Dành riêng cho TownHall và `InitWorker` nông dân.
* **Vành khuyên nội khu `R_hall ≤ r ≤ R_base - 2`:** Nơi đặt 100% các cụm tài nguyên khởi đầu (`Slot 0`, `Slot 1`...). Nhờ nằm trọn trong `R_base`, đường đi từ TownHall tới mọi mỏ khởi đầu luôn là đường thẳng thông thoáng và bằng nhau ở mọi Seed.

### 5.2. Công thức lấy mẫu đều theo diện tích Vành Khuyên (Annulus Sampling)
Để các điểm thử đặt mỏ phân bố đều trong vành khuyên từ **Rₘᵢₙ** (`R_hall`) đến **Rₘₐₓ** (`R_base - 2`) mà không bị dồn cục về phía tâm, bán kính `r` phải tính theo căn bậc hai:
```
θ = 2π ⋅ u₁
r = √( Rₘᵢₙ² + u₂ ⋅ (Rₘₐₓ² - Rₘᵢₙ²) )
Δx = r ⋅ cos(θ),   Δy = r ⋅ sin(θ)
```
*(với **u₁**, **u₂** là hai số ngẫu nhiên độc lập trong `[0, 1)` rút từ `DeterministicRng`).*

### 5.3. Loang nước `BFS Flood-Fill` đảm bảo 1 Đảo Duy Nhất
Sau khi cắt ngưỡng `fBm ≥ RockThreshold` thành núi đá (`WalkCost = 255`), chạy thuật toán **BFS Flood-Fill 8 hướng** bắt đầu từ Tâm bản đồ `C` (với điều kiện đi chéo: 2 ô cạnh vuông góc kẹp bên phải có `WalkCost < 255`, khớp 100% với `GridIslandSystem.cs`). Mọi ô đất trống không chạm được dòng chảy từ tâm sẽ được chuyển thành núi đá (`WalkCost = 255`).

---

## 🛠️ BẬC 6: Lộ Trình Triển Khai Code 5 Phần (Cho Session Tiếp Theo)

1. **Phần 1 — Toán học & Hợp đồng dữ liệu (`MapNoiseAndRng.cs` & `MapGenData.cs`):**
   * Viết `DeterministicRng`, `SeededPerlinNoise2D` và `MapResourceSlotConfig` (cấu hình Slot tài nguyên tổng quát gài enum `ResourceType` động) + `MapModeConfig` (có `TownHallClearRadius` & `BaseProtectedRadius`).
2. **Phần 2 — Bộ sinh bản đồ 4 bước (`MapGenerator.cs`):**
   * Triển khai Bước 1 (Base 2 lớp + Exit Corridor), Bước 2 (Perlin fBm + BFS 1 đảo), Bước 3 (Rải tài nguyên khởi đầu trong `R_base` + tài nguyên trung lập ngoài `NeutralResourceMinDistanceFromBase`).
3. **Phần 3 — Đóng gói Texture & UI Test (`MapTextureEncoder.cs` & `MapGenTestAndSpawnUI.cs`):**
   * Đóng gói/Giải mã Texture RGBA32 + PNG và dựng UI preview trực quan.
4. **Phần 4 — Nhánh `IsFreeSetup` tối giản (`BuildingPlacementComponents.cs` & `BuildingPlacementSystem.cs`):**
   * Thêm `bool IsFreeSetup` để đặt TownHall khởi đầu miễn phí, hoàn tất ngay với đầy máu mà không ảnh hưởng logic xây nhà thông thường.
5. **Phần 5 — Nạp Grid & Spawn Runtime (`MapSpawnerConfigAuthoring.cs` & `MapRuntimeSpawner.cs`):**
   * Nạp Kênh B vào `GridNodeCost` sau `GridInitSystem`, spawn mỏ theo bảng ánh xạ `TileId -> ResourceType`, gửi request đặt TownHall, sinh `InitWorker` nông dân và căn chỉnh Camera.

# Kế hoạch MapGen: Height, Walkability, Plateau và Ramp

Ngày: 2026-10-02. Đây là đề xuất thay thế phần terrain generation trong kế hoạch cũ; chưa phải implementation. Giữ yêu cầu tài nguyên theo slot, setup ECS và ưu tiên Hạng mục B. Không suy diễn rằng các game tham khảo dùng cùng thuật toán nội bộ.

> Đề xuất competitive cụ thể mới nhất: [MapGen_CompetitiveTerrain_Design.md](MapGen_CompetitiveTerrain_Design.md). Theo yêu cầu người dùng, thiết kế dùng terrain/noise không đối xứng, hỗ trợ N player và budget lợi thế từng spawn; có công thức height/noise/ramp, navigation và gates cân bằng. Trữ lượng tài nguyên thuộc prefab; bỏ density encoding trong đề xuất cũ.

## 1. Nguồn nghiên cứu và kết luận

- [StarCraft II Editor — Terrain Layer](https://s2editor-guides.readthedocs.io/New_Tutorials/02_Terrain_Editor/020_Terrain_Layer/): height brush vẫn cho unit đi; cliff brush tạo các tầng nối bằng ramp. Đây là tài liệu sử dụng editor, không phải source engine; số tầng trong hướng dẫn cũ không dùng làm giới hạn thiết kế.
- [Blizzard — 4.13.0 PTR](https://news.blizzard.com/en-us/article/23471116/starcraft-ii-4-13-0-ptr-patch-notes): phân biệt Ground/Shallow Water/Deep Water theo loại di chuyển, đồng thời hỗ trợ nhiều cliff layer. Bài học: height và khả năng đi là hai dữ liệu khác nhau.
- [Age of Empires — Random Map Scripting Commands](https://support.ageofempires.com/hc/en-us/articles/8478836252564-Random-Map-Scripting-Commands): API vùng cliff có height, painting, edge và ramp riêng; ràng buộc có thể tránh vùng không đi được hoặc giữ kết nối. API rm* này không được coi là RMS AoE II.
- [Microsoft — Random map generation in a strategy video game, US20070206023A1](https://patents.google.com/patent/US20070206023A1/en): mô tả outline cliff, khoảng hở tạo ramp, mesh cliff và điều chỉnh elevation. Đây là mô tả thiết kế trong bằng sáng chế, không xác nhận implementation của một phiên bản Age of Empires cụ thể.
- [Ziegler & von Mammen, 2020 — Generating Real-Time Strategy Heightmaps using Cellular Automata](https://downloads.hci.informatik.uni-wuerzburg.de/Ziegler2020aa.pdf): tách layout, erosion, markers, detail và export; kết hợp nhiều lớp địa hình rồi làm mềm độ cao. Thử nghiệm 30 người chơi Supreme Commander đánh giá map mới và cân bằng hơn nhưng kém đẹp hơn. Map nghiên cứu có đối xứng; không suy ra rằng chia đều mỏ trên map bất đối xứng tự động cân bằng.
- [IEEE — Angle-Based Multi-Objective Evolutionary Algorithm ... for Game Map Generation](https://ieeexplore.ieee.org/document/9410278/): dùng các mục tiêu fairness, playability, strategy và interestingness. Kế hoạch chỉ học cách đánh giá nhiều tiêu chí, chưa triển khai thuật toán tiến hóa.

Đề xuất cho dự án: sinh cấu trúc vùng và đường nối trước; tạo height từ cấu trúc đó; dùng noise cho hình dáng và chi tiết; biên cliff chặn di chuyển, ramp nối các tầng. Không dùng một ngưỡng height để biến toàn bộ vùng cao thành đá.

## 2. Hợp đồng dữ liệu

- `MapCellData[]`: một cell trên một ô Grid; index theo `GridHelper`.
- `Height`: độ cao world unit có min/max rõ ràng, không phải trực tiếp giá trị noise 0..1.
- `HeightLevel`: tầng địa hình chiến thuật; bản đầu dùng 2 tầng, có thể mở rộng.
- `TerrainKind`: Ground, Water, Rock, CliffBand, Ramp; tách khỏi ID resource/spawn.
- `ProtectionFlags`: BaseCore, BaseOuter, RequiredConnection, RampReserved; không dùng TileId để mã hóa bảo vệ.
- `BaseWalkCost`: địa hình tĩnh; `Buildable`: quyền xây độc lập với khả năng đi.
- Region, ramp và cluster lưu metadata riêng: ID, footprint, endpoints và lý do thất bại.

Ví dụ: mặt cao nguyên Height=6 vẫn cost=1; chân đất Height=0 cost=1; dải cliff giữa chúng cost=255; ramp chuyển từ 0 lên 6 và vẫn walkable. Các con số này chỉ minh họa.

Cell cost chỉ biểu diễn chặn ô, không biểu diễn chặn riêng một cạnh. Bản đầu dùng một dải cliff chiếm ô và mở dải ramp xuyên qua: tương thích flow field hiện tại. Biên cliff không được có khe chéo. Nếu sau này cần cả hai ô sát biên đều walkable nhưng không được bước qua biên, phải bổ sung dữ liệu cạnh và sửa tất cả navigation consumer; không giả vờ cost hiện tại xử lý được.

## 3. Pipeline sinh địa hình

### A. Validate config và tạo layout graph

Đặt vùng base, natural expansion, vùng tranh chấp và đường nối. Base giữ core và starter ring phẳng cùng cao độ. Mỗi cạnh graph có bề rộng tối thiểu, endpoints, yêu cầu ramp nếu khác tầng. Giữ kết nối ra vùng chính nhưng đường có thể cong, không bắt mọi base đi thẳng vào cùng một tâm. Số lối ra là cấu hình mode.

Validate footprint, khoảng cách biên, vùng bảo vệ, ramp length và khả năng chứa quota ngay từ đầu. Tách seed stream cho layout, height/detail và resources; sửa noise không làm xáo lại toàn bộ tài nguyên. Lưu seed, attempt và generator version.

### B. Sinh vùng plateau và hình dáng

Tạo mask vùng trên grid thô, mở rộng về grid gameplay. Noise tần số thấp làm biến dạng biên trong giới hạn. Có thể thử CA để làm sạch vùng quá nhỏ sau này; không bắt buộc thay RNG/Perlin hiện có.

Một vùng cao đủ diện tích có mặt walkable. Núi đá kín là terrain kind riêng, có quota riêng. Tránh các mảng cao lốm đốm một ô. Những mask cấm vật cản và đường nối đã reserve luôn được giữ.

### C. Dựng height, cliff và ramp

Gán cao độ nền theo region; phát hiện biên khác tầng để tạo cliff band. Với mỗi kết nối bắt buộc, mở ramp đủ rộng và đủ dài. Nội suy độ cao dọc ramp, nối êm hai đầu. BaseCore và starter ring không bị noise nâng/hạ; vùng ngoài hòa chuyển về cao độ nền.

Noise chi tiết có biên độ nhỏ; clamp theo ngân sách độ dốc và không làm đổi tầng hay đóng ramp. Dùng cùng mặt bề mặt cho render, collision và height sampler. Mesh ở biên cliff phải tách đỉnh để không nội suy thành một con dốc nhìn như đi được.

### D. Compile navigation và buildability

Ground/plateau/ramp hợp lệ nhận cost 1 ở bản đầu. Water, rock và cliff band nhận 255. Không tăng cost chỉ vì đứng cao. Nếu cần slow slope, phải kiểm tra movement có thực sự giảm tốc: flow-field cost tự nó chỉ đổi lựa chọn đường.

Kiểm tra slope ở từng đoạn chuyển và cạnh tam giác của mặt bề mặt, tính theo world unit/cell size; không chỉ so height tuyệt đối. Đường ramp ưu tiên hướng theo trục ở prototype để tránh nhiều lỗi raster hóa cùng lúc.

Buildability xét terrain kind và độ chênh cao trên toàn footprint; ramp đi được nhưng mặc định không xây. BFS cùng quy tắc runtime: đi chéo chỉ khi cả hai ô cạnh đều walkable.

### E. Validate, repair rồi đặt resource

Tính khoảng cách tới vật cản để đo clearance theo bán kính unit và bề rộng đường. Kiểm tra graph trên grid đã raster hóa, không chỉ trên graph thiết kế. Repair có giới hạn: nới ramp, mở đường cần thiết, dời obstacle; chỉ bỏ các pocket nhỏ không có chức năng. Không hóa đá hàng loạt một cao nguyên đáng lẽ phải có ramp.

Đặt starter trên ring cùng cao độ base, tránh đường/ramp; kiểm tra từng node và biên cluster, không chỉ tâm. Expansion/contested xét đường đi thực và ô biên khai thác tiếp cận được. Dùng shortest-path cost tới biên cluster để đánh giá công bằng; cùng quota và khoảng cách Euclid là chưa đủ.

Đặt thử cluster theo transaction; reject nếu chặn route, làm thiếu clearance hoặc cô lập vùng cần thiết. Sau toàn bộ resource, kiểm tra lại connectivity, clearance và quota. Failure trả stage, reason và attempt; không âm thầm thiếu mỏ hoặc đổi seed.

## 4. Runtime và texture

Source hiện có `GridHelper.GridToWorld` trả Y=0; actuator chưa lấy height từ địa hình. Giữ navigation/ORCA và kiểm tra khoảng cách planar trên XZ. Bản đầu nâng model render bằng height sampler qua child visual, giữ simulation root trên mặt phẳng để tránh thay đổi khoảng cách 3D, combat và avoidance. Ramp có hiệu lực navigation qua cost mask; hiệu ứng cao độ đối với combat/FoW là bước riêng.

TownHall/resource render lấy cao độ từ footprint; mặt base phẳng. Camera picking dùng surface collider thực rồi đổi XZ sang Grid. Không dùng raycast physics cho mọi unit mỗi tick; sampler truy cập dữ liệu terrain, dùng phép nội suy khớp tam giác mesh và không trộn qua cliff.

Static terrain cost và dynamic blockage phải có cách compose: bỏ mỏ/công trình không được khôi phục cliff/water thành đất. Audit `CostChangeSystem` và blockage bake trước khi nối runtime.

Terrain texture đề xuất giữ R=height preview quantized, G=terrain kind, B=base walk cost, A=height level. Placements lưu riêng prefab ID, cell và rotation; amount thuộc prefab, không encode density. Protection, buildability và region/ramp metadata nằm trong map payload kèm version; PNG đơn lẻ không đủ khôi phục mọi dữ liệu này.

R decode dùng HeightMin/HeightMax; sai số lượng tử phải nhỏ hơn tolerance gameplay. Chốt navigation từ cùng dữ liệu height canonical trước export; round-trip không được làm đổi khả năng đi. Preview có các chế độ height, terrain kind, walkability, buildability, clearance và region/ramp, tách khỏi data texture.

## 5. Các mốc triển khai

| Mốc | File dự kiến | Kết quả kiểm chứng |
|---|---|---|
| 1 | `MapGenData.cs`, mở rộng `MapGenRNG.cs`, tách noise khỏi `MapGenerator.cs` | Schema nhất quán, RNG có trạng thái, seed streams và validation |
| 2 | `MapLayoutGenerator.cs`, `MapHeightBuilder.cs`, `MapNavigationCompiler.cs` | Map cố định 2 tầng, cliff band, 1 ramp, base phẳng |
| 3 | `MapValidation.cs`, `MapPreview` | BFS, clearance, footprint và overlay đúng ở map mẫu |
| 4 | `MapGenerator.cs` orchestration | Layout theo seed, plateau/noise detail, repair có giới hạn |
| 5 | `MapResourcePlacer.cs` | Quota đúng, tiếp cận được, kiểm tra fairness theo đường đi |
| 6 | `MapTextureEncoder.cs`, terrain mesh/sampler/presentation bridge | Round-trip, render và picking khớp; unit đi lên ramp đúng hình ảnh |
| 7 | Authoring, runtime spawner, placement | Nạp sau GridInit, compose blockage, setup miễn phí và acknowledgement một lần |

Tên file có thể gộp khi nhỏ; không để generator trở thành MonoBehaviour phụ thuộc scene.

## 6. Nghiệm thu

Trước tiên dùng fixture nhỏ: cao nguyên phẳng đi được; cliff không ramp không qua được; thêm ramp thì đi được; hai ô chéo kẹp cliff không lách; plateau có base/resource không bị prune; resource không đóng ramp; TownHall trên footprint phẳng; decode không đổi navigation.

Sau đó chạy batch seed cho 128/256, 2/4 spawn khi config hỗ trợ. Ghi tỷ lệ fail theo stage, thời gian sinh, walkable ratio, vùng nhỏ, ramp width, shortest-path tới mỏ/tranh chấp và diện tích buildable quanh base. Ngưỡng cân bằng là tham số cần hiệu chỉnh bằng Play Mode; batch seed không chứng minh map vui hoặc cân bằng mọi chiến thuật.

**Phạm vi coding tiếp theo:** mốc 1–3 với map mẫu hai tầng cố định. Chứng minh height và walkability hoạt động đúng trước khi mở rộng sang random layout và rải tài nguyên. Đây là thay đổi so với kế hoạch cũ bắt đầu bằng bảo vệ vòng base rồi threshold noise thành núi.

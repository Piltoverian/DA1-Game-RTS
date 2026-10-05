# Thiết kế terrain competitive: height, noise và walkability

> **Cập nhật 2026-10-03:** Thuật toán Lab 5 đã được người dùng chốt. Bước tiếp theo là port nguyên thuật toán sang Unity theo [kế hoạch port](Archive/2026-10-04/MapGen_NextSteps_2026-10-03.md). Các mô tả thuật toán cũ bên dưới mâu thuẫn với [Lab 5](MapGen_Lab5_AcceptedAlgorithm.md) chỉ còn là lịch sử tham khảo, không dùng để triển khai. Kiểm tra tiếp theo nhằm đối chiếu bản C# và tích hợp gameplay; không yêu cầu thiết kế lại, sweep balance hoặc làm hash trước khi port.

Ngày 2026-10-02. Đề xuất kỹ thuật có thể triển khai; chưa có generator hoặc map hoàn chỉnh được nghiệm thu. Thiết kế này thay thế phần terrain của kế hoạch trước. Mọi hằng số dưới đây là preset khởi đầu của dự án, không phải hằng số StarCraft hay kết quả cân bằng đã chứng minh qua chơi.

## 1. Quyết định thiết kế

**Cập nhật theo yêu cầu người dùng:** không dùng đối xứng hình học, gameplay hoặc noise. Bản đầu kiểm chứng ở 2 player nhưng thuật toán nhận N player. Giả định mode free-for-all; team mode cần một cấu hình đánh giá khoảng cách giữa team riêng.

Cao nguyên có mặt đi được, cliff chặn, ramp nối tầng. Noise thay đổi hình dạng trong ngân sách; không quyết định toàn bộ topology qua một ngưỡng height. Tài nguyên chỉ lưu prefab ID, vị trí và hướng; amount thuộc prefab.

Cân bằng bằng **ngân sách lợi thế theo player**: cùng starting package và các khoảng mục tiêu cho access time, đất xây, khả năng ra quân và áp lực từ hàng xóm. Base/plateau/mỏ/đường/ramp được đặt độc lập, không copy hoặc rotate giữa player. Sai lệch nhỏ được chấp nhận, sai lệch lớn bị repair hoặc reject. Không dùng một weighted score cho phép nhiều đất xây bù cho thiếu starter resources.

Base ban đầu cùng HeightLevel và cùng chính sách số cửa ra để giảm yếu tố gây nhiễu; hình dạng, vị trí cửa, đường tới mỏ và các vùng ngoài base có thể khác nhau. Đây là ràng buộc công năng, không đối xứng địa hình. Màu/normal detail có thể biến thiên; không phủ texture khiến ramp nhìn như blocked.

## 2. Những gì có nguồn xác nhận

[Blizzard protocol](https://github.com/Blizzard/s2client-proto/blob/master/s2clientprotocol/raw.proto) định nghĩa `pathing_grid`, `terrain_height` và `placement_grid` riêng. Dùng mô hình tách lớp đó, không tuyên bố đây là thuật toán sinh map nội bộ của Blizzard.

[Perkins 2010 / BWTA](https://ojs.aaai.org/index.php/AIIDE/article/download/12405/12264/15933) phân tích StarCraft: Brood War bằng obstacle polygons, Voronoi diagram và region/chokepoint graph. Đây là thuật toán nghiên cứu cho terrain analysis; không phải SC2 engine. Dùng nguyên tắc đo bề rộng và phân tích topology sau rasterization. Không bê nguyên thư viện vào Unity.

[Microsoft US20070206023A1](https://patents.google.com/patent/US20070206023A1/en) mô tả noise elevation, texture mix theo trọng số, mesh cliff và khoảng hở ramp. Dùng sự phân biệt geometry/material/objects; công thức dưới đây do dự án đề xuất.

[Togelius et al. 2010](https://www.um.edu.mt/library/oar/bitstream/123456789/81279/1/Towards_multiobjective_procedural_map_generation_2010.pdf) dùng path distance và mục tiêu map design; cũng nêu tình huống optimizer gom base gần nhau để cải thiện điểm nhưng làm thiết kế xấu. Áp dụng hard constraints trước scoring, tránh metric gaming.

[IEEE, DOI 10.1109/TETCI.2021.3067104](https://ieeexplore.ieee.org/document/9410278/) xác nhận ở abstract việc tối ưu fairness/playability/strategy/interestingness cho MegaGlest. Chưa truy cập được toàn văn; không nhận các metric hoặc công thức bên dưới là của bài IEEE.

## 3. Không gian, dữ liệu và preset đầu tiên

`c = grid.cellsize` theo world unit. Kích thước world không đồng nhất với số ô. Source GridAuthoring lấy c từ Renderer bounds; phải kiểm tra X/Z cùng scale.

| Tham số | Giá trị khởi đầu |
|---|---|
| Grid | 128×128 cho fixture 2 player; 256×256 cho khảo sát 4/8 player, chỉ khi area/footprint budget đạt |
| Tầng thấp / cao | 0 / 4c |
| Dốc walk tối đa | 25° |
| Base core / starter ring | core r≤5c; starter anchors r∈[7c,10c], footprint+biên phải nằm trong r≤12c |
| Mặt base plateau | bán kính khoảng 18c, không được giảm protected area |
| Ramp chính | dài 20c, rộng hình học ít nhất 8c và đạt clearance theo unit radius |
| Apron hai đầu ramp | tối thiểu 4c theo chiều đi, không resource |
| Corridor centerline | rộng tối thiểu 10c; flank 8c |
| Noise biên | amplitude tối đa 2c; wavelengths 24c–48c |
| Noise height | amplitude raw tối đa 0.8c; wavelengths 24c,48c,96c; 3 octaves, persistence 0.5 |
| Noise material | độc lập; không tham gia collision/navigation |
| Static walk cost | 1 cho ground/plateau/ramp; 255 cho cliff/rock/water |

8c không phải tự động đủ cho mọi unit. Chiều rộng phải thỏa `W >= max(Wpreset, n*2r + (n-1)gap + 2margin)` với n=3 unit cạnh nhau ở ramp chính. Lấy r lớn nhất từ prefab ground-unit của preset. Nếu rộng không đủ hoặc footprint TownHall không lọt core, fail config hoặc tăng kích thước; không bỏ qua.

Sáu lớp authoritative:
1. Region ID + HeightLevel.
2. Surface vertex heights và split vertices ở cliff.
3. Terrain static occupancy/cost.
4. Ground navigation mask theo profile/radius.
5. Buildability và footprint validation.
6. Prefab placements và runtime blocker occupancy.

RGBA height preview không phải nguồn chính của surface. Giữ canonical vertex height dạng fixed-step `c/256` hoặc float đã quantize, kèm version; texture color không tham gia quyết định walkability.

## 4. Layout không đối xứng và mở rộng N player

### 4.1 Đặt các vùng xuất phát

Lấy mẫu anchor bằng Poisson-disc có khoảng cách tối thiểu, loại các anchor không đủ buffer với biên map. Tạo tối đa 32 bộ anchor theo seed; không đặt player thành đa giác đều, không dùng cung góc bằng nhau.

Mỗi anchor cần chứa base protected disk, plateau, một ramp đủ dài, apron và natural patch. Dùng Voronoi cells bị clip bởi biên map làm miền tìm kiếm sơ bộ; không biến biên Voronoi thành tường. Diện tích hình học Voronoi không phải tiêu chí balance cuối: phải đo buildable area và path distance sau khi có terrain.

Mỗi miền nhận cùng starting package, một natural package theo resource type, và quota diện tích xây tối thiểu. Chọn cửa ramp và natural độc lập trong miền, ưu tiên còn khoảng trống kết nối shared network. Vị trí spawn chưa gán PlayerId; sau admit map, slot assignment là bước riêng.

### 4.2 Tạo shared network

Tạo một natural node Ni cho mỗi base Bi, cùng khoảng N đến 2N shared combat/expansion nodes bằng blue-noise ngoài core/ramp envelopes. Shared nodes không bắt buộc nằm ở tâm map. Dựng Delaunay adjacency để có tập candidate edges ngắn và phần lớn không cắt nhau; nếu tập node suy biến thì reject/jitter theo lịch seed cố định.

Tạo MST nối shared network, rồi thêm edges Delaunay để loại bridge và có ít nhất hai đường edge-disjoint giữa các Ni. Nếu không đủ, thêm shared node hoặc reject candidate. Đặt degree cap mục tiêu 4 tại combat nodes, nhưng connectivity và routing clearance có ưu tiên cao hơn cap. Edge-disjoint chỉ là precheck: các corridor vẫn có thể hợp vào cùng khe khi raster hóa, nên phải kiểm tra lại bottleneck ở grid cuối.

Bi nối Ni bằng đúng một cửa phòng thủ ở preset đầu. Network bên ngoài natural không có single bottleneck bắt buộc mọi player phải đi qua. Không bắt mọi cặp player có cùng distance: điều này vừa không cần thiết vừa khó thực hiện trên mặt phẳng với nhiều player.

Rasterize room masks và corridor từ graph, route từng edge qua vùng chưa reserve bằng A* có penalty cho đi sát base khác/biên map và tạo giao cắt ngoài node. Hai corridor giao nhau phải admit junction và cập nhật graph hoặc reroute; không giữ graph giả không khớp geometry. Phần ngoài vùng đi được được tạo ridge/rock/terrain blocked với biên organic, không biến toàn map thành mê cung hành lang một ô.

### 4.3 Scale bằng diện tích và khoảng cách

Giữ c và footprint world của prefab nhất quán. Trước generate: `UsableArea >= N*A_private_min + A_shared_min(N)`; A_private_min đo từ base+ramp+natural envelopes, A_shared_min từ combat node/corridor budgets và độ rộng quân. Không thể tăng N vô hạn trên 128×128; fail config và yêu cầu map lớn hơn nếu budget thiếu.

Natural có thể xa hơn range cũ R_base+2..5.5 để dành chiều dài ramp. Thay range đó bằng footprint + travel-time mục tiêu; không ép hai điều kiện mâu thuẫn. Không dời tất cả base/ramp cùng một phép xoay sau repair.

## 5. Ba loại noise và thứ tự xử lý

### 5.1 Noise liên tục không đối xứng

`Ns(p) = SeededFBM(p)` trong [-1,1], lấy mẫu world-space liên tục trên toàn map. Không average với ảnh xoay, không mirror/copy noise giữa player. Không renormalize min/max riêng mỗi seed vì sẽ thay amplitude khó kiểm soát. Bất đối xứng height được phép trong slope và fairness budgets.

Dùng một surface mesh với vertex height canonical và triangulation nhất quán; các biên cliff vẫn split vertices. Nếu dùng noise local theo region, blend trên vùng chuyển tiếp cùng tầng trước khi compile navigation; không tạo seam ở biên region.

Seed streams: hash(seed, generatorVersion, candidateIndex, stageId), stageId riêng cho layout, boundary, height, placement, material. Không dùng Random toàn cục, không phụ thuộc thứ tự HashSet/Dictionary.

### 5.2 Biên vùng

Region có signed distance d(p), âm bên trong. Biên gợi ý:
`d'(p) = d(p) + 2c*Ns_boundary(p)`.

Trước commit, mask phải giữ core/starter area, ramp footprint+aprons, corridor minimum-width envelope. Noise chỉ sửa phần còn lại. Admission theo từng vùng, không theo cặp xoay. Loại island nhỏ chỉ khi không có region role; vùng có vai trò bị tách thì repair ramp hoặc reject candidate. Sau sửa biên, kiểm tra topology, width và lợi thế từng player, không chỉ số lượng ô walkable.

### 5.3 Height nền và ramp

Mặt region: `H0(p)=4c*HeightLevel(p)`.

Ramp có trục unit vector e, start A, length L:
`t=clamp(dot(p-A,e)/L,0,1)`;
`q(t)=6t^5-15t^4+10t^3`;
`H0(p)=hLow+(hHigh-hLow)*q(t)` (đảo t nếu xuống dốc).

Vì `max q'(t)=1.875`, chiều dài tối thiểu:
`L >= 1.875*abs(hHigh-hLow)/tan(thetaMax)`.

Với Δh=4c, L=20c: slope max=0.375, angle≈20.56°. 20c chừa khoảng cho rasterization/slope safety. Ramp height constant theo chiều ngang; không wave hoặc valley ở mép làm khác surface.

Mesh cliff là dải bề mặt/vách riêng bị blocked, không dùng interpolation xuyên từ mặt cao xuống mặt thấp. Split vertices ở discontinuity. Height sampler trả triangle của surface đã chọn và không lấy bilinear qua cliff.

### 5.4 Height detail có chứng nhận độ dốc

`δraw(p)=0.8c*M(p)*Ns_height(p)`.

M=0 trong base protected disk r≤12c, ramp và aprons, cùng các patch dự kiến đặt natural TownHall. M chuyển quintic sang 1 trên dải 8c ở khu vực có thể chứa noise; không smooth xuyên cliff. Natural footprint được flatten TRƯỚC compile navigation. Đạo hàm mask cũng tạo slope nên phải tính trên height cuối, không chỉ giới hạn amplitude noise.

`H(p)=H0(p)+α*δraw(p)`, dùng một α toàn map ở bản đầu để không tạo seam giữa region. Với mỗi tam giác walkable:
`g0=gradient(H0)`, `gn=gradient(δraw)`, `s=tan(thetaBudget)`.

Điều kiện: `||g0+αgn||² <= s²`.
Đặt `a=dot(gn,gn)`, `b=2dot(g0,gn)`, `d=dot(g0,g0)-s²`.
Nếu H0 đã hợp lệ và a>0, upper bound dương:
`αtriangle=(-b+sqrt(b²-4ad))/(2a)`.
Chọn `α=min(1,min αtriangle)`. Nếu H0 sai thì sửa ramp/layout; giảm noise không chữa được ramp vốn quá dốc. Nếu α quá nhỏ (<0.25), reject height candidate hoặc tăng wavelength, không dùng vô hạn lần retry.

Dùng thetaBudget=24° cho noise admission, thetaMax=25° cho nghiệm thu. Sau quantize H ở c/256, tính lại gradient từng triangle; fail thì shrink α theo lịch cố định hoặc reject. Giới hạn retry không dùng random seed mới ngoài candidateIndex.

Đây là chứng nhận hình học ở mặt lưới cho slope, không bảo đảm pathing hoặc army throughput. Cliff triangles không thuộc tập walkable; không giảm toàn bộ cliff height để thỏa slope.

## 6. Walkability phải khớp surface và bán kính

Với mỗi cell, semantic candidate walkable nếu kind là Ground/Plateau/Ramp. Cell không chứa cliff, đá hay nước và toàn bộ các triangle thuộc bề mặt đi qua có slope≤thetaMax.

Tính obstacle occupancy từ geometry/footprint thực, tính cả biên ngoài map. Tạo configuration-space obstacles bằng inflate polygon với `r+margin`, rồi raster hóa bảo thủ thành navigation cells. Mỗi cell admitted khi diện tích cell không cắt obstacle đã inflate. Đây là quy tắc mạnh hơn center clearance; không dùng chỉ khoảng cách tới tâm ô blocked để tuyên bố có clearance.

Có thể lưu distance field để debug width và tối ưu candidate queries, nhưng conservative mask là authoritative. Giữ physical occupancy riêng để không inflate hai lần. Profile đầu dùng bán kính lớn nhất của ground-unit preset; sau này nhiều profile cần cache/island tương ứng.

`CanStep(i,j)`:
- i,j có navigation cost<255;
- nếu chéo, cả hai ô cạnh có navigation cost<255;
- segment giữa tâm ô nằm trên surface hợp lệ, không qua cliff/đứt tầng không ramp;
- surface dọc segment không vượt slope limit.

Với cliff band và rasterization bảo thủ đúng, segment check sẽ nhất quán với cost mask; nếu có ngoại lệ do geometry, sửa rasterizer hoặc thêm edge mask cho TẤT CẢ consumer. Không chỉ thêm edge rule vào validator mà để runtime không biết.

Navigation cost=1 trên toàn terrain đi được ở v1; không tự tăng cost vì height cao. Hệ thống hiện tại không giảm tốc theo terrain cost. Runtime dùng node-cost + DirCost để tích hợp; đánh giá route choice phải dùng cùng convention. Travel time báo riêng bằng độ dài XZ/speed tham chiếu, sau đó đo lại bằng unit thật vì smoothing/ORCA có thể thay thời gian.

## 7. Resource và buildability

Starter chỉ trên cùng mặt phẳng base, cùng prefab ID/count nhưng vị trí và rotation được chọn độc lập cho mỗi player. Kiểm tra footprint + standing cells và access-time budgets chứ không chỉ anchor. Mỏ cùng count nhưng bị xếp thành vành cản base hoặc xa cửa worker vẫn không được admit.

Spawn TownHall cố định; worker origins bên ngoài footprint. Resource access set là ô đứng trong gather range, line-of-sight và collision phù hợp với ability; dùng perimeter fallback chỉ cho design experiment. Không đo khoảng cách tới tâm mỏ blocked.

Mọi resource placement transaction ghi blocker footprint rồi recompute navigation affected area; reject nếu đóng ramp/apron/route hoặc phá access. Prefab amount không đưa vào texture; cân bằng kinh tế bằng cùng prefab catalog và số instance từng type.

Buildability riêng: ground/plateau, không ramp, không cliff, không occupancy. Footprint height spread≤0.1c ở preset đầu; phải validate corners/triangles footprint thật. Kiểm tra ít nhất 12 vị trí đặt được prefab building tham chiếu quanh base trong r≤18c và có đường worker tới; đây là preset thử, phải thay theo building catalog/game economy.

## 8. Cân bằng: hard gates trước soft score

### Hard gates — sai một mục là reject

1. Không áp dụng symmetry gate; mọi player phải đạt từng nhóm fairness budget bên dưới.
2. Protected footprint và prefab catalog đúng, quota exact, không thiếu node âm thầm.
3. Ground profile có đúng một component walkable; mọi base/standing set đều thuộc component đó. Không có walkable pocket bị bỏ quên.
4. Ramp/corridor width thực đạt profile+army requirement; gradient max≤25° sau quantize.
5. Các base cùng HeightLevel và cùng cửa ra theo policy; buildable capacity, access time và threat pressure nằm trong ngưỡng. Không yêu cầu footprint plateau hoặc các đường bằng nhau.
6. Shared network sau natural exits có alternate routes thực. Đóng từng portal bottleneck chính vẫn nối được các Ni; routes thay thế tránh corridor đó và buffer, không chỉ lệch vài ô. Base defensive entrance là ngoại lệ có chủ đích.
7. Terrain+resource blockage vẫn đạt các gate trên; no-resource mask bao ramp/apron/required lane.
8. Không có shortcut xuyên semantic barrier chưa khai báo. Reconstruct region adjacency từ walk map và đối chiếu graph allowance; các merging có chủ đích phải cập nhật graph.

Mọi gate được kiểm tra trên toàn N player và grid sau resource placement. Ưu tiên player tệ nhất; không dùng average để giấu một spawn bị thiệt.

### Chỉ số cụ thể

Với metric dương x_i của player i:
`E(x)=max_i abs(x_i-median(x))/max(median(x),epsilon)`.
Mỗi metric còn cần absolute floor/ceiling theo mode; cùng tệ không được coi là cân bằng. Ngưỡng sau là starting design tolerances cần hiệu chỉnh qua chơi, không phải số đã được IEEE chứng minh.

| Metric theo player | Budget thử |
|---|---|
| Starting prefab package từng type | Exact ID/count; không bù count bằng terrain |
| Starter access time từng resource type | E≤10%; còn giới hạn khoảng cách tuyệt đối quanh core |
| Natural access time từng type | E≤15%; natural phải gần mình hơn enemy đủ margin |
| Buildable capacity quanh base | E≤15%; đạt footprint minimum, số vị trí không chồng nhau |
| Main exit width/throughput proxy | E≤15%, cùng số exit, đạt army clearance floor |
| Thời gian enemy gần nhất tới base | E≤15%; mọi pair ≥T_rush_min |
| Threat pressure nhiều hàng xóm | E≤20% |
| Shared resource opportunity từng type | E≤15% |

Access time: shortest legal path từ worker launch set đến standing/gather set, theo XZ/speed tham chiếu; đồng thời ghi graph cost runtime. Starter metric tính trung bình trọng số theo count từng type, ghi thêm worst-cluster time để average không giấu mỏ khó lấy. Natural ownership margin thử: enemy shortest access time≥1.25×owner time. Nếu không đạt, đổi natural position chứ không tăng prefab amount.

Buildable capacity: số footprint của cùng một building tham chiếu có thể pack không chồng nhau, đứng được và worker tới được trong r≤18c; quét anchors theo thứ tự cố định. Đây là lower-bound heuristic, không phải nghiệm maximum packing. Báo thêm area và footprint clearance.

`t_ij` là travel time từ approach/launch set player j tới base i. `nearest_i=min_{j!=i} t_ij`; không ép mọi t_ij bằng nhau. Với k=min(2,N-1) enemies gần nhất:
`Pressure_i=sum_{j trong k gần nhất} exp(-t_ij/T_threat)`.
T_threat là constant của mode (preset thử25s), không tự normalize từng candidate. Thêm enemy approach angular coverage và high-ground overlooking base vào báo cáo; nếu sau này combat/FoW dùng height, cần thêm hard gates cho các lợi thế đó trước admit.

T_rush_min preset thử=20s cho ground unit nhanh nhất catalog; median nearest target=25s±15%. Kiểm tra world scale/speed cho phép trước generate. Không cộng navigation cost giả để kéo dài reported travel time. Với FFA, mọi player có thể gặp hai hàng xóm với khoảng cách khác nhau nhưng pressure budget phải đạt.

Shared resource không yêu cầu từng mỏ cách đều mọi base. Với cluster k và type q:
`a_ik=exp(-t_ik/T_resource)`;
`w_ik=a_ik/sum_j a_jk`;
`Opportunity_iq=sum_{k thuộc q} InstanceWeight_k*w_ik`.
InstanceWeight=1 cho cùng prefab; khác prefab dùng design catalog weight, không pixel density. T_resource preset cố định20s. Kiểm tra thêm số cluster thật sự reachable trong time budget (không chỉ fractional ownership), quota tổng và các lối tiếp cận độc lập. Metric này là access proxy, không dự đoán ai thực sự thắng tranh chấp.

Alternate path dùng route bị ép tránh bottleneck/corridor buffer, mục tiêu length ratio≤1.8 và đạt clearance floor; không cần đường vòng mỗi phía bằng nhau. Physical walkable area ratio mục tiêu45–70%, từng combat room chứa đủ army test. Floor/ceiling này là mục tiêu thử.

Sau hard gates, tính `F=max_m E_m/budget_m` trên metric fairness: giảm lợi thế tệ nhất trước. Selection lexicographic: F, rush-time target deviation, alternate-route/area target deviation, novelty descriptor rồi candidateIndex. Novelty dựa region shape và curvature, không pixel entropy. Không cho metric tốt bù vượt budget khác.

Ví dụ 4 player có starter times [4.8,5.1,5.0,5.3]s: median5.05,E≈4.95%, đạt10%. Nếu player cuối7.0s thì median5.05,E≈38.6%, reject/repair ngay dù ba player khác rất tốt. Build capacity cần cả E và minimum, không chỉ chênh lệch.

## 9. Generator có giới hạn, tái lập được

```text
for candidateIndex in 0..31:
    streams = DeriveStreams(seed, version, candidateIndex)
    layout = BuildIrregularSpawnDomainsAndSharedGraph(N, streams.layout)
    regions = PerturbBoundaryWithinProtectedEnvelopes(layout)
    surface0 = BuildLevelsAndExplicitRamps(regions)
    surface = AddCertifiedHeightNoiseAndQuantize(surface0)
    physical = RasterizeStaticGeometry(surface, regions)
    navigation = BuildConservativeGroundProfile(physical)
    if not TerrainHardGates(...): record failure; continue
    placements = PlaceIndependentEqualPrefabPackagesAndValidate(...)
    if not FinalHardGates(...): record failure; continue
    candidates.Add(map, metrics)
return SelectFromValidCandidates() or StructuredGenerationFailure
```

Mỗi candidate tối đa3 repair rounds. Xác định player có normalized violation lớn nhất và sửa metric đó: mỏ quá xa→dời cluster cùng type; thiếu đất xây→mở plateau trong vùng được phép; natural quá xa→dời anchor/reroute; enemy pressure quá lớn→đổi spawn hoặc route. Không sửa theo cặp xoay. Repair base-local trước, graph-global sau; spawn move phải regenerate/revalidate mọi dependency, không dịch TownHall đơn lẻ.

Mỗi round tối đa8 proposals theo lịch xác định, giữ proposal làm giảm violation tệ nhất và không phá gate đã đạt hoặc làm player khác vượt budget. Recompute mọi affected metrics của toàn N player sau sửa, không chỉ player được hỗ trợ. Không đạt sau3 rounds thì reject candidate. Không hóa đá toàn pocket để che lỗi hoặc chỉ đổi cost để đi xuyên geometry.

Candidate count=32 là giới hạn preset cho profiling, không cam kết tốc độ. Stop selection cố định theo count cho determinism; time budget chỉ abort toàn generation và trả timeout, không chọn map khác vì máy nhanh/chậm.

Nếu shader/render có noise độc lập, giữ seed material trong payload. MapHash bao height canonical, static/nav masks, placement và profile/catalog hashes. Host gửi map payload khi multiplayer; không tuyên bố float Perlin sẽ bit-identical trên mọi platform.

## 10. Source hiện có: các yêu cầu tích hợp thực tế

- `GridHelper`: index z*width+x; GridToWorld đang Y=0. Giữ simulation root planar ở milestone đầu, child visual dùng surface sampler; combat/FoW height rules là work riêng.
- `IntegrationFieldSystem`: chống cắt góc ở bước tích hợp, ngưỡng blocked 255. Validator phải theo đúng cost và DirCost runtime.
- `UnitMovementMath.GetRawDirection`: đọc bestcost các neighbor, chưa kiểm tra corner legality tại bước chọn direction. Bilinear flow direction có thể trộn qua vách. Cần admissible-neighbor filtering và collision sweep tại actuator trước khi tuyên bố cliff an toàn.
- ORCA tránh unit không thay thế swept-disc kiểm tra static obstacles. Validate proposed XZ segment với inflated geometry; project/stop ở ranh giới hợp lệ, không teleport qua cliff do separation correction.
- `BlockageGridBakeSystem` cleanup hiện gửi cost=1; `CostChangeSystem` ghi trực tiếp. Phải chuyển sang static base + dynamic occupancy reference counts/owner sets, compose final mask và xử lý footprint chồng nhau. Xóa một resource không được mở cliff hoặc xóa blocker khác.
- Static/canonical geometry revision và dynamic blockage revision phải invalidate navigation/cache đúng lượt; chờ GridInit, refresh grid trước spawn. Camera picking dùng collider surface thật.

Như vậy cần sửa navigation integration ở các điểm trên, không chỉ thêm component Height cho unit. Không sửa code gameplay trong lượt nghiên cứu này.

## 11. Kiểm chứng đã làm và những gì chưa làm

`Research/verify_height_constraints.py` là thí nghiệm lịch sử của đề xuất trước: bề mặt synthetic 128×64, hai ramp đối xứng, analytic low-frequency noise; không dùng Perlin production và không có cliff/resource hoặc Unity movement. Giữ lại để kiểm tra công thức slope, không dùng symmetry của fixture này làm yêu cầu cho thiết kế hiện hành.

Kết quả lưu `Research/height_constraint_results.json`:
- Ramp Δ=4c,L=20c: analytic max≈20.556°.
- Noise amplitude 0.8c: α=1, slope sau quantize≈20.360°.
- Noise amplitude 4c (stress): raw slope≈30.067°, α≈0.80548; sau quantize≈24.998°.
- Vertex rotation error=0 cả hai trường hợp.

Thí nghiệm chứng minh công thức có thể giới hạn slope ở fixture này. Công thức gradient không phụ thuộc symmetry, nhưng chưa chạy batch map bất đối xứng nhiều player. Không chứng minh mọi mesh, seed, army movement hoặc map balance. Production dùng budget24° và final25°, không áp dụng sát giới hạn25° như stress experiment.

## 12. Lộ trình triển khai và nghiệm thu

1. Fixture 1v1 cố định: data schema, graph/region masks, plateau + explicit ramp, collision/sampler. Chưa random.
2. Navigation compiler + runtime consumer legality/sweep + static/dynamic composition. Test no ramp bị chặn, có ramp đi được, diagonal corner, delete resource và overlapping blockers.
3. Noise không đối xứng theo ba stage + certified gradient + quantization. Hash, protected zones, edge seams, no shortcuts.
4. Prefab placement độc lập + footprint/gather-access + per-player budget reports.
5. Candidate loop bounded + repair/selection + overlays. Batch1,000 seeds cho từng cấu hình 2/4/8 player; 128 cho2, 256 cho4/8 chỉ khi area budget đạt. Đo worst-player metrics, không chỉ average.
6. Unit trials: 1,20,100 ground units qua từng ramp theo hai hướng, đo arrivals/time/stuck events và illegal crossing=0. Không ước lượng throughput chỉ bằng ramp width; bổ sung throughput fairness budget từ unit trials khi có số liệu.
7. Balance trials: swap start slots trong1v1; rotate bot/civ assignments qua tất cả spawn cho N-player, giữ control config, báo per-spawn outcomes và confidence interval. Lịch thử giới hạn trước, không factorial mọi assignment. Human playtest để đánh giá đường vòng/rush/đất xây có thú vị không. Access metrics chưa phải win probability.

Batch output phải có success/failure rate từng stage, accepted-map metrics, generation latency p50/p95, seed/version/hash; báo cả seed fail, không chỉ chọn hình đẹp. Baseline ablation: A threshold-height cũ, B layout+ramp không height detail, C layout+ramp+certified noise. So sánh connectivity, slope, resource access, throughput và failure. Chưa có số liệu batch/match, không tuyên bố preset đã balance.

Thành phẩm milestone đầu là map hai tầng có mọi kiểm tra hình học/navigation thông qua và báo cáo metrics, không phải chỉ một PNG đẹp.



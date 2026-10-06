> **Unity 2026-10-06 — thay đổi trữ lượng theo yêu cầu người dùng:** Amount của mỏ chuyển sang config spawn (MapGenConfig.Resources.Catalog[].AmountPerMine); prefab/ResourceAuthoring không lưu Amount nữa. Mô tả nguồn trữ lượng prefab trong tài liệu lab bên dưới là lịch sử. Xem [resource config hiện tại](MapGen_ResourceSpawn_Config.md).

> **Quota ramp mới:** mỗi đoạn face cliff thẳng liên tục cùng hướng/cặp level, tách tại góc, có mục tiêu `max(1, ceil(n/12))` ô mặt ramp. Đếm chiều rộng mặt ramp, không đếm corridor/landing. Bổ sung sau repair và thống nhất núi; ưu tiên rộng cấu hình, cho phép thu đến 1 ô khi cần. Không khoét núi/góc hoặc phá khoảng cách nhóm ramp. Đoạn thiếu vị trí hợp lệ ghi `rampQuota.limited` với reason `geometry_or_ramp_spacing`, hiển thị số đoạn bị giới hạn trong HTML; không tuyên bố quota thay thế kiểm tra một island.
> **Thay đổi theo yêu cầu người dùng 2026-10-04:** bỏ clearance và unit-radius bake. Một mask `walk` dùng chung cho mọi unit; không còn field `physical` hoặc `clearance` trong output/schema. Núi, cliff chưa mở ramp và footprint resource chặn trực tiếp; HeightLevel không thay thế walkability. Cost Unity = walk ? 1 : 255. Các mô tả clearance/radius bên dưới là lịch sử và được thay thế bởi quyết định này. Retry, ramp cardinal/spacing và một island vẫn giữ. Radius input cũ bị bỏ qua, không ảnh hưởng generation.
# Thuật toán MapGen đã chọn — Lab 5

**Cấu hình được chọn mới nhất:** map **256×256 ô** (65.536 ô), `fullMap:true`, sinh terrain kín tới mép grid. Bỏ viền outside terrain hai ô; không sinh nước/outside để bao map. MountainMask, cliff collision, clearance và ramp vẫn quyết định ô chặn; sinh full map không đồng nghĩa mọi ô walkable. HTML kết hợp hiện dùng cấu hình này. Engine giữ mặc định `fullMap:false` chỉ để tái hiện các regression/preset cũ; bản port phải truyền `fullMap:true` rõ ràng.

Với fullMap, phạm vi sinh mountainMask và gán terrain kind chạy toàn grid thay vì bỏ viền hai ô. Núi ở mép vẫn hợp lệ; giữ luật không khoét núi. Không thay luật ramp, repair/retry, lấp pocket nhỏ hoặc điều kiện đúng một island. Biên ngoài grid vẫn là giới hạn map, không phải tile outside được sinh bên trong map.

Regression mới: `Labs/Lab5_6/lab56-256-tests.cjs`, seed 30000 / 4 người / radius 0.2 / width 3 cố định / resource tắt: đúng một island, không có kind=4 ngoài mountainMask, visual diagnostics=0. Kết quả hiện tại candidate index 0, 36 nhóm ramp, 2 warning art compound; đây là quan sát preset, không phải hằng số thuật toán cho mọi seed.

**Chốt bổ sung 2026-10-04 — hoàn tất session Lab 5 + Lab 6:** người dùng đã xem và chọn thuật toán trong [HTML kết hợp](Labs/Lab5_6/Lab5_6_Combined_Map_Texture.html). Lab 5 sinh terrain/occupancy/navigation; Lab 6 biên dịch cliff/ramp và render từ chính map đó. Nguồn bổ sung chuẩn: `Labs/Lab5/lab5-visual-tiles.js`, `Labs/Lab6/lab6-engine.js`, `Labs/Lab5_6/lab56-app.js`; quy trình chi tiết trong [Lab6_README.md](Labs/Lab6/Lab6_README.md). Không xét lại thuật toán hoặc quay lại các pack thử cũ khi bắt đầu session sau.

Luật đã chọn: cliff face nằm trên phía cao của biên tầng, lưu ports nối và quadrant cao/thấp riêng; ramp chỉ cardinal, nằm trên cliff thẳng, không ở góc, dài một ô theo chiều dốc. Chiều rộng dùng single hoặc left + middle + right; nhóm ramp cách nhau ít nhất một ô. Vẽ ground → cliff → ramp, sprite không kéo giãn; thu nhỏ đồng đều chỉ để xem tổng quan. Candidate thử tối đa 16 lần; từ lần thử thứ chín có fallback contour block chung theo config để tạo đoạn thẳng hợp lệ. Navigation vẫn đọc walk mask, Y=0.

**Session tiếp theo: gen núi** theo [bàn giao 2026-10-04](MapGen_Session_Handoff_2026-10-04.md). Giữ nguyên mountainMask và gameplay; thay màu núi tạm bằng bộ tile núi phù hợp. Art đầu cliff–ramp và cliff nhiều mảnh còn ghi nhận trong Lab 6; việc chọn thuật toán không đồng nghĩa các texture đã đạt bản cuối hoặc đã port Unity.

Ngày chốt: 2026-10-02. Người dùng đã chọn hướng thuật toán Lab 5 sau khi bổ sung luật giữ núi liền mạch. Đây là bản ghi bàn giao để tiếp tục ở session khác; chốt hướng sinh terrain và tài nguyên, chưa phải nghiệm thu tích hợp Unity hay chứng nhận cân bằng mọi seed.

**Bổ sung đã được người dùng yêu cầu ngày 2026-10-03:** hai cụm mặt ramp khác nhau phải cách nhau ít nhất một ô, kể cả chạm chéo; hướng ramp chỉ N/E/S/W. Luật áp dụng cho cả ramp chính và cửa nối island bổ sung. Không đổi noise, mức terrain, độ rộng cấu hình, núi gốc hoặc policy tài nguyên. Chi tiết ghép sprite và kiểm tra: [MapGen_VisualTileCompiler.md](MapGen_VisualTileCompiler.md).

## 1. Các quyết định không được thay đổi khi tiếp tục

**Policy nối island hiện hành:** thử nối mọi island với component chứa P1, ưu tiên island lớn nhất trước, bằng đường mở cliff có footprint rộng tối thiểu và không đi qua núi gốc. Chỉ khi không tìm được đường nối hợp lệ mới chuyển pocket không chứa base, tối đa **64 ô walkable/pocket**, thành núi; tổng ô lấp không vượt **2% diện tích grid/candidate**. Vùng lớn hoặc vùng chứa base không nối được phải reject/retry candidate, tối đa 16. Không lấp tất cả island phụ ngay sau khi nối base. Điều kiện cuối vẫn đúng 1 island sau resource occupancy. Các mô tả lấp mọi pocket trong lịch sử đã được thay thế bởi policy này.

Mask và render phải nhất quán: tâm cluster phải nằm trên nền walkable, không thuộc núi/ramp tại lúc chọn; mọi ô footprint kiểm tra mountainMask trực tiếp. Resource occupancy không phải núi và không được vẽ đá núi lên footprint resource. Sau chuyển pocket thành núi, loại portal không còn ô sống, remap ramp ID và đặt nhãn R tại ô nền sống của portal; không dùng tọa độ candidate cũ để ghi nhãn trên núi. Regression `Labs/Lab5/lab5-mask-consistency-tests.cjs` bao gồm seed 30000 và 4 seed khác, quota 3/3/6/6 với 10 người.

Pocket nền thường bị loại để giữ 1 island phải được chuyển thành **núi thật trong mountainMask/kind**, không chỉ blocked vô danh. Xóa nhãn cliff/ramp của các ô đó, thống nhất HeightLevel theo median của khối núi 4 hướng sau khi gộp. Không đổi mức của nền còn đi được. Render dùng cùng mask/occupancy nên pocket chuyển sang đá núi; resource không thể đặt vào đó.

**Điều kiện nghiệm thu mới nhất: walk map cuối cùng khi bật ramp phải có đúng 1 connected component (1 island), sau cả clearance và resource occupancy.** Nối mọi base là cần nhưng chưa đủ. Sau khi thử nối mọi island, chỉ pocket nhỏ không nối được mới chuyển thành núi rồi tính lại clearance; không khoét núi. Mỗi candidate resource gây thêm island bị từ chối. Cảnh tắt ramp chỉ là debug để quan sát tách tầng, không phải map hợp lệ để chơi.

- Toàn bộ map, unit và prefab cùng Y = 0. Grid dùng X/Z; height chỉ giả độ cao bằng texture.
- HeightMap lưu **mức nguyên HeightLevel**, không lưu độ cao vật lý để unit trèo dốc.
- Unit chỉ đọc walkable/cost cuối cùng. Không thêm height link, kiểm tra chênh tầng khi pathfinding hoặc movement theo Y.
- Hai ô cùng mức coi như cùng độ cao. Khác mức được tách bằng dải cliff không walkable; ramp là một hoặc nhiều ô liền kề mở qua dải đó.
- Map không đối xứng. Base chia theo N cung, đặt về phía rìa/góc phù hợp, không gom gần tâm. Chỉ vùng bảo vệ nhỏ phải cùng mức; không làm các đảo base giống hệt nhau.
- Núi là occupancy cố định: cliff dừng tại mép núi; ramp, hành lang và resource không được khoét núi. Giữ mọi ô của mask núi ban đầu.
- Resource amount nằm trong prefab. Generator quyết định loại prefab và vị trí, số prefab/cluster; không sinh trữ lượng.
- Mỗi cluster cùng một HeightLevel. Mọi prefab trong cluster phải có vị trí thu thập tiếp cận được sau khi đặt **toàn bộ** tài nguyên.

## 2. Nguồn chuẩn và thứ tự pipeline

Nguồn chạy đã chọn: `Labs/Lab5/lab5-engine.js`, `Labs/Lab5/lab5-bake.js`, `Labs/Lab5/lab5-connectivity.js`, `Labs/Lab5/lab5-clusters.js`. `Labs/Lab5/lab5-overview.js` chỉ vẽ; màu/texture minh họa không quyết định gameplay. `Labs/Lab5/lab5-view.fragment.html` điều phối UI, tắt bộ resource cũ bằng bốn count = 0 rồi gọi bộ cluster.

```text
seed + config
  → PRNG và các noise field
  → N sector + base bất đối xứng
  → raw height noise + làm mềm quanh base
  → mountain mask cố định
  → lượng tử HeightLevel + majority smoothing + core base phẳng
  → thống nhất mức của từng khối núi
  → cliff band theo chênh mức
  → mở ramp → chia cụm mặt cliff có hướng cardinal → giữ khoảng trống ≥1 ô giữa các cụm
  → physical mask → clearance → walk mask
  → thử nối mọi island bằng cửa cliff bổ sung, giữ khoảng trống ≥1 ô, không khoét núi
  → cửa bị loại làm mất liên thông: rollback rồi thử đường khác (tối đa 24 lần/island)
  → pocket không nối được ≤64 ô, không có base: chuyển thành núi (tổng ≤2%)
  → kiểm tra đúng 1 island; candidate không hợp lệ thì retry (tối đa 16)
  → thống nhất mức các khối núi sau gộp
  → thử đặt cluster → bake occupancy → audit mọi resource và đúng 1 island
  → HeightLevel + mask núi/cliff/ramp + final walk + prefab placements
```

## 3. Luật noise hiện đang chạy

PRNG xorshift32, seed được trộn bằng hash 32-bit. Noise gradient 2D có permutation 256 phần tử, 8 vector gradient và fade quintic `6t^5 - 15t^4 + 10t^3`. fBm gồm 3 octave: amplitude 1, 0.5, 0.25; frequency nhân 2; tổng chia tổng amplitude. Không lấy Math.random cho generation.

Với W là cạnh map, B/D/R lần lượt là boundary/detail/rocks noise, các field có seed riêng (XOR salt). Landscape:

```text
wx = x + 14 × B.fbm(x+81, y-39, 75)
wy = y + 14 × B.fbm(x-53, y+92, 75)
landscape = height × [0.65
  + 1.5 × B.fbm(wx+217, wy+151, W×0.38)
  + 0.4 × (1 - abs(2×R.fbm(wx-93, wy+163, W×0.23)))]
rawHeight = landscapeSauBaseBlend + amplitude × detailMask × D.fbm(x,y,wavelength)
```

Base có level tạm `landscape(base) + height×0.24`. Blend ra ngoài core bằng fade; biên ngoài `protectedRadius + 13 + 3×B.fbm(x+47,y-38,28)`. Core được ép phẳng. Float raw height chỉ tồn tại trong generation; hiện làm tròn 1/256 trước lượng tử.

Mức ô = round(trung bình 4 đỉnh / levelStep). Chạy 3 lượt majority trên lân cận 3×3, mỗi lượt đọc snapshot; khi bằng phiếu giữ lựa chọn hiện tại. Sau đó ép vùng base bán kính protectedRadius + 2 về một mức nguyên. Đây là collar bảo vệ để cliff không xén sát điểm spawn.

Mountain mask: ô trong biên map 2 ô, ngoài mọi base protectedRadius + 4, có `R.fbm(x+79,y-143,19) > 0.20`. Đóng khe một ô nếu hai ô đối diện ngang hoặc dọc đều là núi, chỉ **thêm** ô. Flood-fill 4 hướng mỗi khối núi, lấy median HeightLevel của khối và gán chung. Không dùng miễn trừ đường/resource để cắt mask núi.

## 4. Base, cliff và ramp

Base: chia góc 2π/N, sector offset π/4 với jitter; mỗi base có jitter góc độc lập và bán kính 0.88–0.98 của khoảng cách đến mép trừ margin 27 ô. Không mirror terrain/resource giữa player. Công thức hiện tại phù hợp phạm vi lab, cần kiểm định riêng khi scale N/size.

Cliff chỉ sinh giữa hai ô nền trống khác mức, không giữa ô núi. Độ dày = abs(deltaLevel) × cliffCells, mở rộng Manhattan ở phía tầng thấp và chỉ trong đúng mức thấp đó. Các band ở góc/junction có thể chồng nhau, dày hơn công thức đơn. Cliff đặt physical = 0.

Ramp chọn trên interface giữa hai region cùng mức nội bộ (region 4 hướng chỉ dùng lúc generation). Candidate được shuffle bằng seed. Mỗi interface có tối đa rampsPerBorder cửa; cửa đầu dùng widthMin, cửa thứ hai widthMax, các cửa tiếp lấy ngẫu nhiên trong khoảng. Không đảm bảo widthMax sẽ fit.

Ramp là brush chữ nhật: chiều dài `2 × (deltaLevel×cliffCells + 3)`. Mỗi lane phải có hai landing khác mức không nằm trên cliff, đúng một lần chuyển tầng, chỉ đi qua hai mức của interface. Mọi ô phải là nền trống trước cliff bake; không đè núi, ramp khác hoặc core base. Cửa phải đủ xa cửa đã tạo. Candidate thất bại thì bỏ; không mở đường xuyên núi để cứu connectivity. Ramp hợp lệ đặt physical = 1 và lưu ramp ID để render/debug.

## 5. Walk map và vị trí xây

### Nối island trước khi lấp — thuật toán hiện hành

1. Bake clearance theo unit radius và flood-fill bằng đúng luật navigation. Chọn component chứa tâm P1 làm component đích; mọi base phải có source hợp lệ.
2. Chọn island ngoài đích lớn nhất. Dijkstra đa nguồn từ mọi ô của island đủ footprint vuông widthMin, đi 4 hướng trên nền solid; cost bước = 1 + 12 cho mỗi ô blocked trong footprint. Núi/biên không thuộc nền solid nên không thể đi xuyên.
3. Nếu tìm được đường, chỉ mở ô cliff cần thiết, ghi ramp và landing. Tính lại walk/components; đường phải thực sự nối source với component đích. Lặp cho các island còn lại. Không chỉ nối các island có base.
4. Nếu không có đường nối hợp lệ: chỉ lấp island **không chứa base**, diện tích ≤64 ô walkable và tổng diện tích lấp ≤floor(W×H×0.02). Chuyển các ô đó thành núi rồi bake clearance lại. Đây là giới hạn nội bộ hiện tại, chưa phải control UI.
5. Nếu không đủ điều kiện lấp hoặc đường mở không đạt kiểm tra, reject cả candidate; engine thử tối đa 16 candidate xác định từ seed/attempt rồi báo lỗi. Không tăng giới hạn lấp để cứu map.
6. Sau khi còn 1 island, gộp mức núi, dọn portal chết/nhãn ramp. Đặt resource sau bước này; mỗi trial resource phải giữ 1 island và access của mọi prefab. Không lấp ô trống cluster để sửa lỗi do resource.

Các cửa nối bổ sung có thể vượt quota ramp trang trí mỗi interface và đi qua nhiều band. Quy tắc này kiểm tra connectivity; hình dạng cửa và cân bằng cạnh tranh vẫn cần duyệt riêng.

Kết quả seed 30000, 10 player, protectedRadius 6, unit radius 0.2, widthMin/Max 3/7: candidate đầu giữ đúng 1 island, mở 7 cửa nối bổ sung; ô núi thêm (gồm đóng khe noise) giảm từ 3165 của bản lấp cũ xuống 112. Không lấy số liệu bản cũ làm kết quả thuật toán hiện hành.

Physical mask gồm núi/biên/cliff và occupancy prefab; ramp chỉ mở lại ô cliff hợp lệ. Clearance hiện tại là khoảng cách Euclidean đến tâm ô blocked trừ √0.5, chặn âm về 0. `walk = physical && clearance >= unitRadius + 0.05` (đơn vị ô grid).

Dijkstra 8 hướng có cost 1 cho bước ngang/dọc và √2 cho bước chéo; diagonal chỉ hợp lệ khi cả hai ô cạnh ngang/dọc đều walkable, tránh cắt góc. Connected components dùng cùng luật. Height/ramp ID không được đọc để quyết định bước đi.

Chưa triển khai luật building placement Unity trong lab. Khi tích hợp: giữ điều kiện footprint/occupancy hiện có, bổ sung footprint cùng HeightLevel và tránh cliff/ramp nếu building yêu cầu nền phẳng. Không coi một ô walkable đơn lẻ là đủ để xây cả footprint. Base spawn đã có core phẳng.

## 6. Cluster tài nguyên

**Cluster không phải vật cản.** Chỉ footprint 2×2 của từng prefab được ghi vào `resourceMask`; `physical = terrainPhysical AND NOT resourceMask`. Hàm chuẩn `RTSClusters.bakeResourceOccupancy` không đọc radius, đường bao hoặc bounding box cluster. Ô trống giữ physical của địa hình; walk vẫn phải qua clearance theo radius unit, nên ô sát resource có thể không đủ khoảng trống cho unit lớn. Không chuyển ô trống thành núi vì cluster. `Labs/Lab5/lab5-resource-occupancy-tests.cjs` so sánh toàn bộ grid trước/sau placement ở seed 30000/1337/2000, kiểm tra mask núi không đổi, chỉ footprint bị chặn và walk còn đúng 1 island.

Bốn role: main/chính, secondary/phụ, advantage/lợi thế, contested/tranh chấp. Quota riêng mỗi player; contested quota mỗi cung. Count của từng cluster lấy số nguyên đều trong [clusterMin, clusterMax]. Mỗi prefab lab chiếm 2×2 ô, type xen kẽ 0/1; mapping sang prefab game chưa chốt.

Tâm lấy độc lập theo annulus với `r = sqrt(lo² + u×(hi²-lo²))`, không dùng một anchor cố định cho mọi mỏ:

| Role | Tâm lấy quanh | Khoảng bán kính (ô) |
| --- | --- | --- |
| Main | Base | 5 … max(11, W×0.09) |
| Secondary | Base | W×0.11 … W×0.22 |
| Advantage | Base | W×0.23 … W×0.40 |
| Contested | Tâm map, trong cung của base | W×0.07 … W×0.28 |

Cluster radius = max(4, sqrt(count)×2.3); tâm cluster cách nhau ít nhất tổng hai radius + 3. Member sample đều theo diện tích trong radius, cách nhau ít nhất 3 ô. Tối đa 100 candidate tâm/cluster và 160 draw member/candidate. Phải đủ đúng count mới xét nhận; thất bại bỏ cả cluster và báo placed/requested.

Mọi ô footprint phải physical trống, cùng level của cluster, không là ramp, không sát base trong 3 ô. Các khoảng trống trong vòng tròn cluster không bị ép phẳng: luật cùng mức áp dụng các footprint resource.

Private cluster phải có đường từ owner; kiểm tra khoảng cách đường đi ban đầu: main trung bình ≤ W×0.15; secondary trung bình trong W×0.08 … W×0.30; advantage trung bình ≤ W×0.50 và resource gần nhất xa hơn secondary xa nhất ít nhất 4 ô (nếu không có secondary dùng W×0.18).

Mỗi trial đặt toàn bộ footprint vào occupancy, tính lại clearance/walk/components rồi audit mọi prefab của cả cluster cũ và mới. Vị trí thu thập là ô walkable cùng level trong vùng kiểm tra quanh footprint, reach = ceil(radius+1). Private cluster có một component chung từ owner đến tất cả member. Contested cluster có một component chung tới tất cả member và tới ít nhất 2 base. Terrain phải đạt 1 island trước placement; mọi trial phải giữ đúng 1 island và mọi base có source hợp lệ. Audit cuối sau tất cả placement.

## 7. Cấu hình lần thử gần nhất đã chọn

Cấu hình nhận từ trạng thái Lab 5 lúc người dùng chọn hướng thuật toán; đây là preset thử, không đổi default source:

```json
{
  "scene": "noise", "seed": 300, "players": 4, "size": 192,
  "amplitude": 0.8, "wavelength": 36, "height": 3,
  "protectedRadius": 6, "levelStep": 1, "cliffCells": 1,
  "rampWidthMin": 3, "rampWidthMax": 7, "rampsPerBorder": 2,
  "radius": 0.2, "rampEnabled": true,
  "clusterMin": 2, "clusterMax": 4,
  "mainClusters": 3, "secondaryClusters": 3,
  "advantageClusters": 6, "contestedClusters": 6,
  "view": "levels", "debug": true
}
```

Đây là preset lịch sử lúc chốt hướng, trước luật 1 island; kết quả 25 vùng walk khi đó không còn đạt nghiệm thu hiện tại. UI hiện có control players 2–10 và protectedRadius 4–16, mặc định preview 10 người/radius 6. Map size vẫn 192; các tham số noise trong preset là default engine.

## 8. Kiểm chứng và giới hạn để session sau không hiểu sai

Kiểm chứng sau đổi policy: `lab5-island-policy-tests.cjs` kiểm tra vùng bị cliff tách được nối mà không lấp, vùng lớn bị núi gốc tách phải reject mà không lấp, pocket 16 ô bị núi bao quanh được chuyển thành núi. `lab5-connectivity-tests.cjs` chạy lại 27 cấu hình: đúng 1 island trước và sau placement được kiểm tra, tổng 208 cửa bổ sung và 3 lượt retry. `lab5-mask-consistency-tests.cjs` chạy lại 5 seed 10 người (30000/2000/1337/42/200000): resource không đè núi, nhãn ramp nằm trên nền sống, tất cả map đạt 1 island. Browser test 2/6/10 player và protectedRadius 4/6/16 không lỗi JS, không tràn ngang ở 320px. Đây là phạm vi kiểm tra cụ thể, không chứng minh mọi seed/config đều tạo được map hay đạt balance.

Kiểm chứng sau đổi policy: `lab5-island-policy-tests.cjs` kiểm tra vùng bị cliff tách được nối mà không lấp, vùng lớn bị núi gốc tách phải reject mà không lấp, pocket 16 ô bị núi bao quanh được chuyển thành núi. `lab5-connectivity-tests.cjs` chạy lại 27 cấu hình: đúng 1 island trước và sau placement được kiểm tra, tổng 208 cửa bổ sung và 3 lượt retry. `lab5-mask-consistency-tests.cjs` chạy lại 5 seed 10 người (30000/2000/1337/42/200000): resource không đè núi, nhãn ramp nằm trên nền sống, tất cả map đạt 1 island. Browser test 2/6/10 player và protectedRadius 4/6/16 không lỗi JS, không tràn ngang ở 320px. Đây là phạm vi kiểm tra cụ thể, không chứng minh mọi seed/config đều tạo được map hay đạt balance.

Các test đã chạy trong session: `lab5-browser-tests.cjs` (demo delta 1/2/3, band, bật/tắt ramp, cửa 1 ô, integer height, navigation mask), `lab5-cluster-tests.cjs` (min/max, cùng level, final access, ramp bounds, layout 320px), `lab5-mountain-tests.cjs` (seed 1/42/1337, giữ núi qua bake và resource). Không phải sweep mọi seed, không chứng minh map balance hoặc quota luôn đủ. Seed 300 là trạng thái người dùng thử, chưa nằm trong bộ mountain test đã ghi trên.

Engine Lab 5 còn code di sản Lab 4: slope, curved-ramp planning, resource placement và balance rows. UI tắt resource cũ; curved height ramp/prune/slope blocking cuối bị vô hiệu hóa. Bake thực tế nhận mask từ kind (chỉ núi/biên chặn), không dùng slope để chặn unit. `accepted/errors/hash` của engine cũ không được cập nhật theo bộ cluster cuối, nên **không dùng chúng làm certificate/hash final map**. UI cluster audit chứng nhận access của prefab đã đặt và đúng 1 island khi bật ramp; không chứng nhận cân bằng kinh tế.

Chưa sửa Unity gameplay trong session này. Texture hiện tại là minh họa; cần ground/cliff/ramp tiles giả cao thấp, vẫn cùng Y. Chưa cam kết line-of-sight/high-ground bonus.

## 9. Điểm tiếp tục session sau — cập nhật 2026-10-03

Visual workflow bổ sung: [CliffFaceMap/RampTileMap](MapGen_VisualTileCompiler.md). Compiler thuần sau generation chọn sprite cố định từng ô: cliff thẳng/góc trong/góc ngoài/endcap, ramp start/middle/end hoặc single, turn/tee/cross cho tuyến cong/giao nhau. Không kéo sprite, không xử lý riêng seed, không đổi levels hoặc gameplay masks. Sample dùng compiler này; renderer lab chính và Unity chưa được nối vào.

Thuật toán Lab 5 đã chốt và người dùng đánh giá kết quả generation tốt. Session tiếp theo bắt đầu port nguyên thuật toán sang Unity theo [kế hoạch port](Archive/2026-10-04/MapGen_NextSteps_2026-10-03.md):

1. Port RNG/noise và config/result C#.
2. Port base → mountain/levels → cliff/ramp → clearance/connectivity/retry → cluster/resource audit, giữ nguyên luật trong tài liệu này.
3. Làm preview Unity để đối chiếu cùng seed/config với Lab 5.
4. Nạp grid, mapping prefab và spawn tài nguyên/TownHall/worker; kiểm tra movement, gathering và building footprint.
5. Gắn hiển thị terrain/cliff/ramp cùng Y.

Các kiểm tra phục vụ phát hiện sai lệch bản port và lỗi tích hợp. Không mở lại thiết kế thuật toán, không tự thêm policy balance/quota, không bắt buộc refactor lab hoặc làm final hash trước khi port. Hash/report được bổ sung trong quá trình triển khai khi cần đối chiếu dữ liệu cuối.

Rebuild lab bằng `python Docs/Multiplayer/Labs/Lab5/build_lab5.py`; mở `Labs/Lab5/Lab5_FlatY_Cliff_Ramp_Bake.html`. Lab 5 và các luật trong tài liệu này tiếp tục là nguồn chuẩn nếu plan cũ mâu thuẫn.


### Bổ sung vị trí sprite cliff — 2026-10-03

Mọi sprite cliff thuộc ô phía cao sát viền HeightLevel, kể cả connector góc ở đỉnh chung. Không dùng cliffMask collision phía thấp làm danh sách ô vẽ. Ramp thay face ở highCell; lowCell là đầu tiếp cận thấp. rampFaceGroups từ bake xuất thêm highCells, giữ cells cho collision/cửa mở. Spacing kiểm tra cả hai phía để chuyển nơi vẽ không gây chạm ramp. Không thay đổi HeightLevel hoặc thêm collision để nối góc hình ảnh.




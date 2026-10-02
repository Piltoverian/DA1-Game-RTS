# Thuật toán MapGen đã chọn — Lab 5

Ngày chốt: 2026-10-02. Người dùng đã chọn hướng thuật toán Lab 5 sau khi bổ sung luật giữ núi liền mạch. Đây là bản ghi bàn giao để tiếp tục ở session khác; chốt hướng sinh terrain và tài nguyên, chưa phải nghiệm thu tích hợp Unity hay chứng nhận cân bằng mọi seed.

## 1. Các quyết định không được thay đổi khi tiếp tục

**Policy nối island hiện hành:** thử nối mọi island với component chứa P1, ưu tiên island lớn nhất trước, bằng đường mở cliff có footprint rộng tối thiểu và không đi qua núi gốc. Chỉ khi không tìm được đường nối hợp lệ mới chuyển pocket không chứa base, tối đa **64 ô walkable/pocket**, thành núi; tổng ô lấp không vượt **2% diện tích grid/candidate**. Vùng lớn hoặc vùng chứa base không nối được phải reject/retry candidate, tối đa 16. Không lấp tất cả island phụ ngay sau khi nối base. Điều kiện cuối vẫn đúng 1 island sau resource occupancy. Các mô tả lấp mọi pocket trong lịch sử đã được thay thế bởi policy này.

Mask và render phải nhất quán: tâm cluster phải nằm trên nền walkable, không thuộc núi/ramp tại lúc chọn; mọi ô footprint kiểm tra mountainMask trực tiếp. Resource occupancy không phải núi và không được vẽ đá núi lên footprint resource. Sau chuyển pocket thành núi, loại portal không còn ô sống, remap ramp ID và đặt nhãn R tại ô nền sống của portal; không dùng tọa độ candidate cũ để ghi nhãn trên núi. Regression `Labs/lab5-mask-consistency-tests.cjs` bao gồm seed 30000 và 4 seed khác, quota 3/3/6/6 với 10 người.

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

Nguồn chạy đã chọn: `Labs/lab5-engine.js`, `Labs/lab5-bake.js`, `Labs/lab5-connectivity.js`, `Labs/lab5-clusters.js`. `Labs/lab5-overview.js` chỉ vẽ; màu/texture minh họa không quyết định gameplay. `Labs/lab5-view.fragment.html` điều phối UI, tắt bộ resource cũ bằng bốn count = 0 rồi gọi bộ cluster.

```text
seed + config
  → PRNG và các noise field
  → N sector + base bất đối xứng
  → raw height noise + làm mềm quanh base
  → mountain mask cố định
  → lượng tử HeightLevel + majority smoothing + core base phẳng
  → thống nhất mức của từng khối núi
  → cliff band theo chênh mức
  → mở các ramp hợp lệ
  → physical mask → clearance → walk mask
  → thử nối mọi island bằng cửa cliff bổ sung, không khoét núi
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

**Cluster không phải vật cản.** Chỉ footprint 2×2 của từng prefab được ghi vào `resourceMask`; `physical = terrainPhysical AND NOT resourceMask`. Hàm chuẩn `RTSClusters.bakeResourceOccupancy` không đọc radius, đường bao hoặc bounding box cluster. Ô trống giữ physical của địa hình; walk vẫn phải qua clearance theo radius unit, nên ô sát resource có thể không đủ khoảng trống cho unit lớn. Không chuyển ô trống thành núi vì cluster. `Labs/lab5-resource-occupancy-tests.cjs` so sánh toàn bộ grid trước/sau placement ở seed 30000/1337/2000, kiểm tra mask núi không đổi, chỉ footprint bị chặn và walk còn đúng 1 island.

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

## 9. Điểm tiếp tục session sau

**Luật bổ sung mới nhất — base không được cô lập khi mở ramp:** sau ramp thông thường, `Labs/lab5-connectivity.js` audit component của mọi ô tâm base trên walk mask theo unit radius. Nếu tách nhóm, chạy Dijkstra 4 hướng trên mask nền có đủ footprint widthMin; đường qua cliff có giá cao, núi/biên bị cấm. Mở các ô cliff trong footprint đường nối, đánh dấu landing và giữ HeightLevel/Y. Đường bổ sung có thể cong và đi qua nhiều band; không bị quota ramp trang trí chặn. Kiểm tra lại bằng walk map thật. Nếu không sửa hợp lệ, bỏ toàn candidate và thử seed-derived attempt tiếp theo, tối đa 16; sau đó báo lỗi chứ không trả map base bị cô lập. Khi người dùng tắt ramp, vẫn giữ thí nghiệm tách tầng. UI ghi số cửa bổ sung và candidate. Quy tắc này thay cho việc giữ base bị cô lập trong các ghi chú trước; mọi island được thử nối trước; chỉ pocket nhỏ không nối được mới chuyển thành núi. Resource placement tiếp tục bảo toàn kết nối đã có.

Kiểm thử mới: `lab5-connectivity-tests.cjs`, 27 cấu hình 2/6/10 người với 9 seed, có cliffCells 2 và protectedRadius 16; tất cả base nối nhau, không khoét núi, không có bước khác tầng mở mà thiếu nhãn ramp. Ba seed 10 người 1337/2000/200000 còn được audit sau quota 3/3/6/6 và prefab 2–4. Chưa phải chứng minh cho mọi seed/config, vẫn có thể báo thiếu cluster hoặc hết candidate hợp lệ.

Cập nhật thử nghiệm nhiều người (2026-10-02): Lab 5 đã mở control 2–10 player và protectedRadius 4–16 ô; map vẫn 192×192, preview mới mặc định 10 player. Debug vẽ vòng core và UI báo nhóm base kết nối, min spacing, cặp core+collar chồng nhau. Kết quả mới nhất seed 1337/quota mặc định: 10 player ở radius 6 và 16 đều có 1 island, 40/40 cluster sau sửa connectivity. Đây là phạm vi mới thay cho giới hạn 2–8 trước đó; các kết quả không chứng nhận cân bằng.

1. Đọc tài liệu này và 4 source chuẩn; giữ các quyết định mục 1. Không quay lại slope/Y/height link của kế hoạch cũ.
2. Tách pipeline thuần khỏi di sản Lab 4; tạo hash final bao gồm config, levels, mountain/cliff/ramp masks, occupancy và prefab placements.
3. Đối chiếu grid cost, building footprint, resource prefab và unit radius hiện tại của Unity để port từng bước; kiểm tra reproducibility C#/JS trước khi dùng seed giữa client/host.
4. Sweep seed và 2–10 player, đo đúng 1 island, quota thiếu, access theo role và áp lực hàng xóm. Bổ sung chọn candidate tốt nhất nếu cần balance; không phá núi và không mirror map để sửa.
5. Kiểm tra hình dáng núi sinh thêm từ pocket và các cửa bổ sung, nhất là radius lớn; policy là nối trước, chỉ lấp pocket không nối được ≤64 ô và tổng ≤2%; reject vùng lớn không nối được.
6. Tạo tile terrain thật và mapping integer level + cliff/ramp direction; movement tiếp tục chỉ dùng walk/cost.

Rebuild lab bằng `python Docs/Multiplayer/Labs/build_lab5.py`; mở `Labs/Lab5_FlatY_Cliff_Ramp_Bake.html`. Đọc thêm `Labs/Lab5_README.md`. Tài liệu này là quyết định hướng mới, ưu tiên hơn phần hướng height vật lý trong các plan nghiên cứu trước đó nếu mâu thuẫn.

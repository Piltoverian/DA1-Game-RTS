> **Triển khai 2026-10-05:** người dùng đã yêu cầu viết đầy đủ để đọc. Bản C# terrain -> render và menu preview đã có; xem [hướng dẫn đọc/chạy](MapGen_Unity_ReadingGuide.md). Dùng RNG C# hiện có, kiểm tra luật bảo vệ thay vì parity từng field với JS. Runtime/Editor đã compile và 12 case generator đã pass; visual QA và Play Mode movement chưa nghiệm thu. Các câu "chưa yêu cầu triển khai" bên dưới là lịch sử lập kế hoạch.
> **Quota ramp mới:** mỗi đoạn face cliff thẳng liên tục cùng hướng/cặp level, tách tại góc, có mục tiêu `max(1, ceil(n/12))` ô mặt ramp. Đếm chiều rộng mặt ramp, không đếm corridor/landing. Bổ sung sau repair và thống nhất núi; ưu tiên rộng cấu hình, cho phép thu đến 1 ô khi cần. Không khoét núi/góc hoặc phá khoảng cách nhóm ramp. Đoạn thiếu vị trí hợp lệ ghi `rampQuota.limited` với reason `geometry_or_ramp_spacing`, hiển thị số đoạn bị giới hạn trong HTML; không tuyên bố quota thay thế kiểm tra một island.
> **Thay đổi theo yêu cầu người dùng 2026-10-04:** bỏ clearance và unit-radius bake. Một mask `walk` dùng chung cho mọi unit; không còn field `physical` hoặc `clearance` trong output/schema. Núi, cliff chưa mở ramp và footprint resource chặn trực tiếp; HeightLevel không thay thế walkability. Cost Unity = walk ? 1 : 255. Các mô tả clearance/radius bên dưới là lịch sử và được thay thế bởi quyết định này. Retry, ramp cardinal/spacing và một island vẫn giữ. Radius input cũ bị bỏ qua, không ảnh hưởng generation.
# Kế hoạch port Lab 6 vào Unity — 2026-10-04

**Cập nhật cấu hình đích:** dùng **256×256, fullMap=true** như kích thước game và HTML mới nhất. Sinh kín grid, không có viền outside hai ô. Các mốc 192×192 bên dưới là fixture lịch sử để đối chiếu, không phải kích thước mặc định bản port. Exporter/importer phải lưu fullMap trong config; thêm preset 256×256 seed 30000 / 4 người / radius 0.2 / width 3 / resource tắt vào nghiệm thu. Kiểm tra không có outside tile nhưng vẫn giữ núi, cliff và một island sau clearance.

Người dùng yêu cầu lập kế hoạch, chưa yêu cầu triển khai port. Giữ nguyên thuật toán đã chọn trong `Labs/Lab5_6/Lab5_6_Combined_Map_Texture.html`. Lab 6 là visual compiler và render plan; nó cần dữ liệu Lab 5, không tự sinh terrain. Bắt đầu bằng snapshot Lab 5 để có preview Unity sớm, sau đó nối generator C# khi port Lab 5 hoàn tất. Nhiệm vụ gen art núi vẫn giữ riêng, không bắt buộc hoàn thành trước preview.

## Hiện trạng repository

- Unity 6000.3.13f1 theo `ProjectSettings/ProjectVersion.txt`.
- `Assets/Scripts/Game(New Simulation Logic)/MapGen/MapGenerator.cs` hiện có hàm noise trên MonoBehaviour, chưa chứng minh tương đương toàn pipeline Lab 5.
- `MovementAgent/Grid/GridComponent.cs` có width/height/cellsize/origin, generation/islandGeneration và cost buffer.
- `GridInitSystem.cs` khởi tạo cost=1 rồi tắt system. Nạp terrain phải sau bước khởi tạo thực tế; không chỉ dựa vào thứ tự hai system nằm khác update group.
- `BlockageGridBakeSystem.cs` hiện hoàn trả cost=1 khi blockage bị xóa. Cần sửa cơ chế tổng hợp occupancy trước khi đưa terrain vào gameplay, nếu không có thể mở lại ô cliff/núi.
- Nguồn port visual: `Labs/Lab5/lab5-visual-tiles.js` và `Labs/Lab6/lab6-engine.js`. Asset thử nghiệm: `Assets/Art/Terrain/Lab5TextureReview_v17/atlas.png`, native 1254×1254 gồm chín tile 418×418.

## Quyết định thiết kế

Giữ index `i=y*W+x`, cùng Y=0, HeightLevel nguyên. Cliff collision phía thấp khác cliff face phía cao; ô 10442 ở preset HTML seed 30000 là collision cliff, không phải pocket bị bỏ sót. Render không ghi walk/physical hoặc tự lấp ô theo màu texture.

Tách bốn phần:

| Phần | Kiểu dự kiến | Trách nhiệm |
|---|---|---|
| Dữ liệu | `TerrainSnapshot`, `TerrainConfig` | Levels, physical, walk, clearance, mountain/cliff/ramp masks, ramp direction, bases và config đầy đủ |
| Compiler | `CliffRampVisualCompiler` | Port face/parts, ports và high/low quadrants, nhóm ramp, connector và diagnostics |
| Plan | `TerrainRenderPlan`, `TileDraw` | Cell, layer, asset ID, rotation, ports, high/low, low/high cells, group ID, warnings |
| Presentation | `TerrainTileCatalog`, `TerrainChunkRenderer`, `TerrainPreviewController` | Atlas/UV, chunk mesh, đổi view, inspector và regeneration |

Các tên trên là đề xuất file mới, chưa tồn tại. Compiler C# thuần không phụ thuộc MonoBehaviour, không RNG, không sửa gameplay; chưa Burst hóa trong giai đoạn đối chiếu.

## 1. Snapshot và preview nhập dữ liệu

Mở rộng exporter JS thành schema versioned có đầy đủ input compiler, seed/config/attempt và output chuẩn. `terrain.json` hiện chưa có physical/cliffMask/rampCells/rampFaceDirection; không đủ để tái biên dịch visual. `render_plan.json` dùng làm kết quả đối chiếu và có thể render trực tiếp trong milestone đầu.

Tạo Editor importer đọc snapshot, validate độ dài W×H và atlas, báo lỗi rõ ràng. Dựng scene preview riêng, không thay scene gameplay. Nạp plan JS và vẽ crop 10×10/full 192×192 trước. Export hai preset khác nhau: HTML width=3 cố định và CLI width min=3/max=7; không so kết quả khi config khác nhau.

Nghiệm thu: scene vẽ cùng cell/asset/orientation/layer với HTML; đổi view không đổi snapshot.

## 2. Port visual compiler và render plan

Port theo thứ tự hàm JS: phân loại face/parts và high/low → crossings cardinal → gom nhóm và hướng toàn cục → vai single/left/center/right → reciprocal connections → connector → asset requirement → kiểm tra và sort draw.

Luật bắt buộc: face cliff phía cao đúng biên tầng; ports N/E/S/W độc lập quadrant NW/NE/SE/SW; góc phân biệt outer/inner theo phía cao/thấp. Ramp chỉ trên face một mảnh thẳng, không ở góc; low/high kề cardinal và vector trùng hướng toàn nhóm. Slope dài một ô, width ghép tile, nhóm cách ≥1 ô theo luật Lab 5. Render ground → cliff → ramp; face open không vẽ cliff kín lên ramp. Connector trang trí không thêm collision.

Giữ thứ tự duyệt và tie-break của JS. So field trên từng cell/part/group trước khi so ảnh. Bổ sung đối chiếu cell 10442, các hướng corner và các ca synthetic hiện có. Sai ports/high-low hoặc ramp hướng sai phải trả lỗi; compound opaque parts giữ warning như lab, không âm thầm coi art hoàn thiện.

Nghiệm thu: C# compiler khớp plan JS trên cùng snapshot, không sửa input arrays, lặp lại cùng output.

## 3. Renderer Unity

Dùng mesh theo chunk (đề xuất 16×16 ô/chunk), atlas và số material ít; không tạo GameObject/SpriteRenderer riêng cho từng ô trong full map. Preview đầu tiên dùng một hệ tọa độ rõ ràng: grid x→world X, grid y→world Z, origin từ GridComponent. Camera nhìn từ +Y, hướng screen-up tương ứng grid N. Kiểm tra bốn hướng bằng scene nhỏ có nhãn N/E/S/W trước khi xem map.

Mapping rotation dùng hoán vị UV 90° quanh tâm tile để khớp ảnh canvas; không đoán dấu quay từ Quaternion. Rect atlas từ trái-trên trong JS phải đổi sang UV trái-dưới của Unity. Sprite luôn một cell vuông; 418 pixel là độ phân giải nguồn, cellsize là kích thước world. Không kéo head/middle để lấp nhiều ô.

Chọn một đường render có layer ổn định: mesh subpasses/material queue hoặc offset presentation rất nhỏ để tránh z-fighting; không tăng Y gameplay theo HeightLevel. Import texture giữ kích thước atlas, kiểm tra max texture size/compression/filter/mipmap/padding bằng crop cận và camera xa để tránh bleed. Không tuyên bố pixel-seam QA cũ chứng minh mọi khoảng zoom Unity.

Preview có seed/config label, texture/HeightLevel/walk/collision views, khoanh ramp + uphill arrow, inspector gồm physical/clearance/blockedReason. Núi/nước dùng màu tạm đến khi có art. Ô compound và đầu cliff–ramp hiện còn vấn đề art; renderer phải hiển thị warning.

Nghiệm thu: bốn rotation, mặt cao/thấp, layer và ramp giống lab; regenerate dọn mesh/material owned cũ, không rò asset.

## 4. Nối generator C# Lab 5

Theo kế hoạch port Lab 5 đã có, giữ RNG/noise/uint overflow/rounding, smoothing, base core, mountain occupancy, ramp spacing, repair/retry và contour fallback từ candidate thứ chín. Không dùng noise helper hiện có chỉ vì tên giống; đối chiếu trước.

Generator trả snapshot theo cùng schema → compiler → plan → renderer. Khởi đầu tắt resource như HTML. Cluster/resource và quota/access là phạm vi Lab 5 tiếp theo, không gộp vào sửa renderer. Khi bật resource, audit cuối vẫn đúng một island sau clearance/occupancy.

Nghiệm thu: seed 300, 30000, 1337, 2000, 2/4/10 người và radius 0.2/0.65 khớp levels/masks/attempt với JS ở các config đã chọn; không dùng hash cũ thay đối chiếu dữ liệu.

## 5. Tích hợp grid gameplay

Sau tín hiệu GridReady, nạp terrain cost và cập nhật generation/dirty theo lifecycle hiện có. Tách terrain occupancy khỏi blockage động; khi bỏ nhà/resource, tính lại cost từ terrain và các blocker còn lại thay vì hard-code cost=1. Kiểm tra cost convention và clearance trong movement trước khi nối để không áp bán kính unit hai lần.

Navigation đọc final walk/cost, không thêm height-link, slope/Y movement hoặc permission theo sprite. Renderer đọc cùng snapshot nhưng không biến mỗi face phía cao thành collider. Mount/cliff band và resource footprint giữ semantics Lab 5.

Nghiệm thu Play Mode: unit qua ramp, không qua cliff/núi, tháo blocker không mở terrain, đúng một island trên map tĩnh được chấp nhận; đổi seed không giữ grid/mesh cũ. Resource placement và setup TownHall/worker theo kế hoạch Lab 5, không thuộc milestone visual đầu.

## Thứ tự triển khai và hoàn tất

1. Export/import snapshot + scene render plan JS (bản xem Unity đầu tiên).
2. Port compiler C#, chạy so từng trường với JS.
3. Hoàn thiện chunk renderer và debug views.
4. Nối generator C# Lab 5 khi port xong.
5. Nối grid và kiểm tra Play Mode.

Port Lab 6 visual hoàn tất khi snapshot và compiler C# cho đúng plan, Unity vẽ đúng orientation/layer và lifecycle. Port kết hợp gameplay hoàn tất sau milestone 4–5. Art núi và việc polish texture là nhánh riêng có thể tiến hành song song, vẫn cần người dùng duyệt. Không cần thay thuật toán đã chốt để bắt đầu milestone 1.


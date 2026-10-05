> **Quota ramp mới:** mỗi đoạn face cliff thẳng liên tục cùng hướng/cặp level, tách tại góc, có mục tiêu `max(1, ceil(n/12))` ô mặt ramp. Đếm chiều rộng mặt ramp, không đếm corridor/landing. Bổ sung sau repair và thống nhất núi; ưu tiên rộng cấu hình, cho phép thu đến 1 ô khi cần. Không khoét núi/góc hoặc phá khoảng cách nhóm ramp. Đoạn thiếu vị trí hợp lệ ghi `rampQuota.limited` với reason `geometry_or_ramp_spacing`, hiển thị số đoạn bị giới hạn trong HTML; không tuyên bố quota thay thế kiểm tra một island.
> **Thay đổi theo yêu cầu người dùng 2026-10-04:** bỏ clearance và unit-radius bake. Một mask `walk` dùng chung cho mọi unit; không còn field `physical` hoặc `clearance` trong output/schema. Núi, cliff chưa mở ramp và footprint resource chặn trực tiếp; HeightLevel không thay thế walkability. Cost Unity = walk ? 1 : 255. Các mô tả clearance/radius bên dưới là lịch sử và được thay thế bởi quyết định này. Retry, ramp cardinal/spacing và một island vẫn giữ. Radius input cũ bị bỏ qua, không ảnh hưởng generation.
# Lab 6: thuật toán ghép tile cliff/ramp

Preset HTML mới nhất: 256×256, `fullMap:true`. Sinh terrain đến hết mép grid, không có viền outside terrain hai ô. Núi vẫn giữ occupancy; toàn map không có nghĩa mọi ô đều walkable. Seed 30000 / 4 người / radius 0.2 / rộng ramp 3 kiểm tra đúng một island và không có kind=4 ngoài mountainMask. Config mặc định của engine vẫn giữ preset cũ để đối chiếu regression; HTML bật fullMap rõ ràng.

**Trạng thái 2026-10-04: đã hoàn tất session, thuật toán kết hợp Lab 5 + Lab 6 được người dùng chọn. Session tiếp theo gen núi.** Xem [bàn giao](../../MapGen_Session_Handoff_2026-10-04.md). Các giới hạn art bên dưới vẫn được giữ để tiếp tục đúng phạm vi.

Trang kết hợp tự chứa: [Lab5_6_Combined_Map_Texture.html](../Lab5_6/Lab5_6_Combined_Map_Texture.html). Đổi seed/players/radius/width, gen trực tiếp, xem texture/HeightLevel/walk mask, zoom/pan, inspect ô và Shift+click để thử đường đi. Thuật toán và atlas được nhúng trong HTML, không cần server. Rebuild bằng `node Docs/Multiplayer/Labs/Lab5_6/build-lab56.cjs`.

Lab 6 tiếp nối terrain và navigation của Lab 5. Đầu vào là seed/config; đầu ra gồm terrain, render plan toàn map, crop 10×10, ảnh native và trang xem tương tác. Không dùng ảnh AI để quyết định layout map, không có ngoại lệ cho một seed, không kéo sprite.

## Chạy

```powershell
node Docs/Multiplayer/Labs/Lab6/lab6-generate.cjs 30000
pwsh -File Docs/Multiplayer/Labs/Lab6/render-lab6.ps1
node Docs/Multiplayer/Labs/Lab6/lab6-tests.cjs
```

Mở `Lab6_TileMap_Cliff_Ramp.html` sau khi gen. Trang đọc `lab6-data.js` nên mở trực tiếp từ filesystem được; bật lưới để xem HeightLevel và bật khoanh ramp để xem vector lên tầng cao. CSS thu nhỏ toàn ảnh để xem; sprite trong canvas và PNG luôn vẽ ở kích thước gốc.

## Quy trình hoàn chỉnh

1. Lab 5 tạo noise theo seed, lượng tử hóa HeightLevel, giữ núi và vùng căn cứ, bake collision cliff ở phía thấp. Chọn corridor, kiểm tra clearance và sửa connectivity. Candidate không hợp lệ bị loại; sau tám lần thử dùng contour theo block để tạo đoạn thẳng dài hơn. Tối đa 16 candidate, hết giới hạn trả lỗi. Đây là fallback chung theo config, không phải sửa riêng seed.
2. Lưu cliff face phía cao độc lập với collision phía thấp. Với mỗi mảnh, lưu ports N/E/S/W và mặt cao/thấp theo bốn quadrant NW/NE/SE/SW. Hai bitset có ý nghĩa khác nhau. Góc nối hai hướng dùng outer/inner corner theo mặt cao, không chỉ dựa vào góc quay.
3. Chọn ramp trên chính mặt cliff thẳng. Tầng thấp và cao phải kề nhau theo một hướng cardinal; ô góc hoặc face nhiều hướng bị loại. Gom các crossing cùng interface, nguồn ramp, hai HeightLevel và hướng thành một nhóm. Chốt hướng toàn nhóm trước khi chọn art.
4. Slope dài một ô theo hướng lên cao. Chiều rộng chạy theo tiếp tuyến: một ô dùng single; nhiều ô dùng left + n middle + right. Mặt đường không cỏ, chỉ hai mép ngoài có vai đá. Các nhóm độc lập cách nhau ít nhất một ô, xét cả support cao/thấp và khoảng cách Chebyshev.
5. Compiler kiểm tra ports và high/low cùng khớp một sprite/orientation. Ramp thay thế face đã mở, không vẽ cliff kín đè lên ramp. Connector trang trí không tự sinh collision hoặc thay đổi walk mask.
6. Render plan xếp ground → cliff → ramp. Draw có cell, layer, tên asset, rotation và metadata hướng. Vẽ native 418×418, chỉ quay 0/90/180/270°. Crop chỉ chọn vùng xem nhiều ramp/góc, không sửa map và không cắt thuật toán xuống 10×10.
7. Xuất `terrain.json`, `render_plan.json`, `sample_10x10.json`, `draw_manifest.json`, PNG thường và PNG khoanh ramp vào `Assets/Art/Terrain/Lab6`. Cùng seed/config/code/atlas cho cùng kết quả.

## Kiểm tra và giới hạn

`lab6-tests.cjs` kiểm tra tính lặp lại, compiler không sửa gameplay, từng orientation khớp high/low và ports, ramp cardinal/cao-thấp, chặn metadata sai, crop 100 ô. Các bài kiểm tra Lab 5 tiếp tục kiểm tra khoảng cách nhóm và connectivity.

Atlas V17 đã có 44 phép ghép mép pixel qua bốn hướng trong `Lab5TextureReview_v17/all_rotation_audit.json`. Phạm vi này chưa gồm mọi đầu cliff–ramp, ramp single hoặc tổ hợp cliff nhiều mảnh. Không coi 44 phép thử là chứng minh toàn bộ map liền texture.

Compiler xuất warning `compound_opaque_parts` cho face nhiều mảnh: preview hiện vẽ các tile nền kín theo thứ tự, có thể che nhau. Cần asset compound hoặc trim trong suốt trước khi dùng làm art cuối. Bốn ô của seed 30000 thuộc trường hợp này. Đầu cliff–ramp và sự lặp nền cỏ vẫn là việc art cần hoàn thiện; hướng/vị trí được kiểm tra riêng. Lab này chưa tích hợp renderer vào Unity, chưa chứng minh cân bằng cạnh tranh hoặc đạt chuẩn art cuối.

## Ánh xạ sang Unity

Đọc cell `(i % W, floor(i / W))`, giữ world Y=0 như Lab 5. Dùng HeightLevel cho metadata, physics đọc occupancy, renderer đọc render plan. Mỗi atlas tile có source rect cố định; quay quanh tâm cell theo rotation. Không suy hướng cao/thấp từ màu ảnh và không dùng sprite để quyết định walkability.



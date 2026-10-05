> **Quota ramp mới:** mỗi đoạn face cliff thẳng liên tục cùng hướng/cặp level, tách tại góc, có mục tiêu `max(1, ceil(n/12))` ô mặt ramp. Đếm chiều rộng mặt ramp, không đếm corridor/landing. Bổ sung sau repair và thống nhất núi; ưu tiên rộng cấu hình, cho phép thu đến 1 ô khi cần. Không khoét núi/góc hoặc phá khoảng cách nhóm ramp. Đoạn thiếu vị trí hợp lệ ghi `rampQuota.limited` với reason `geometry_or_ramp_spacing`, hiển thị số đoạn bị giới hạn trong HTML; không tuyên bố quota thay thế kiểm tra một island.
> **Thay đổi theo yêu cầu người dùng 2026-10-04:** bỏ clearance và unit-radius bake. Một mask `walk` dùng chung cho mọi unit; không còn field `physical` hoặc `clearance` trong output/schema. Núi, cliff chưa mở ramp và footprint resource chặn trực tiếp; HeightLevel không thay thế walkability. Cost Unity = walk ? 1 : 255. Các mô tả clearance/radius bên dưới là lịch sử và được thay thế bởi quyết định này. Retry, ramp cardinal/spacing và một island vẫn giữ. Radius input cũ bị bỏ qua, không ảnh hưởng generation.
# Lab 5 — terrain và navigation đã chọn

Bản hiện hành: [HTML Lab 5 + Lab 6](../Lab5_6/Lab5_6_Combined_Map_Texture.html), map 256×256, `fullMap:true`. [Thuật toán đã chốt](../../MapGen_Lab5_AcceptedAlgorithm.md) là nguồn quyết định; [Lab 6](../Lab6/Lab6_README.md) mô tả renderer và compiler.

Nguồn: `lab5-engine.js`, `lab5-bake.js`, `lab5-connectivity.js`, `lab5-visual-tiles.js`. Resource cluster riêng nằm trong `lab5-clusters.js`; preview kết hợp hiện tắt resource. HTML Lab 5 riêng và test của nó được giữ để kiểm tra resource và các chế độ bake.

Pipeline: seeded noise → integer HeightLevel → núi cố định/base core → cliff collision → ramp cardinal → clearance → nối mọi island → chỉ lấp pocket nhỏ không nối được → final walk. Đúng một island khi bật ramp; tối đa 16 candidate, có contour fallback từ candidate thứ chín. Núi không bị khoét. Y=0; navigation đọc walk/cost, không đọc sprite hoặc height-link.

Collision cliff nằm phía thấp, face texture nằm phía cao. `walk=false` không tự đồng nghĩa một island chưa lấp. Inspector HTML có physical, clearance và blockedReason.

Resource chỉ chặn footprint từng prefab; cluster cùng level, kiểm tra access sau toàn occupancy. Trữ lượng nằm trong prefab. Luật pocket ≤64 ô và tổng lấp ≤2% vẫn giữ; vùng lớn hoặc base không nối được phải reject.

Kiểm tra: `lab56-256-tests.cjs` cho preset hiện hành; `lab5-ramp-spacing-tests.cjs`, `lab5-cliff-direction-tests.cjs`, `lab5-visual-tile-tests.cjs` cho ramp/visual; các `lab5-*-tests.cjs` còn lại giữ regression terrain/resource/navigation. Preset 192×192 trong test lịch sử không thay kích thước bản hiện hành.

Lịch sử mô tả chi tiết và các thử nghiệm cũ: [archive](../../Archive/2026-10-04/README.md). Công cụ rebuild HTML hiện hành: `node Docs/Multiplayer/Labs/Lab5_6/build-lab56.cjs`.



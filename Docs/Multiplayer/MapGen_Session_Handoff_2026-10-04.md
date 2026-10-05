> **Thay đổi theo yêu cầu người dùng 2026-10-04:** bỏ clearance và unit-radius bake. Một mask `walk` dùng chung cho mọi unit; không còn field `physical` hoặc `clearance` trong output/schema. Núi, cliff chưa mở ramp và footprint resource chặn trực tiếp; HeightLevel không thay thế walkability. Cost Unity = walk ? 1 : 255. Các mô tả clearance/radius bên dưới là lịch sử và được thay thế bởi quyết định này. Retry, ramp cardinal/spacing và một island vẫn giữ. Radius input cũ bị bỏ qua, không ảnh hưởng generation.
# Bàn giao session — 2026-10-04

**Cập nhật sau bàn giao:** người dùng chọn thử đúng kích thước game **256×256** và yêu cầu bỏ outside terrain. HTML hiện gen với `fullMap:true`: nền tới hết mép grid, mountainMask vẫn có thể xuất hiện ở mép, không tự mở núi/cliff. Preset 192×192 bên dưới là lịch sử. Bản chốt và kế hoạch port đã cập nhật 256×256 fullMap; session sau dùng cấu hình này để gen núi. Seed 30000 / 4 người / radius 0.2 / width 3 kiểm tra đúng một island, không outside, không lỗi hướng visual; còn 2 warning art compound.

Session này đã xong. Người dùng xác nhận “ok tốt rồi chọn thuật toán như thế này ghi vào đã xong session sau là gen núi”. Thuật toán chuẩn là bản kết hợp [Lab 5 + Lab 6](Labs/Lab5_6/Lab5_6_Combined_Map_Texture.html), không phải các preview atlas cũ.

## Kết quả được chọn

- Lab 5 sinh map theo seed, HeightLevel, mountainMask, cliff collision, ramp, clearance và walk mask.
- Visual compiler lưu riêng hướng nối cliff và mặt cao/thấp; Lab 6 chọn orientation/asset và xuất render plan. Ramp cardinal, trên cliff thẳng, không ở góc; slope một ô, chiều rộng ghép head/middle/tail, nhóm khác nhau cách ít nhất một ô.
- HTML tự chứa engine và atlas V17, gen trực tiếp, xem full map/10×10, texture/HeightLevel/walk mask, inspect ô và thử route.
- Seed minh họa 30000, 192×192, 4 người chơi, radius 0.2. HTML hiện chọn rộng ramp cố định 3; CLI Lab 6 dùng min 3/max 7. Đây là hai config khác nhau, không so ảnh như cùng config. Resource tắt trong preview kết hợp.
- `lab6-tests.cjs`, `lab5-cliff-direction-tests.cjs`, `lab5-visual-tile-tests.cjs` và `lab56-tests.cjs` đã pass. Bài test HTML kiểm tra dependency browser-global và thuật toán nhúng, không phải test UI trình duyệt tự động. Người dùng đã mở HTML và chấp nhận kết quả.

## Bắt đầu session sau: gen núi

1. Đọc mountainMask/kind hiện có và xem núi màu xám trong full map. Giữ nguyên vị trí núi, HeightLevel/occupancy/navigation và thuật toán đã chọn.
2. Xây bộ tile núi có phần trong, mép và góc phù hợp mask, tiếp giáp cỏ/cliff; mặt đá và hướng cao/thấp phải rõ và thống nhất với atlas hiện có. Không quay lung tung hoặc kéo sprite để lấp hình.
3. Ghép các trường hợp mép/góc để kiểm tra liền mảnh trước, sau đó thử vùng có núi và full seed. Núi vẫn không đi được, ramp/resource không khoét núi.
4. Đưa texture núi vào HTML kết hợp để thay màu tạm. Người dùng duyệt art núi trước khi chốt bộ texture.

Không bắt đầu port Unity, đổi balance hoặc viết lại terrain khi chưa được yêu cầu. Yêu cầu “gen núi” là bước kế tiếp; chưa chốt thiết kế bộ núi cụ thể.

## Giới hạn còn giữ lại

Atlas V17 vẫn có lặp cỏ, khớp đầu cliff–ramp cần hoàn thiện, bốn ô cliff nhiều mảnh trong config CLI seed 30000 đang có warning vì tile nền kín có thể che nhau. Núi và nước chưa có art riêng, mới là màu tạm. Chốt thuật toán không phải nghiệm thu art cuối, cân bằng cạnh tranh hoặc tích hợp Unity.

## File và lệnh

Nguồn: `Labs/Lab5/lab5-engine.js`, `lab5-bake.js`, `lab5-connectivity.js`, `lab5-visual-tiles.js`, `lab6-engine.js`, `lab56-app.js`. Atlas: `Assets/Art/Terrain/Lab5TextureReview_v17/atlas.png`. Output CLI: `Assets/Art/Terrain/Lab6/`.

```powershell
node Docs/Multiplayer/Labs/Lab5_6/build-lab56.cjs
node Docs/Multiplayer/Labs/Lab5_6/lab56-tests.cjs
node Docs/Multiplayer/Labs/Lab6/lab6-generate.cjs 30000
pwsh -File Docs/Multiplayer/Labs/Lab6/render-lab6-full.ps1
```

Giữ các pack cũ để đối chiếu; không xóa texture hoặc thay thế atlas đang chọn mà không bảo tồn bản gốc.


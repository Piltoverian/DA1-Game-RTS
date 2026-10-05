# Kế hoạch port Lab 5 sang Unity — 2026-10-03

> Cập nhật 2026-10-04: kế hoạch port này được giữ cho giai đoạn sau. Người dùng đã chọn bản kết hợp Lab 5 + Lab 6 và yêu cầu **session tiếp theo gen núi**. Đọc [bàn giao mới nhất](../../MapGen_Session_Handoff_2026-10-04.md) trước khi tiếp tục; không tự bắt đầu port Unity.

## Trạng thái đã chốt

Người dùng đã chốt thuật toán Lab 5 và đánh giá generation tốt. Bước tiếp theo là port nguyên thuật toán sang Unity, giữ nguyên hành vi đã chọn. Không mở lại giai đoạn thiết kế, chọn policy hoặc cân bằng thuật toán.

Nguồn chuẩn: [thuật toán đã chốt](../../MapGen_Lab5_AcceptedAlgorithm.md), [Lab 5 README](../../Labs/Lab5/Lab5_README.md) và bốn module lab5-engine.js, lab5-bake.js, lab5-connectivity.js, lab5-clusters.js. Các mô tả cũ mâu thuẫn với Lab 5 đã được thay thế. Lần cập nhật này chỉ sửa docs, chưa port gameplay.

## Các luật giữ nguyên

- Common Y=0, HeightLevel nguyên; navigation chỉ đọc walk/cost cuối.
- Base/terrain bất đối xứng; 2–10 player, protectedRadius 4–16; dùng map 192×192 của lab để đối chiếu đầu tiên.
- Giữ núi gốc; thử nối mọi island qua cliff trước. Chỉ pocket không có base, <=64 ô và tổng <=2% được chuyển thành núi khi không nối được. Vùng lớn phải reject/retry; tối đa 16 terrain candidate.
- Khi bật ramp, đúng một island sau clearance và toàn bộ resource occupancy; ramp-off là diagnostic.
- Cluster cùng level; chỉ footprint từng prefab chặn ô; giữ access cuối của từng resource và bốn role main/secondary/advantage/contested. Trữ lượng thuộc prefab.
- Giữ hành vi quota, retry và báo thiếu của Lab 5 trong bản port đối chiếu; không tự thêm balance gate hoặc chọn candidate tốt nhất.

## Bước 1 — Port RNG/noise và dữ liệu map

Tạo config/result C# theo Lab 5: seed/attempt, bases, levels, mountain/cliff/ramp masks, terrainPhysical, resourceMask, clearance, finalWalk và placements.

Đối chiếu MapGenRNG.cs và các hàm Perlin/fBm trong MapGenerator.cs trước khi tái sử dụng. Giữ thứ tự gradient, shuffle, random stream, uint overflow, rounding và thứ tự duyệt của JS. Math.round JS khác rounding mặc định C# ở điểm nửa.

Kiểm tra: RNG/noise mẫu khớp lab; cùng seed/config cho kết quả lặp lại. Kiểm tra nhằm xác nhận bản port đúng, không xét lại thuật toán.

## Bước 2 — Port nguyên pipeline generation

1. Base/core và raw noise.
2. Mountain mask, integer levels, majority smoothing và core/collar phẳng.
3. Cliff band và ramp thông thường.
4. Clearance, walk mask, nối mọi island và pocket policy/retry.
5. Cluster, occupancy từng prefab và audit access trên map cuối.

Tách generator C# thuần khỏi MonoBehaviour/ECS để đối chiếu từng bước. Dùng seed 300, 30000, 1337, 2000 và các ca regression hiện có của lab. So levels/masks/placements và quyết định retry; giữ nguyên luật Lab 5.

## Bước 3 — Preview Unity

Tạo UI seed/config và view levels, mountain/cliff/ramp, final walk, resource placements, paths. Hiển thị attempt, placed/requested và lỗi theo lab. Preview và gameplay đọc cùng dữ liệu.

Preview texture tách data texture. Không dùng A×100 để sinh Amount; placements/spawn có danh sách riêng, không giới hạn spawn ID 0..3 của spec cũ. Đối chiếu cùng seed/config với lab; đổi view không đổi map.

Hash/report và dữ liệu mẫu có thể bổ sung trong lúc port khi cần, không là giai đoạn bắt buộc trước bước 1. Không dùng hash/accepted di sản engine để chứng nhận dữ liệu cluster cuối.

## Bước 4 — Nạp grid và spawn gameplay

- Nạp cost sau GridInitSystem, tăng generation; bảo toàn terrain cost khi BlockageGridBakeSystem cập nhật occupancy động.
- Authoring resource slot/prefab, TownHall và worker. Đối chiếu footprint prefab thực tế với 2×2 của lab; đổi unit radius world sang ô bằng radius/cellsize.
- Setup chạy đúng một lần và có kết quả xác nhận.
- Thêm IsFreeSetup cho TownHall: vẫn validate/reserve footprint, owner/population/blockage; bỏ worker/cost, tạo nhà completed/full HP.
- Building placement kiểm tra toàn footprint cùng HeightLevel và luật tránh cliff/ramp; cost ==1 hiện có chưa đủ xác nhận cùng level.
- Spawn worker ở ô hợp lệ; kiểm tra di chuyển và khai thác bằng gameplay thực tế.

Nghiệm thu tích hợp: không spawn trùng, terrain không mất sau tick/bake; unit qua ramp, không qua núi/cliff, tới được resource; lệnh xây thường giữ hành vi hiện có.

## Bước 5 — Hiển thị terrain/cliff/ramp

Gắn tile/texture giả cao thấp theo integer level và hướng cliff/ramp, vẫn cùng Y. Render đọc cùng masks với navigation. Kiểm tra Play Mode tại cấu hình đã chọn và các ca regression cần cho tích hợp.

Chỉ sửa sai lệch bản port hoặc lỗi tích hợp. Thay đổi thuật toán/cân bằng là công việc riêng khi người dùng yêu cầu.

## Công việc bắt đầu ngay

Bắt đầu bước 1 rồi port từng phần ở bước 2. Không yêu cầu refactor toàn Lab 5, final hash hoặc sweep balance trước khi port. Multiplayer Sprint 0/Relay/Lobby tiếp tục sau Hạng mục B theo lộ trình hiện hành.



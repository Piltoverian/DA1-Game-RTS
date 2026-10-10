# 08 — Giới hạn và chương trình nghiên cứu tiếp theo

Các hạng mục dưới đây là **đề xuất**, không phải tính năng đã có. Thứ tự ưu tiên đặt correctness và việc cô lập regression trước mở rộng hiệu ứng.

## 1. Che khuất terrain sprite và unit 3D

**Vấn đề:** terrain quad không ghi depth; scalar painter key không diễn tả mọi quan hệ giao nhau với đối tượng 3D. Có thể giữ hình chiếu đẹp nhưng unit phía sau cliff vẫn hiển thị sai.

**Phương án nghiên cứu:** so sánh pass depth proxy cho mặt ảo, tách lớp foreground hoặc graph thứ tự theo footprint. Mỗi phương án phải được đánh giá cùng stock material pipeline; không chọn trước một giải pháp chỉ vì demo đơn giản đẹp.

**Fixture:** unit đi quanh cliff nhiều tầng, ramp sát núi, building footprint nhiều ô, object cao, zoom 8/18/40. Có ảnh oracle/đánh dấu pixel cần bị che và cần còn thấy. **Chấp nhận:** không phá ramp/picking, không đổi collider/cost, không thêm collider chỉ để sửa art; chi phí CPU/GPU đo riêng. Budget định lượng cần thống nhất trước thử.

## 2. Hiệu năng và chênh lệch frame sample

**Vấn đề:** build candidate tăng khoảng 5,3% trong phép đo nhỏ; sample Editor sau sửa offset chậm hơn trước, nguyên nhân chưa cô lập.

**Thiết kế:** lưu baseline/candidate snapshot bất biến, cùng map/entity count/camera. Warm cả hai; ABBA nhiều lượt; đo main/render thread, job waits, GC allocation, peak memory, GPU time và median/p95/p99 ở standalone Development Build. Cô lập lần lượt artwork, helper transform, cache strategy và package state.

**Các tối ưu chỉ xét sau đo:** cache logical hierarchy theo change version, reuse buffer build, giảm face ẩn, prepack atlas hoặc spatial build. Phải giữ global order khi cắt batch/chunk; không thay sort toàn map bằng sort cục bộ mà không có test overlap.

**Chấp nhận:** baseline workload đã mô tả, metrics đáp ứng budget công bố, functional captures/hash/cost/transform gate không regress. Không lấy FPS Editor đơn lẻ để chứng minh tối ưu hoặc lỗi compiler.

## 3. Vòng đời và khả năng chẩn đoán

**Vấn đề:** exception offset ngày 2026-10-10 chưa có reproduction. Thuật toán cũ/lab đã được xóa theo yêu cầu; không khôi phục vào Main chỉ để thử giả thuyết.

**Thiết kế:** kiểm thử Play/Stop domain reload on/off, scene transition, create/destroy/reparent entity, disable/enable surface và script reload trong Play. Ghi phase/frame/world/entity cùng trạng thái cache khi exception xuất hiện; giữ stack gốc. Chạy phiên dài có churn thực và theo dõi kích thước cache/native allocation.

**Chấp nhận:** không accumulation offset, restore đúng trước physics, không leak root/mesh/atlas hoặc access entity chết. Nếu lỗi lặp lại, thu reproduction tối thiểu trước khi kết luận native memory corruption.

## 4. Material transition và lặp pattern

**Vấn đề:** hai bank 4×4 giảm đồng đều nhưng vẫn có chu kỳ, lựa chọn xác suất từng ô có thể làm lộ biên giữa bank.

**Thiết kế:** thử nhiều seed visual, bản đồ rộng ở zoom40, đo tần suất cạnh có tương phản cao và khảo sát khả năng nhận diện pattern. So sánh edge-compatible bank lớn hơn, Wang-style edge labels hoặc transition masks trong theme lab riêng.

**Chấp nhận:** không consume RNG gameplay, alpha/corner metrics không đổi, atlas không scale, build/memory còn trong budget. Không tuyên bố đã triển khai Blendomatic hoặc Wang tiles trước khi có code và evidence.

## 5. Hình núi và picking

**Vấn đề:** field khoảng cách tạo lõi plateau có tier clamp; tốt cho join nhưng chưa tạo hình núi nhiều cấu trúc. Raycast hiện theo terrain top, không theo toàn bộ rock silhouette/cap.

**Thiết kế:** thay đổi maxTiers/palette/pattern cap trên mountain mask cố định; thử field bất đẳng hướng hoặc ridge seed chỉ trong footprint, vẫn giữ boundary=0 và neighbor diff≤1. Đánh giá yêu cầu click: chọn cell logic hay hit đá nhìn thấy. Chỉ thêm picking proxy nếu tương tác sản phẩm cần nó.

**Chấp nhận:** không nở vào ramp/walkable, không đổi mountain blockage, góc ghép không nứt, unit scale/pivot giữ nhất quán. Kiểm tra hình thực tế; structural pass không chứng minh mountain silhouette đẹp.

## 6. Rebuild và nhiều camera/world

Adapter hiện build một lần và Active surface là static. Nếu map thay trong runtime, cần revision/hash và invalidation rõ, rebuild/clear có ownership. Nếu nhiều view/world, cần mapping surface theo world/camera và test disposal độc lập. Đây là mở rộng API; đổi docs hoặc thêm một singleton khác chưa đủ.

## 7. Hồ sơ cho mỗi nghiên cứu mới

Mỗi thử nghiệm cần câu hỏi, baseline, biến độc lập, điều giữ cố định, metric, criterion, môi trường, code/asset revision, report/capture và kết luận có giới hạn. Ghi thất bại và phương án bị loại khi chúng giải thích tradeoff. Chỉ đánh dấu “hiện hành” sau khi triển khai, kiểm tra lab và xác minh Main.

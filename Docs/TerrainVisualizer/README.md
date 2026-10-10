# Terrain Visualizer — bộ nghiên cứu và đặc tả

Đối chiếu mã và bằng chứng ngày **2026-10-10**. Đây là điểm vào riêng cho hệ thống hiển thị terrain. Quyết định sinh topology, chọn spawn, đặt resource và tính navigation thuộc [Map Generation](../MapGeneration/MapGen_INDEX.md).

## Tóm tắt nghiên cứu

**Artwork hiện hành:** JadeSlateTerrainTheme, tạo từ vật liệu ImageGen và UV mặt canonical. [Pipeline art](05_Atlas_ArtPipeline.md), [lab hiện hành](06_Lab_BangChung.md), [kết quả cập nhật và dọn dẹp](10_JadeSlate_Cleanup.md). Các bộ thử cũ đã nghỉ dùng; History chỉ giữ redirect tới archive phục hồi, không còn tài liệu chạy thuật toán cũ.

Visualizer dựng cảnh isometric từ dữ liệu grid đã có bằng PNG canonical và các quad nằm trên mặt phẳng Y=0. Cao độ logic được chuyển thành dịch chuyển hình chiếu và chọn silhouette; không dựng mesh địa hình nổi. Unit/resource dùng ma trận hiển thị có offset, còn transform simulation được giữ nguyên. Núi ghép được xây từ trường khoảng cách trên đỉnh chung, giới hạn trong footprint núi đầu vào.

Mục tiêu là giữ cạnh ghép, độ rõ của ramp và sự tách biệt với gameplay trong khi nâng chất lượng artwork. Renderer hiện có giới hạn ở che khuất giữa sprite terrain và đối tượng 3D; bằng chứng smoke test không đủ để kết luận ổn định phiên dài hoặc đạt hiệu năng sản phẩm.

## Lộ trình đọc

| Chương | Câu hỏi được trả lời |
|---|---|
| [01 — Phạm vi, hợp đồng và kiến trúc](01_PhamVi_KienTruc.md) | Visualizer nhận gì, tạo gì, sở hữu tài nguyên nào, giao tiếp với MapGen ra sao? |
| [02 — Phép chiếu, camera và painter order](02_PhepChieu_RenderOrder.md) | Vì sao quad Y=0 vẫn thể hiện cao độ? Công thức, pivot, batching và các trường hợp sort sai. |
| [03 — Thuật toán tile, ground, cliff và núi](03_ThuatToan_Tile_Nui.md) | Mask được tính thế nào? Vì sao đỉnh chung giữ cạnh núi khớp? Biến thể đất có tiêu thụ RNG gameplay không? |
| [04 — Surface, picking và vòng đời ECS](04_Surface_Picking_ECS.md) | Click lên sprite trả về điểm nào? Offset/restore làm gì? Lỗi đã biết và điều chưa chứng minh. |
| [05 — Hợp đồng art và bộ atlas](05_Atlas_ArtPipeline.md) | Gửi ảnh nào để gen? Kích thước, slot, import, pack và duyệt kết quả ra sao? |
| [06 — Phương pháp lab và bằng chứng](06_Lab_BangChung.md) | Thử nghiệm có thể chạy lại thế nào? Số liệu nào còn đúng, số nào chỉ là lịch sử? |
| [07 — Nguồn và lập luận nghiên cứu](07_Nguon_NghienCuu.md) | Nguồn nào hỗ trợ quyết định nào? Đâu là thuật toán dự án, đâu là tham khảo? |
| [08 — Giới hạn và chương trình nghiên cứu](08_GioiHan_HuongNghienCuu.md) | Những vấn đề nào chưa giải quyết? Thiết kế thí nghiệm và tiêu chí chấp nhận cho bước sau. |

Artist đọc 01 → 05 → 06. Lập trình renderer đọc 01 → 02 → 03 → 04 → 06. Người đánh giá nghiên cứu đọc 07 → 06 → 08, rồi đối chiếu thuật toán.

## Quy ước bằng chứng

- **Hiện hành:** được đối chiếu trực tiếp với mã hoặc asset hiện tại.
- **Đã đo:** có report/capture; chỉ áp dụng cho môi trường và workload đã ghi.
- **Suy luận:** kết luận từ công thức hoặc cấu trúc thuật toán; chưa thay thế test thực tế.
- **Đề xuất:** chưa triển khai hoặc chưa nghiệm thu.
- **Chưa xác định:** dữ liệu không đủ để kết luận nguyên nhân.

Các báo cáo trong `Artifacts` là nhật ký thí nghiệm, không tự động trở thành đặc tả hiện hành. Kết quả cũ không được dùng để phủ nhận lỗi mới.

## Tham khảo nhanh

- Bộ mẫu đầy đủ ZIP (đã loại khỏi repository), [trình xem offline](../../ArtSource/Terrain/TerrainReferenceKit/index.html), [tổng quan mẫu](../../ArtSource/Terrain/TerrainReferenceKit/00_Tong_quan.png).
- [TerrainTheme](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainTheme.cs), [renderer](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainChunkRenderer.cs), [sprite builder](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/CanonicalIsometricSprites.cs).
- [Điều tra lỗi offset](../../Artifacts/TerrainVisual/History/offset-cause-investigation.md).

Tách bộ docs không đổi namespace, đường dẫn code, scene hoặc kết quả map. Một số lớp adapter/theme/compiler còn nằm trong thư mục code `Simulation/MapGeneration`; đây là vị trí vật lý hiện tại, không phải quyền sở hữu logic của bộ tài liệu.

# 07 — Nguồn, mức tin cậy và lập luận nghiên cứu

Nguồn được kiểm tra ngày 2026-10-10. Chương này phân biệt tư liệu gốc, nghiên cứu phục dựng, tutorial của tác giả và suy luận từ mã dự án. Không có mã nguồn renderer gốc của AoE/StarCraft trong project; không gọi triển khai này là sao chép thuật toán chính thức của hai game.

## 1. Danh mục nguồn

| ID | Tài liệu / tác giả | Loại và phạm vi | Áp dụng vào dự án | Không suy ra |
|---|---|---|---|---|
| R1 | [Herb Marselas — Profiling, Data Analysis, Scalability, and Magic Numbers, Part II; Game Developer July 2000, trang in 36–43](https://media.gdcvault.com/GD_Mag_Archives/GDM_July_2000.pdf) | Bài kỹ thuật từ lập trình viên Ensemble, tư liệu lịch sử | Đo cùng workload, tách chi phí và xem scalability | Không mô tả đầy đủ renderer AoE, không cung cấp budget cho máy hiện tại |
| R2 | [openage — terrain research](https://github.com/SFTtech/openage/blob/master/doc/media/terrain.md) | Nghiên cứu reverse engineering do dự án openage công bố | Gợi ý nhiều frame/vật liệu và ánh xạ terrain | Không phải source/đặc tả Ensemble; renderer dự án không được chứng nhận tương đương |
| R3 | [Blizzard — Remastering StarCraft's Art](https://news.blizzard.com/en-us/article/20695698/remastering-starcraft-s-art) | Nguồn trực tiếp từ studio, art direction | Giữ nhận diện khi tăng chi tiết; làm nền cho câu hỏi readability | Không chứng minh thuật toán tile/sort/depth của StarCraft |
| R4 | [Blizzard — Behind the Scenes of StarCraft: Remastered](https://news.blizzard.com/en-us/article/20726732/behind-the-scenes-of-starcraft-remastered) | Tư liệu trực tiếp từ đội art | Đánh giá silhouette và nhận diện trong bối cảnh RTS | Không cung cấp tiêu chí pixel hoặc solver occlusion cho dự án |
| R5 | [Wenting Zhou — Heavenly Court](https://zhou2014.artstation.com/projects/RYnvnA) | Concept do nghệ sĩ công bố | Tham khảo tổ chức không gian, sắc độ và chủ đề tiên giới | Không coi ảnh là asset đã được phép đưa vào game, không coi đây là phong cách tu tiên duy nhất |
| R6 | [Amit Patel / Red Blob Games — BFS multiple start points](https://www.redblobgames.com/pathfinding/distance-to-any/) | Tutorial và thuật toán trực tiếp từ tác giả | Cơ sở distance field đa nguồn; lựa chọn 8-neighbor và clamp thuộc dự án | Không khẳng định field 8-neighbor là Euclidean; không nhập benchmark tutorial làm benchmark Unity |
| R7 | [Unity — Texture2D.PackTextures](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2D.PackTextures.html) | API chính thức Editor 6000.3 | Guard chống atlas pack làm scale tile | Không bảo đảm mọi backend/zoom không bleed chỉ nhờ padding |
| R8 | [Unity Entities — transform concepts](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/transforms-concepts.html) | Tài liệu chính thức; đối chiếu thêm package cài tại Library | Phân biệt LocalTransform/LocalToWorld và thời điểm dùng transform | Không đủ để xác định nguyên nhân exception của phiên riêng |
| R9 | [Unity Burst — changelog](https://docs.unity3d.com/Packages/com.unity.burst@1.8/changelog/CHANGELOG.html) | Lịch sử package chính thức | Ghi bối cảnh nâng 1.8.30 và thay compiler; kiểm soát biến thí nghiệm | Không chứng minh NullReferenceException hoặc crash project trùng một bug cụ thể trong changelog |

R2 được giữ như bối cảnh phục dựng có nhãn rõ; các kết luận kỹ thuật hiện hành được xác minh từ code dự án/API chính thức, không chỉ từ R2.

## 2. Lập luận thiết kế

**Nhận diện trước chi tiết.** R3/R4 hỗ trợ mục tiêu giữ identity; tiêu chí cụ thể “ramp rõ ở zoom18, nền không nuốt unit” là giả thuyết thiết kế của dự án và phải thử trên cảnh thật.

**Một metric cho camera và artwork.** Công thức projection, pivot và tier trong chương 02 là suy luận và hợp đồng của mã canonical. Không gán chúng cho StarCraft hoặc AoE chỉ vì hình thức nhìn tương tự.

**Đỉnh chung thay cao độ độc lập từng cell.** R6 cung cấp nguyên lý lan distance đa nguồn. Project dùng vertex lattice để hai ô dùng cùng level tại cạnh, chọn 8-neighbor và max tier. Lập luận giữ neighbor diff≤1 được kiểm tra bằng structural assertions; đẹp hay không vẫn cần capture.

**Tách chi phí build và frame.** R1 làm cơ sở tư duy đo workload. Phép đo 356,6/375,6 ms và sample Editor 14/22 ms là số riêng của dự án, không phải kết quả từ bài Ensemble.

**Không suy ra geometry từ ảnh.** Bộ art chứa shading/silhouette; runtime chỉ tạo quad. Lệnh gameplay dùng grid/surface logic. Nhận xét này được đối chiếu với builder, picker và ECS code, không dựa vào nguồn mỹ thuật.

## 3. Quy tắc trích dẫn và cập nhật

Gắn nguồn cạnh nhận định mà nó hỗ trợ. Dùng diễn giải ngắn; không chép toàn bộ bài hoặc tải artwork của tác giả vào runtime. Với thuật toán đề xuất mới, ghi pseudocode và chứng minh/invariant riêng, rồi nguồn tham khảo. Với bug, yêu cầu reproduction/log/version trước khi nhận một issue ngoài project là nguyên nhân.

Mỗi lần thay camera/schema/compiler hoặc package, cập nhật ngày đối chiếu, code liên quan và [bằng chứng](06_Lab_BangChung.md). Thay đổi art direction cần nói rõ phần nào là lựa chọn của đội, tránh biến tham khảo thẩm mỹ thành yêu cầu kỹ thuật không có căn cứ.
# Nguồn art bổ sung — Jade Slate, 2026-10-10

Các trang chính thức: [Amazing Cultivation Simulator](https://store.steampowered.com/app/955900/Amazing_Cultivation_Simulator/), [Tale of Immortal](https://store.steampowered.com/app/1468810/Tale_of_Immortal/), [The Matchless Kungfu](https://store.steampowered.com/app/1696440/The_Matchless_Kungfu/). Chúng hỗ trợ bối cảnh cultivation/wuxia của hướng art; không chứng minh thuật toán isometric hay mask trong dự án. Một số ảnh chính thức không fetch được, nên không tuyên bố phân tích đầy đủ screenshot. Palette xanh dịu, mặt đá phẳng và giảm contrast ground là quyết định của dự án; artwork mới là ImageGen gốc, không copy screenshot/game asset.


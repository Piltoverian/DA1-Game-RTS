# Chuyển tối ưu Ramp Algorithm bản thử nghiệm vào Unity

Bản triển khai ngày 2026-10-11. Unity tiếp tục dùng RNG/permutation và mã nguồn shared-đỉnh C#; không thay độ cao bộ sinh dữ liệu bằng trường sóng JS. Layout sau cập nhật có thể khác cùng hạt giống vì chính sách spawn/ramp đã thay đổi.

## Luồng xử lý khi chạy mới

```text
các đỉnh dùng chung → làm dịu (chênh lệch ≤ 1, loại saddle)
→ mức tầng theo đa số góc
→ chọn vị trí sinh thông minh có giới hạn trong cung riêng → nền cơ sở trung vị → lõi tròn
→ đường bao + ràng buộc vành chuyển tiếp
→ khoảng trống bố cục / cụm núi
→ giới hạn bước cao độ → kiểm tra lại vành base
→ bake vách/góc → kết nối/hạn ngạch dốc bằng heap
→ sửa vùng nhỏ chạm lõi tối đa một lần và bake/ramp lại
→ lấp vùng nhỏ thường trong tổng ngân sách 2% → kiểm tra hợp lệ
→ TerrainReady → navigation → bootstrap nhà/resource bằng ECB → GameplayReady
→ biên dịch/dựng/ảnh vật liệu phía hiển thị → camera cục bộ sau khi renderer/khởi tạo Ready
```

## Các thay đổi đã chuyển

Cập nhật orchestration ngày 2026-10-12: [luồng xử lý ECS](EcsMapPipeline.md) thay sinh dữ liệu trong bộ chuyển dữ liệu sang ECS bằng recipe/runtime sinh dữ liệu; mô hình căn lưới consume tag, camera hệ thống riêng, khởi tạo ECB và bộ dựng hình theo phiên bản. Các tối ưu kết nối lưới bên dưới vẫn được giữ. [đo hiệu năng hiện hành](../Maintenance/VerificationHistory.md) phân biệt chi phí sinh dữ liệu, bộ lập kế hoạch và bản dựng hình ảnh; chuyển điều phối sang ECS chưa chuyển toàn bộ thuật toán sang Burst.

| Thay đổi | mã |
|---|---|
| Tối đa 9 tâm có chủ đích trong cung riêng, trung vị từ độ cao lượng tử hóa, đường bao hộp giới hạn | `TerrainTopologyOptimizer.PrepareBases` |
| Core tròn `distance < floor(protectedRadius)+1`; chuyển tiếp theo cliffCells, không xóa vách core | `ProtectBaseGeometry`, `TerrainGeneration.Validate` |
| chuyển dữ liệu sang ECS chỉ số trực tiếp, east/south, nhánh diff×cliffCells=1, góc trong giữ nguyên | `TerrainTopologyOptimizer.Bake` |
| Labels/queue mảng tái dùng theo từng lần Generate; public nhãn có bản riêng | `Labels`, `SharedVertexTerrainGeneration.Generate`, `ComponentLabels` |
| Enumerate lane một lần; gom dải cùng profile độ cao; hai binary heap; union-find; xung đột tách lane | `OpenRamps` |
| hạn ngạch vùng vách cùng level/4 cạnh; ô dốc nối tầng lớn trước; cho phép vượt hạn ngạch nguyên cửa | `OpenRamps` |
| Pocket chạm core về độ cao cơ sở, sửa kết nối lưới một lần | `RepairProtectedPockets`, `TryFinishTerraces` |
| Influence riêng cho sinh dữ liệu đặt nhà | `GridTerrain.RampInfluence`, `MapResourcePlanner`, `PlayerSpawnPlacement.TryBuilding` |
| Render dùng hướng của độ dốc phía cao; tách chuỗi theo mức | `TerrainVisualCompiler.Compile` |
| Bỏ mảng 4 góc cấp phát cho từng ô khi relaxation/convert | `SharedVertexTerrainGeneration.Generate` |

Scratch thuộc invocation/map, không dùng pool tĩnh toàn cục. Các lần thử lại của cùng invocation dùng chung ba mảng, nên không có nguy cơ hai bộ sinh dữ liệu chạy đồng thời ghi đè nhãn. đầu ra của `ComponentLabels` được clone để caller có thể giữ ổn định; mã nội bộ dùng nhãn tái dùng.

## Settings và tương thích dữ liệu

Inspector có **maximumRampCells**, mặc định12, là tổng ô **độ dốc** thật trong một cửa, bao gồm chiều rộng và số tầng chuỗi. Fixture cấu hình có giá trị12 rõ ràng. `rampWidth` cũ được giữ ẩn để đọc tài nguyên cũ và dùng kích thước khối của phương án dự phòng cũ; không còn giới hạn width của cửa mới.

`RampId` vẫn đánh dấu cả hành lang vách đã mở để các bên xử lý biết ô vách đó được đi. `RampDirection >= 0` chỉ đánh dấu độ dốc có chênh +1 từ phía sau; đoạn xuyên vách dày cùng mức có hướng=-1. Những ô phẳng này không tính quota/max độ dốc, không vẽ ảnh sprite độ dốc. Đây là cách giữ khả năng vượt cliffCells2/3 của Unity mà bản thử nghiệm JS còn thiếu. Renderer tách cùng cổng theo mức để chuỗi nhiều tầng không ghép thành một dải ảnh sprite ngang sai.

Ảnh hưởng của mỗi độ dốc là hai ô cạnh theo hướng: thấp phía sau, cao phía trước; có thể là độ dốc khác. Không chừa blanket 3 ×3 cho các cửa. tài nguyên vẫn giữ margin núi phục vụ artwork, nhưng kiểm tra ramp/influence trên vùng chiếm chỗ thực. Nhà khởi tạo giữ vòng circulation của vùng chiếm chỗ và không chặn influence. lối chơi xây nhà không dùng flag này như lệnh cấm; không đổi nút chi phí vì influence.

Giữ quy tắc góc an toàn: độ dốc phía cao phải có một hướng thấp duy nhất, phù hợp hình ảnh trình biên dịch và điều hướng hiện tại. hạn ngạch thiếu không gây không đạt; sinh đối tượng không liên thông hoặc pocket vượt ngân sách vẫn gây không đạt. Giới hạn an toàn16 bản thử và khối-trung vị phương án dự phòng từ candidate9 vẫn còn, không tuyên bố mọi cấu hình đều có lời giải hay loại hoàn toàn thử lại.

## Kiểm tra

Đã bổ sung [đo hiệu năng từng bước 256/512](../Maintenance/VerificationHistory.md) với cùng cấu hình và 200 hạt giống mỗi kích thước. Lượt Editor nographics đó đo512² median903.81 ms, p951059.14 ms, 200/200 đạt bản thử đầu; độ cao nhiễu và núi chiếm82.8% tổng thời gian đo. Số đo này là lượt riêng với dữ liệu đo, không thay số liệu integration phía dưới.

Lượt xác nhận cuối: **400/400 bản đồ đạt**, không khi chạy exception. 64² có197/200 hạt giống đạt bản thử đầu, tối đa2, median15.78 ms/p9523.17 ms; 256² cấu hình dự án có199/200 hạt giống đạt bản thử đầu, tối đa2, median246.57 ms/p95285.77 ms. Cấu hình hai nhóm khác nhau ngoài kích thước (players/base/mountain), không dùng bảng này như phép đo scaling thuần.

Integration terrain512, tài nguyên và chuẩn renderer/picking/Clear-dựng lại đều đạt. chụp đã được kiểm tra trực quan. Bộ lính bổ sung kiểm tra trực tiếp sửa một protected pocket cô lập và chuyển dữ liệu sang ECS lại về một vùng đi được.

Entry point: `MapGenerationOptimizationValidation.RunIntegration` (Unity batchmode có graphics).

- 108 hình học dữ liệu kiểm thử ngẫu nhiên: bộ chuyển dữ liệu sang ECS tối ưu khớp bộ chuyển dữ liệu sang ECS tham chiếu độc lập, nhiều kích thước, núi, viền, cliffCells1/2/3.
- Base diff5/depth1/2/3: core phẳng, không vách; vách bên ngoài còn nguyên.
- Ramp qua vách dày1/2/3, giới hạn độ dốc/cửa, direct chain0→1→2 và hình ảnh trình biên dịch.
- nhãn caller sở hữu và hạt giống determinism.
- Small-vùng liên thông suite: giữ sinh đối tượng, ngân sách, metadata và atomic từ chối.
- 200 hạt giống cố định × hai config64/256: kiểm tra hợp lệ mới, cung riêng, hình ảnh trình biên dịch; results.csv ghi từng seed/attempt/thời gian.
- Integration mountain512, tài nguyên bộ lập kế hoạch bốn hướng influence, địa hình renderer/picking, hiển thị không sửa ECS địa hình, Clear/rebuild.

Kết quả và nhật ký của lượt xác nhận cuối: summary.txt (nguồn lịch sử hiện không còn trên đĩa), results.csv (nguồn lịch sử hiện không còn trên đĩa), Unity nhật ký (nguồn lịch sử hiện không còn trên đĩa). chụp bộ dựng hình: terrain.png (nguồn lịch sử hiện không còn trên đĩa).

Thời gian bộ kiểm thử là thời gian Generate, fresh validation/visual biên dịch nằm ngoài timer. Không phải đo hiệu năng theo cặp với bản Unity trước; không suy ra phần trăm tăng tốc từ số đo JS. Chưa chuyển sang ECS Jobs/Burst: các hướng đó trong tài liệu là đề xuất tiếp theo, chưa phải tối ưu đã đo hiệu năng và được chuyển ở đây.

Tham khảo hình minh họa và quy tắc chi tiết ở [PipelineGuide.md](UnityOptimizationPort.md). Tài liệu đó mô tả bản thử nghiệm JS; phần tương thích Unity trong tệp này là chuẩn để đọc khi chạy sau chuyển.

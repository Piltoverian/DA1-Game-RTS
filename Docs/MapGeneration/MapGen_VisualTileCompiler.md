# Mặt ramp, khoảng cách và kiểm tra ghép texture

## Luật hiện hành — 2026-10-03

Người dùng đã cho phép sửa Lab 5 để bảo đảm vị trí, hướng và khoảng cách giữa ramp. Noise, HeightLevel, núi gốc, resource occupancy và điều kiện cuối một island vẫn giữ policy đã chốt. Các mô tả cũ về start/middle/end theo chiều dốc hoặc vector hướng trung bình đã được thay thế.

### Xác định ramp trước khi chọn sprite

1. Tìm cạnh nối ô thấp đã mở physical với ô cao đi được ở N/E/S/W. Ô chứa sprite ramp là **ô cao** của cạnh này, thay trực tiếp mặt cliff phía cao; lưu lowCell/highCell để không nhầm ô tiếp cận với mặt dốc.
2. Chọn một hướng thấp → cao cardinal cho mỗi ô mặt cliff. Các cạnh lên cao phụ là thông tin góc; không tạo ramp chồng lên ô đó hoặc cộng hướng thành vector chéo.
3. Nhóm các ô cùng nguồn cửa mở, cùng hai mức và cùng hướng trên một đường cliff liên tiếp. Tính width từ số ô trong cụm, theo phương ngang với hướng đi.
4. Chốt hướng chung trước khi gán sprite. Vector chỉ được [0,-1], [1,0], [0,1], [-1,0].
5. Width 1 dùng single. Width lớn hơn dùng left + các center + right theo phía trái/phải của hướng đi. Phần dốc chỉ một ô theo chiều đi.
6. Mọi ramp, kể cả single và middle, chỉ nằm trên face một mảnh thẳng. Không có ramp ở góc; góc dùng cliff/connector riêng, không đổi hướng ramp để bám góc.

`rampCells` và `ramps[].cells` là dữ liệu hành lang/cửa mở, có thể bao gồm ground landing. Renderer dùng `rampFaceGroups`/`rampTileMap`, không vẽ mọi ô rampCells thành dốc.

### Khoảng cách giữa ramp

Hai cụm mặt ramp riêng biệt phải có khoảng cách Chebyshev giữa các ô mặt gần nhất ít nhất 2: luôn có một ô chen giữa, kể cả chạm chéo. Các ô trong cùng một ramp rộng được phép kề nhau.

`lab5-bake.js` xuất rampFaces, rampGapViolations, enforceRampGap. Sau bake, giữ cụm theo thứ tự xác định, ưu tiên cụm rộng hơn, đóng toàn bộ cụm mặt xung đột; không cắt lỗ giữa một cụm. rampFaceDirection giữ hướng đã chọn.

`lab5-connectivity.js` kiểm tra footprint đường sửa kết nối với vùng đệm một ô quanh các mặt ramp hiện có. Được đi qua cửa đã mở, nhưng không mở thêm cliff trong vùng đệm. Sau mở cửa mới, chuẩn hóa khoảng cách và tính lại clearance/components. Nếu không nối được island do cửa bị loại, rollback physical, rampCells, direction và danh sách ramp; loại tâm ứng viên gây lỗi, thử đường khác, tối đa 24 lần trước khi reject candidate. Mountain mask được kiểm tra lại ở mỗi lần tìm đường, kể cả núi mới từ pocket đã lấp.

`lab5-engine.js` audit khoảng cách sau remap và xuất rampGapCells:1, rampFaceGroups, rampFaceDirection. Không hạ điều kiện một island hoặc đổi độ rộng cấu hình để đáp ứng spacing.

### Ô cliff bắt buộc thuộc tầng cao

`cliffFaceMap` được suy từ HeightLevel, không từ dải `cliffMask` thấp. Mỗi face có ownerSide=high, level=highLevel và ít nhất một mẫu thấp hơn ở cạnh hoặc đỉnh chung. Ô trong lòng plateau không có sprite cliff. Ô chỉ có chênh cao ở đỉnh là connector góc trong, không thêm ô chặn vật lý và không lấp landing của ramp. `open` chỉ đúng khi chính face đó có ramp thay thế; không dùng physical=1 để xóa mọi cliff ở tầng cao.

`cliffMask` là dải collision thấp của Lab 5, không phải bản đồ vị trí texture. Hai dữ liệu này có mục đích khác nhau. Khoảng cách ramp kiểm tra cả lowCells và highCells; chuyển sprite sang cao không được làm hai cửa riêng chạm nhau.

### Chọn góc và vẽ

Mask theo NW/NE/SE/SW: 1/2/4/8 là ngoài; 7/11/13/14 là trong; 3/6/9/12 là thẳng; 5/10 là góc tách; 0/15 là fill. Không kéo sprite theo bounding box hoặc viết nhánh cho seed cụ thể.

V3 có corner ngoài chuẩn NW-high, corner trong chuẩn NW-low. Corner trong V3: mask 14 là rotation 0, 13 là 90°, 11 là 180°, 7 là 270°. Không dùng bảng xoay corner ngoài cho corner trong.

Renderer mẫu vẽ ba lượt đầy đủ: ground → cliff → ramp. Mỗi ô ramp chỉ một sprite. Asset nhập cùng kích thước vuông cho preview, giữ tỷ lệ; sau nhập dùng DrawImageUnscaled. Bước thu ảnh dùng TileFlipXY tại biên để tránh viền alpha giả, không chỉnh màu hoặc blend để che đường nối.

## Kiểm tra texture V3

`lab5-texture-join-cases.cjs` liệt kê toàn bộ cặp E/S và mọi cụm 2×2 của seed 30000, deduplicate theo khóa texture/hướng nhưng giữ count và tọa độ đầu tiên. Bao phủ 73.344 cạnh và 36.481 cửa sổ 2×2. Thêm mẫu width 1–7, cliff–ramp–cliff, góc trong/ngoài, bốn hướng để kiểm tra asset chưa xuất hiện trong seed.

Kết quả: 204 cặp duy nhất + 438 cụm 2×2 duy nhất + 76 mẫu thử = 718 ảnh; 46 contact sheets và gallery HTML. Mẫu thử có tọa độ -1,-1, không được trình bày như terrain đã sinh.

`render-lab5-texture-join-audit.ps1` ghép trực tiếp asset, xuất meanEdgeRgbJump để sắp thứ tự xem. Đây là dấu hiệu độ lệch màu, không phải chứng nhận liền mạch. Các biến thể thiếu fill/endcap được ghi trong key khi dùng cliff thẳng thay thế. Texture núi lấy từ V2 vì V3 chưa có asset núi.

**Kết luận: V3 chưa khớp.** Cliff góc không cùng điểm nối với cliff thẳng; mép ramp không cùng profile cliff; đất các mảnh lệch tone; thiếu fill/endcap. Chưa nghiệm thu atlas và chưa tích hợp renderer này vào Unity.

## Validation

- lab5-ramp-spacing-tests.cjs: 3 cảnh tổng hợp và 24 map (4 seed × 2/4/10 players × radius 0,2/0,65), kiểm tra trực tiếp khoảng cách, hướng cardinal, một island và núi không mở.
- lab5-visual-tile-tests.cjs: hướng, width role, góc, input immutability và determinism.
- Existing connectivity, island policy, mountain, player, mask consistency và resource occupancy regressions chạy lại sau thay đổi.

Output: Assets/_Project/Art/Design/Terrain/Lab5TextureReview_v3/seam-audit/ gồm cases.json, results.json, index.html, pages/, cases/, ramp-spacing-audit.json và join_overview.png.


### Chọn ô nối theo hướng mặt cliff

Compiler lấy lowEdges (N/E/S/W là hướng nhìn từ ô cao xuống ô thấp). Một hướng tạo strip thẳng, cổng nối nằm trên hai cạnh tiếp tuyến. Hai hướng vuông góc tạo góc ngoài, cổng nối ngược hai normal. Khi chỉ có ô thấp ở đường chéo, chỉ thêm connector góc trong nếu cả hai ô cạnh trung gian ở cùng tầng cao; cổng nối hướng đến hai ô cạnh đó. Không lấp vật lý hoặc đổi HeightLevel.

Mỗi face xuất parts gồm shape, cornerMask, ports và rotation. Giao nhau phức hợp xuất nhiều part, không ép về cliff thẳng rotation=0. Renderer dùng parts trực tiếp. Ramp middle góc dùng spriteRotation của corner; hướng đi cardinal vẫn lưu riêng ở direction. Texture V3/V4 vẫn chưa đạt ghép mép, sửa orientation không xác nhận chất lượng atlas.

Kiểm tra: lab5-cliff-direction-tests.cjs kiểm 12 hướng thẳng/góc và 7 kiểu giao phức hợp; lab5-visual-tile-tests.cjs kiểm các seed thật và ownership phía cao.



# IntegrationField: hiểu ngăn hàng đợi bằng ví dụ

Cập nhật 2026-10-12. `IntegrationFieldSystem` đã chuyển phần lan truyền từ FIFO sang Dijkstra dùng Dial ngăn hàng đợi. Queue nằm trong `IntegrationBucketQueue.cs`. Chưa chạy Unity/Burst sau thay đổi này.

Đã thêm [GridNeighborMaskSystem](../../Grid/GridNeighborMaskSystem.md) trước Integration. Nó tính mặt nạ hướng hợp lệ dùng chung, bộ nhớ đệm theo topologyVersion. Propagation đọc mặt nạ thay vì kiểm tra lại biên, ô chặn và góc chéo cho từng trường. Cost/seed/bucket giữ nguyên; Direction chưa dùng mặt nạ.

## IntegrationField tính gì?

Mỗi ô có một số `bestcost`: chi phí lan từ hạt giống tới ô đó. hạt giống có chi phí 0; ô chưa tới được có `int.MaxValue`. Unit dùng kết quả này để tạo hướng di chuyển. Công thức hiện tại giữ nguyên:

`cost mới = bestcost ô đang xét + GridNodeCost ô hàng xóm + 10 hoặc 14`

10 là bước ngang/dọc, 14 là bước chéo. Ô chi phí >=255 bị chặn. Đi chéo bị cấm nếu một trong hai ô cạnh bị chặn. Vì có cộng chi phí ô, đất chi phí 1 tạo bước 11/15, không phải 10/14.

## Vì sao FIFO có thể làm thừa việc?

FIFO xử lý theo thứ tự đến trước. Giả sử ô A được phát hiện với chi phí 80, rồi ô B với chi phí 20. FIFO có thể xử lý A trước và lan chi phí 80 sang nhiều ô khác. Sau đó đường qua B cải thiện A xuống 35, khiến các ô liên quan cần cập nhật lại.

FIFO cũ vẫn có thể ra đúng kết quả vì tiếp tục cập nhật tới khi hết hàng đợi. Vấn đề là nó có thể làm lại nhiều lần trên bản đồ có chi phí khác nhau.

Các hạt giống đều được thêm vào cùng hàng đợi trước khi lan; không chạy hết một hạt giống rồi mới bắt đầu hạt giống kế. Mỗi lần bestcost hàng xóm giảm, ô đó được thêm vào hàng đợi lại, kể cả khi đã xử lý hoặc đã có mục đang chờ. FIFO cũ lấy bestcost mới nhất khi lấy khỏi hàng đợi và không bỏ mục trùng/cũ. ngăn giữ quy tắc cập nhật chi phí nhưng thay thứ tự xử lý và bỏ mục chi phí cũ.

## Ngăn là gì?

Hãy tưởng tượng nhiều khay có nhãn chi phí: khay 0, khay 1, khay 2... Một mục có chi phí 20 đi vào khay 20; mục có chi phí 80 đi vào khay 80.

Một con trỏ tìm khay có nhãn nhỏ nhất còn việc. Vì vậy chi phí 20 được xử lý trước chi phí 80, dù mục chi phí 80 được thêm trước. Bên trong cùng một khay, các mục vẫn xếp FIFO. Không cần sắp xếp toàn bộ hàng đợi như heap.

Ví dụ:

1. hạt giống ở khay 0.
2. Từ hạt giống, tới ô đất chi phí 1 bằng bước ngang: đưa ô đó vào khay 11.
3. Tới ô đất chi phí 1 bằng bước chéo: đưa vào khay 15.
4. Xử lý khay 11 trước khay 15; từ khay 11 có thể thêm mục vào khay 22, 26...
5. Lặp tới khi không còn mục nào.

Đây là Dijkstra với cách tổ chức hàng đợi bằng khay, không phải BFS coi mọi bước có cùng giá.

## Vì sao chỉ cần 269 khay?

Cost ô đi được tối đa là 254. Bước chéo tối đa 14, nên một cạnh thêm tối đa 268. Khi đang xử lý chi phí C, mục mới chỉ có chi phí trong khoảng C đến C+268, nếu chi phí cạnh không âm.

269 nhãn liên tiếp đủ cho khoảng này. Dùng khay vòng: `số khay = cost % 269`. Cost 0 và 269 cùng dùng khay số 0, nhưng ở hai vòng khác nhau. Con trỏ chi phí chỉ tiến về phía trước; khay chi phí 0 đã được lấy hết trước khi khay đó dùng cho chi phí 269. Không cấp một khay cho mọi khoảng cách có thể xuất hiện trên cả bản đồ.

Điều kiện bắt buộc: chi phí cạnh nguyên, không âm, tối đa 268; tất cả hạt giống được thêm vào hàng đợi chi phí 0 trước khi lan truyền. Nếu sau này tăng chi phí bước hoặc cho phép chi phí ô >=255 vẫn đi được thì phải cập nhật giới hạn. Cost ô âm không thuộc hợp đồng được hỗ trợ.

## Một ô đổi bestcost thì làm gì?

Giả sử đã thêm vào hàng đợi `(A, 80)`, sau đó tìm được đường tốt hơn và thêm vào hàng đợi `(A, 35)`. Mục 80 vẫn còn trong hàng đợi. Khi lấy nó ra, so chi phí đã lưu với bestcost hiện tại:

- 80 khác 35: mục cũ, bỏ qua ngay.
- 35 bằng 35: mục hiện tại, xét hàng xóm.

Nhờ vậy mục cũ không lan lại. Khi chi phí bằng nhau, không thêm vào hàng đợi thêm vì chỉ cập nhật khi `newcost < bestcost`.

## Bộ nhớ của bản khi chạy

Hai mảng 269 số giữ đầu/cuối của từng khay. Các mục nằm trong một `NativeList`, mỗi mục gồm nút, chi phí lúc thêm vào hàng đợi và liên kết tới mục sau. Khi lấy một mục ra, vị trí của nó được đưa vào danh sách trống để lần thêm vào hàng đợi sau dùng lại. Vì vậy số vị trí tăng theo độ dài hàng đợi lớn nhất trong trường, không tăng mãi theo tổng số lần thêm vào hàng đợi. Queue được tạo trong tác vụ bằng `Allocator.Temp` và giải phóng sau khi trường tính xong.

## Những hành vi giữ nguyên

Tối ưu tiếp sau phản hồi đột biến thời gian khoảng 20 ms: GridIslandSystem lưu islandCount và islandTopologyVersion. Nếu có đúng một vùng liên thông, kết nối lưới của dữ liệu vùng liên thông còn hiện hành và targetcell đi được, chọn ngay targetcell làm hạt giống vì khoảng cách bình phương bằng 0. Không quét toàn bộ đệm tìm hạt giống trong trường hợp đó. Đích blocked, nhiều vùng liên thông, thiếu metadata hoặc kết nối lưới vừa đổi vẫn dùng lượt quét cũ. Đây không phải bỏ tính chi phí trên một phần bản đồ; propagation vẫn tính toàn vùng reachable.

IntegrationSeedUtility đã kiểm tra độc lập với 1000 mục tiêu: 995 nhánh nhanh, 5 blocked đi phương án dự phòng, không khác hạt giống của lượt quét cũ; 8 ca điều kiện chặn không an toàn bị từ chối. [kết quả đã lưu](../../../../../Maintenance/VerificationHistory.md). Không suy ra thời gian toàn Integration từ thời gian tìm hạt giống.

Profiler có marker trên thợ: Integration.Reset (đặt lại bộ đệm), Integration.Seeds (cấp queue/scratch, chọn và nạp hạt giống), Integration.Propagate (lan chi phí và giải phóng hàng đợi). Duration nằm trong tác vụ Integration, không cộng với duration parent. Scheduling/main-luồng chờ và Direction nằm ngoài marker này. Cần xem thợ threads, không chỉ chính luồng.

Reset toàn bộ đệm, cách chọn hạt giống, hạt giống quanh nhà, các đảo, công thức chi phí, cấm cắt góc và chuyển trạng thái `CalculatingDirection` đều giữ nguyên. Không thay đổi scheduling hay tự chia một trường qua nhiều bước mô phỏng. `IJobChunk` vẫn chia theo khối; trường trong cùng khối vẫn được xử lý lần lượt.

hạt giống đại diện mỗi vùng liên thông hiện được chọn theo khoảng cách bình phương tới `targetcell`. Nếu đích ở nhà, thêm các ô đi được trên viền ngoài bounding box nhà và các hạt giống vùng liên thông hiện có. Không gọi đây là hạt giống theo khoảng cách tới hình chữ nhật vì mã hiện không làm vậy.

Lỗi cũ vẫn tồn tại: mảng hạt giống có `MaxIslands=10000`, nhưng vòng khởi tạo và vòng nạp hạt giống chỉ chạy tới 1000. Thay hàng đợi không sửa lỗi vùng liên thông ID >=1000.

## Kết quả và giới hạn

Thử nghiệm ban đầu trên 1000 trường 512 ×512, 3 lần lặp, cùng vòng duyệt: ngăn nhanh hơn FIFO khoảng 3,4–4,2 lần ở bản đồ trọng số cao, nhưng chậm hơn trên bản đồ trống và hành lang chi phí 1. Xem [kết quả đã lưu](../../../../../Maintenance/VerificationHistory.md).

Bản hàng đợi khi chạy có cách lưu trữ khác bản thử nghiệm ban đầu. Bộ kiểm tra độc lập biên dịch trực tiếp `IntegrationBucketQueue.cs` với adapter được quản lý cho NativeArray/NativeList, đối chiếu từng ô với FIFO. Kết quả ở [kết quả đã lưu](../../../../../Maintenance/VerificationHistory.md).

Bản khi chạy đã hoàn tất 1000 trường × 3 lần lặp: ngăn khớp toàn bộ bestcost với FIFO, 0 ô sai lệch. Trung bình trên ca trọng số: FIFO 33,05 ms, ngăn 10,03 ms; trọng số + vật cản: 45,46 ms so với 11,61 ms. bản đồ trống: FIFO 5,30 ms, ngăn 6,77 ms. Các số này là được quản lý C# với adapter, không phải Burst hoặc thời gian khung hình trò chơi.

Test này kiểm tra xử lý logic hàng đợi trên bản đồ tổng hợp và hạt giống cung cấp sẵn. Không kiểm chứng Unity NativeContainer safety, Burst, chọn hạt giống sản xuất, Direction, nhiều trường chạy đồng thời hay thời gian khung hình Main. Chưa kết luận đã sửa đột biến thời gian 823 ms. Khi được chạy Unity, cần compile/Burst, kiểm tra hồi quy chuyển động và bộ đo hiệu năng để xác nhận hiệu quả thực tế.

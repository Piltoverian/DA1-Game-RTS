# GridNeighborMaskSystem: tính hướng được phép trước Integration

Cập nhật 2026-10-12. hệ thống chạy trong FixedStep, sau GridInit/CostChange/GridIsland và trước IntegrationFieldSystem.

## Mặt nạ lưu gì?

Mỗi ô có một byte, gồm 8 bit theo thứ tự Bắc, Đông, Nam, Tây, Đông Nam, Tây Bắc, Tây Nam, Đông Bắc. Bit bật nghĩa là bước sang hàng xóm đó không ra biên, không vào ô blocked và không cắt góc chéo. Ô blocked có mặt nạ 0.

Ví dụ Đông bị tường chặn thì bit Đông tắt; hai hướng chéo qua cạnh Đông cũng bị tắt. mặt nạ không lưu bestcost, không chọn đường tới đích và không xét bán kính lính. Mọi trường cùng lưới đọc chung mặt nạ vì chúng dùng cùng luật vật cản.

## Khi nào tính lại?

- Lần đầu sau lưới initialized, hoặc kích thước lưới đổi.
- Khi GridComponent.topologyVersion đổi. GridInit tăng version lúc tạo costs; CostChange tăng ngay khi một ô chuyển giữa walkable/blocked; BuildingPlacement tăng khi giữ chỗ vùng chiếm chỗ.
- Không chờ lưới.generation/heartbeat 12 bước mô phỏng. Thay chi phí giữa hai giá trị vẫn đi được không đổi mặt nạ; Integration vẫn đọc chi phí hiện tại để tính trọng số.

Bản đầu tiên này dựng lại toàn mặt nạ một lần khi kết nối lưới đổi bằng IJobParallelFor, không cập nhật riêng từng ô. Các trường dùng chung kết quả. Khi không có thay đổi, không quét lại costs. lưới 512 ×512 dùng 256 KiB mặt nạ, ngoài metadata/bộ đệm overhead.

Nếu thêm mã ghi trực tiếp GridNodeCost, phải tăng topologyVersion khi walkability đổi; chỉ đặt isDirty không đủ. Helper `PlayerSpawnPlacement.Reserve` hiện sửa bản costs tạm để lập kế hoạch; đường publication vật chặn thật vẫn qua CostChange.

## Dependency và vòng đời

GridAuthoring chuyển dữ liệu sang ECS bộ đệm GridNeighborMask và GridNeighborMaskState. Với lưới tạo thủ công, hệ thống bổ sung chúng ở lần đầu; bước bổ sung thành phần có Complete, không xảy ra mỗi bước mô phỏng.

Job đọc costs, ghi mặt nạ bộ đệm. State ghi version của tác vụ đã schedule; Built không có nghĩa tác vụ đã xong trên CPU tại thời điểm schedule. Integration đọc mặt nạ bộ đệm theo phụ thuộc nên chỉ dùng sau tác vụ ghi hoàn tất; đồng thời kiểm tra Built/version/kích thước. ECS quản lý bộ nhớ bộ đệm theo thực thể, không có bộ nhớ đệm native persistent riêng cần giải phóng.

## Integration dùng mặt nạ thế nào?

Lấy mặt nạ ô hiện tại, tìm bit bật đầu tiên bằng math.tzcnt, tính chỉ số hàng xóm bằng độ lệch, rồi tính chi phí như trước. Tắt bit vừa xét và lặp tới khi hết bit. Bỏ kiểm tra biên/blocker/góc chéo trong vòng propagation; kiểm tra đó đã được thực hiện lúc tạo mặt nạ.

ngăn hàng đợi, hạt giống và trạng thái trường giữ nguyên. Direction vẫn dùng các kiểm tra cũ; chưa chuyển Direction sang mặt nạ trong thay đổi này.

## Kiểm chứng

[kết quả đã lưu](../../../../Maintenance/VerificationHistory.md) biên dịch trực tiếp GridNeighborMaskUtility và IntegrationBucketQueue với adapter được quản lý, so từng bestcost với FIFO. Có thêm 40 ca đóng/mở ô, gồm góc bản đồ, so mặt nạ sản xuất với phép kiểm tra độc lập.

Test độc lập không xác nhận Unity/Burst, ECS phụ thuộc hoặc thời gian khung hình. Test thứ tự chạy TerrainHeightValidation đã thêm GridNeighborMaskSystem trước Integration; chưa chạy Unity trong lượt này.

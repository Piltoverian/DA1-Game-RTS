# Thiết lập dữ liệu trò chơi

**Chọn GameDataRegistry trên Inspector và bấm Validate — khoảng 2 phút để bắt đầu.**

## Tạo hoặc sửa dữ liệu

1. Tạo BasedSO và UnitSO/BuildingSO; UnitSO/BuildingSO tham chiếu Base, không kế thừa Base. Base giữ mẫu đối tượng, MaxHP,WorkRate,JobCapacity và tham chiếu Ability/Job.
2. Tạo khả năng/tác vụ thuộc kiểu đã có. Train tham chiếu OutputUnit; Research tham chiếu TechDefinition. WorkLoad nằm trên Job, chi phí nghiên cứu nằm trên TechDefinition.
3. Đăng ký Unit/Building/Ability/Job/Civ trong danh mục dữ liệu của cảnh. Base được duyệt qua UnitSO/BuildingSO; danh mục công nghệ đến từ TechTree của Civ.
4. Bấm **Assign Missing IDs**, lưu tài nguyên. ID hiện có được giữ; Base ID chỉ cấp bằng nút này. tham chiếu được thêm bên trong Base/TechTree cũng cần cấp ID.
5. Bấm **Validate** hoặc `Tools → Game Data → Validate Selected Registry`. Sửa lỗi rồi validate lại trước bake/Play.

## Validate làm gì?

Kiểm tra ID,tham chiếu bắt buộc,quan hệ với danh mục,kiểu,số,chi phí và điều kiện tiên quyết công nghệ. Registry Editor gộp chi phí trùng ResourceType, lưu thay đổi trên SO hợp lệ và chạy bộ kiểm tra hợp lệ chung; dữ liệu sai không được chuẩn hóa thành một giá hợp lệ. bộ chuyển dữ liệu sang ECS chỉ sao chép chi phí đã chuẩn hóa.

Chi phí rỗng hợp lệ; số lượng âm/NaN/Infinity,giá trị enum tài nguyên sai hoặc tổng vượt float.MaxValue bị từ chối. Có blob trong danh mục dữ liệu không tự cấp quyền bên sản xuất: Base phải có lựa chọn được cung cấp phù hợp. Không tự đổi ID theo tên hiển thị hoặc thứ tự danh sách.

## Mẫu đối tượng và hiển thị

[Entity thiết lập dữ liệu](EntityAuthoring.md) mô tả UnitAuthoring/BuildingAuthoring,chủ sở hữu,HP và construction progress. [Registry hiển thị](RegistryPresentation.md) mô tả duy nhất từ Awake và cách tra UI trên tài nguyên; không có phương án dự phòng tự tải tài nguyên.

## Khi cần thêm kiểu mới

[DesignerTypeManual](DesignerTypeManual.md) có quy trình SO → blob → bộ chuyển dữ liệu sang ECS → bộ kiểm tra hợp lệ → tra cứu. Tạo cấu hình không tự tạo hành vi lối chơi. [Blob tham chiếu](BlobDefinitions.md) là API khi chạy hiện hành.

bộ kiểm tra hợp lệ ở `Scripts/Runtime/GameData/Validation`; Editor UI ở `Scripts/Editor/GameData`. bộ kiểm thử lối chơi đã xóa theo yêu cầu, nhưng nút/menu Validate dữ liệu vẫn giữ. GUI Undo và lượt SubScene chuyển dữ liệu sang ECS độc lập cần nghiệm thu riêng; lượt dọn tài liệu không chạy chúng.

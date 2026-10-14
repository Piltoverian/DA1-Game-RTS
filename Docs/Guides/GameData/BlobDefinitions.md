# Định nghĩa blob và danh mục dữ liệu — hiện hành

**Mở phần Lookup dưới đây để đọc cấu hình khi chạy — khoảng 3 phút.**

## Lookup nhị phân và progression tham chiếu

`RegistryBlobElement` giữ `TypeKey`, `ID` và untyped blob tham chiếu. bộ chuyển dữ liệu sang ECS dùng `RegistryBlobElement.Create<T>(blob)` để lấy ID từ đúng kiểu ngay lúc chuyển dữ liệu sang ECS; tra cứu không đoán layout của blob để đọc ID.

RegistryAuthoring gom mục rồi sắp xếp ổn định theo `(TypeKey, ID)` một lần lúc chuyển dữ liệu sang ECS. `RegistryBlobElement.CompareTo` dùng cùng thứ tự cho baking và tìm kiếm nhị phân cận dưới. `RegistryPrefabElement` không bị sắp xếp, giữ nguyên mọi PrefabIndex. Một tra cứu nhị phân được dùng bởi cả ba API:

```csharp
var typed = registry.GetBlobByID<TrainBlob>(id, out var result);
var untyped = registry.GetBlobByID(RegistryTypeKey.For<TrainBlob>(), id, out result);
var resolved = registry.GetBlobByID(gameBlobReference, out result);
```

Hai overload không generic trả `UnsafeUntypedBlobAssetReference`; chỉ reinterpret sau khi tra cứu thành công và đã xác định đúng kiểu khóa. Wrapper generic tính mã băm khóa và gọi lại tra cứu gốc. Với khóa duy nhất, tra cứu O(nhật ký n); nếu có khóa trùng, chỉ duyệt nhóm trùng để giữ tham chiếu hợp lệ đầu tiên. Registry vẫn không có native bản đồ hoặc hệ thống khởi tạo/cleanup riêng; Unity sở hữu blob lifetime. Fixture hoặc bên cung cấp tạo bộ đệm thủ công phải sắp xếp trước tra cứu; không sắp xếp hoặc kiểm tra toàn bộ đệm ở mỗi lần tra khi chạy.

`CommandQueueElement.Progression` dùng `GameBlobReference { CatalogKey, ID }` có sẵn, thay `DataIndex`. Phát lệnh với `progression: offer.JobReference`, hoặc tạo tham chiếu từ `RegistryTypeKey.For<TrainBlob/ResearchBlob>()` và JobID. `ProductionJobs.Enqueue` phân giải tham chiếu và xác nhận bên sản xuất có lựa chọn được cung cấp cùng kiểu+ID trước thanh toán. Vì vậy đổi thứ tự lựa chọn được cung cấp không đổi tác vụ đang chờ, còn lựa chọn được cung cấp bị gỡ sẽ bị từ chối. `ProductionElement.UnitPrefab` vẫn lấy từ lựa chọn được cung cấp hợp lệ của bên sản xuất.

Schema bộ đệm danh mục dữ liệu đã đổi; cần chuyển dữ liệu sang ECS lại SubScene khi chạy sau biên dịch. Không mang raw blob pointer trong lệnh hoặc giải phóng blob từ hàng đợi.


## Các nhóm blob

| tệp dưới GameData/Baking/Blobs | Nội dung |
|---|---|
| BaseBlob.cs | ID,MaxHP,WorkRate,JobCapacity,PrefabIndex; BlobArray tham chiếu Ability/Job |
| UnitBlob.cs / BuildingBlob.cs | Base inline; dân số,chi phí,type/tag và khối lượng công việc phù hợp loại |
| JobBlob.cs | Job ID/WorkLoad; TrainBlob có OutputUnitID; ResearchBlob có Tech inline |
| TechBlob.cs / CivBlob.cs | Tech/tree/điều kiện tiên quyết và civilization/unlocks |
| AbiilityBlob.cs | Các khả năng cấu hình được RegistryAuthoring chuyển dữ liệu sang ECS |

Tên `AbiilityBlob.cs` giữ theo mã nguồn hiện có. Job identity lấy Job.ID; Unit/Building identity lấy Base.ID. UI icon/name thuộc ScriptableObject hiển thị, không nhét vào lối chơi blob.

## Đọc an toàn và lifetime

1. Lấy bộ đệm danh mục dữ liệu của scene/world hiện tại; tra cứu đúng kiểu+ID và kiểm tra FunctionResult.
2. Đọc root/nested struct chứa BlobArray qua `ref`; không sao chép struct rồi đọc relative offsets.
3. Không Dispose blob mượn từ danh mục dữ liệu hoặc gửi raw pointer/Entity handle qua mạng.

BlobBuilder,AddBlobAsset và DependsOn thuộc baking luồng xử lý; Unity quản lý lifetime. PrefabIndex dùng RegistryPrefabElement của đúng danh mục dữ liệu, không phải biểu tượng chỉ số. Không sắp xếp mẫu đối tượng liên kết để sắp xếp danh mục blob.

[Entity thiết lập dữ liệu](EntityAuthoring.md) · [Registry hiển thị](RegistryPresentation.md) · [Trạng thái/bằng chứng](../../Maintenance/CurrentState.md)

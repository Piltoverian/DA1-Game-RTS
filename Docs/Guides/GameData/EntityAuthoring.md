# Unit và Building — component/authoring mới

Thiết kế ngày 2026-09-22 theo yêu cầu xây lại độc lập với lối chơi cũ. Các hướng dẫn trước về adapter sang thành phần legacy không áp dụng cho Unit/Building mới.

## Thành phần

| thành phần | Dữ liệu |
| --- | --- |
| `UnitComponent` | `DefinitionID`, tra `UnitBlob` |
| `BuildingComponent` | `DefinitionID`, tra `BuildingBlob` |
| `EntityOwner` | `PlayerID` của instance |
| `EntityHealth` | `CurrentHP` của instance |
| `BuildingConstruction` | `CompletedWork` và `Phase` của công trình |

cấu hình chỉ nằm trong blob do `RegistryAuthoring` đã chuyển dữ liệu sang ECS: MaxHP, WorkRate, JobCapacity, tham chiếu Ability/Job, dân số, costs, UnitTypes, BuildingTags và WorkLoad. Entity không chép lại cấu hình, không chứa SO, tên UI hoặc biểu tượng. Không tạo blob riêng hay map/runtime Registry hệ thống mới.

## Authoring

- Unit: gắn `UnitAuthoring`, chọn `unitDef`, đặt `playerID`.
- Building: gắn `BuildingAuthoring`, chọn `buildingDef`, đặt `playerID`. Không cần UnitAuthoring; không được gắn cả hai trên cùng GameObject.
- `initialHealth = -1` khởi tạo full MaxHP; giá trị từ 0 đến MaxHP dùng làm HP ban đầu.
- Building có `initialConstructionProgress` từ 0 đến 1: 0 là Planned, giữa 0 và 1 là UnderConstruction, 1 là Completed. `CompletedWork = progress * WorkLoad`.
- `EntityDefinitionBaker<T>` dùng chung cho owner/HP và theo dõi BasedSO bằng `DependsOn`; mỗi bộ chuyển dữ liệu sang ECS theo dõi UnitSO/BuildingSO của nó.
- Đăng ký definition và các Ability/Job được tham chiếu trong GameDataRegistry của cảnh; cấu hình chuyển dữ liệu sang ECS mode để bản ghi tương ứng có mặt. Dùng kiểm tra hợp lệ Registry hiện có. Entity bộ chuyển dữ liệu sang ECS không tự đăng ký hoặc tạo bản ghi còn thiếu.

## Đọc dữ liệu ở hệ thống mới

```csharp
var unit = entityManager.GetComponentData<UnitComponent>(entity);
var registry = entityManager.GetBuffer<RegistryBlobElement>(registryEntity, true);
var definition = registry.GetBlobByID<UnitBlob>(unit.DefinitionID, out var result);
if (result != FunctionResult.Success)
    return;

ref var config = ref definition.Value;
float maxHP = config.Base.MaxHP;
int populationCost = config.PopulationCost;
```

Building dùng cùng tra cứu với `BuildingComponent.DefinitionID` và `BuildingBlob`. tham chiếu Ability/Job đọc từ `config.Base`; giữ blob theo `ref`, không Dispose tham chiếu do Registry sở hữu. `registryEntity` là thực thể Registry đang được cảnh cung cấp.

## Phạm vi

Authoring mới không tạo thành phần cũ, không ánh xạ nhiều BuildingTags về BuildingType, không gọi HealthAuthoring/HouseAuthoring và không chuyển dữ liệu sang ECS TargetCache. Các khai báo kiểu mà hệ thống cũ vẫn tham chiếu được đặt riêng trong `CoreECS/LegacyEntityComponents.cs` để mã nguồn còn biên dịch; chúng không tham gia đường chuyển dữ liệu sang ECS mới.

Chưa chuyển scene/prefab hoặc viết lại các hệ thống lối chơi, UI, khả năng execution và tác vụ hàng đợi. mẫu đối tượng dùng đường mới phải được cấu hình theo phần Authoring; hệ thống cũ không tự xử lý thực thể mới. Không tự tạo mutable trạng thái cho khả năng/tác vụ chưa có bên xử lý mới.

Kiểm chứng: biên dịch khi chạy và Editor với Unity references/source generators thành công. Chưa kiểm chứng Unity baking/Play Mode.


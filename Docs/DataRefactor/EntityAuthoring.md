# Unit và Building — component/authoring mới

Thiết kế ngày 2026-09-22 theo yêu cầu xây lại độc lập với gameplay cũ. Các hướng dẫn trước về adapter sang component legacy không áp dụng cho Unit/Building mới.

## Component

| Component | Dữ liệu |
| --- | --- |
| `UnitComponent` | `DefinitionID`, tra `UnitBlob` |
| `BuildingComponent` | `DefinitionID`, tra `BuildingBlob` |
| `EntityOwner` | `PlayerID` của instance |
| `EntityHealth` | `CurrentHP` của instance |
| `BuildingConstruction` | `CompletedWork` và `Phase` của công trình |

Config chỉ nằm trong blob do `RegistryAuthoring` đã bake: MaxHP, WorkRate, JobCapacity, Ability/Job references, population, costs, UnitTypes, BuildingTags và WorkLoad. Entity không chép lại config, không chứa SO, tên UI hoặc icon. Không tạo blob riêng hay map/runtime Registry system mới.

## Authoring

- Unit: gắn `UnitAuthoring`, chọn `unitDef`, đặt `playerID`.
- Building: gắn `BuildingAuthoring`, chọn `buildingDef`, đặt `playerID`. Không cần UnitAuthoring; không được gắn cả hai trên cùng GameObject.
- `initialHealth = -1` khởi tạo full MaxHP; giá trị từ 0 đến MaxHP dùng làm HP ban đầu.
- Building có `initialConstructionProgress` từ 0 đến 1: 0 là Planned, giữa 0 và 1 là UnderConstruction, 1 là Completed. `CompletedWork = progress * WorkLoad`.
- `EntityDefinitionBaker<T>` dùng chung cho owner/HP và theo dõi BasedSO bằng `DependsOn`; mỗi baker theo dõi UnitSO/BuildingSO của nó.
- Đăng ký definition và các Ability/Job được tham chiếu trong GameDataRegistry của scene; cấu hình bake mode để record tương ứng có mặt. Dùng validation Registry hiện có. Entity baker không tự đăng ký hoặc tạo record còn thiếu.

## Đọc data ở system mới

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

Building dùng cùng lookup với `BuildingComponent.DefinitionID` và `BuildingBlob`. Ability/Job references đọc từ `config.Base`; giữ blob theo `ref`, không Dispose reference do Registry sở hữu. `registryEntity` là entity Registry đang được scene cung cấp.

## Phạm vi

Authoring mới không tạo component cũ, không ánh xạ nhiều BuildingTags về BuildingType, không gọi HealthAuthoring/HouseAuthoring và không bake TargetCache. Các khai báo type mà system cũ vẫn tham chiếu được đặt riêng trong `CoreECS/LegacyEntityComponents.cs` để source còn compile; chúng không tham gia đường bake mới.

Chưa chuyển scene/prefab hoặc viết lại các system gameplay, UI, ability execution và job queue. Prefab dùng đường mới phải được cấu hình theo phần Authoring; system cũ không tự xử lý entity mới. Không tự tạo mutable state cho ability/job chưa có consumer mới.

Kiểm chứng: compile runtime và Editor với Unity references/source generators thành công. Chưa kiểm chứng Unity baking/Play Mode.

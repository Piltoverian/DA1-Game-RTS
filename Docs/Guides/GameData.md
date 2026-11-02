# GameData: cấu hình, baking và lookup

## Hai dạng dữ liệu

```text
SO + prefab + registry trong Inspector
    → Validate/IDs → RegistryAuthoring Baker → blob/config + registry buffers trong World
    → simulation lookup theo type key + ID

DefinitionID/JobID → EntityPresentation → GameDataRegistry SO → icon/name/preview
```

[BasedSO.cs](../../Assets/_Project/Scripts/Runtime/GameData/DefinitionScript/BasedSO.cs), UnitSO/BuildingSO, AbilityDefinition/Job/CivDef là cấu hình. UnitSO/BuildingSO tham chiếu BasedSO; không kế thừa nó. Base giữ prefab, MaxHP, WorkRate, JobCapacity và danh sách ability/job. Tạo một asset preset mới không cần tạo subtype mới.

## Sửa dữ liệu hiện có

1. Tạo/sửa Base, Unit/Building, ability/job và Civ/TechTree liên quan.
2. Đăng ký trong [GameDataRegistry.cs](../../Assets/_Project/Scripts/Runtime/GameData/DefinitionScript/GameDataRegistry.cs). Base dùng ability/job thì vừa phải tham chiếu trong Base vừa đăng ký catalog; tham chiếu không tự mở rộng catalog.
3. Bấm **Assign Missing IDs**, lưu asset. Giữ ID đã cấp; không đổi theo display name/thứ tự list. Asset duplicate có thể giữ ID trùng; công cụ không tự sửa ID trùng. Base ID cấp qua nút này.
4. Bấm **Validate** hoặc `Tools → Game Data → Validate Selected Registry`; xử lý lỗi rồi rebake SubScene/Play.

[GameDataRegistryEditor.cs](../../Assets/_Project/Scripts/Editor/GameData/Registry/GameDataRegistryEditor.cs) gọi [GameDataValidator.cs](../../Assets/_Project/Scripts/Runtime/GameData/Validation/GameDataValidator.cs): ID/reference/type/số/chi phí/prerequisite. Cost rỗng hợp lệ; âm/NaN/Infinity, enum sai, tổng overflow bị từ chối. Registry Editor normalize ResourceType trùng và lưu SO hợp lệ; Baker chỉ sao chép dữ liệu đã chuẩn hóa. Một Base chỉ chứa một ability mỗi concrete subtype; nhiều vũ khí thuộc danh sách Weapons của Attack.

## Authoring và prefab

[UnitAuthoring.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Authoring/Unit/UnitAuthoring.cs) và [BuildingAuthoring.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Authoring/Building/BuildingAuthoring.cs) bake config/identity/capability. EntityOwner là owner, EntityHealth giữ HP, EntityVision giữ vision; UnitComponent/BuildingComponent giữ đặc trưng tương ứng. Enableable work components biểu diễn trạng thái thực thi, không phải danh sách mọi khả năng.

Registry DefaultWorker/DefaultTownHall là fallback cho Civ thiếu StartWorker/TownHallPrefab; hai lựa chọn độc lập. Prefab civ có nhưng sai không được âm thầm thay bằng default. Bootstrap kiểm tra component/health/footprint/movement; worker phải unlocked không cần tech. Thiếu prefab cần dùng thì Failed trước instantiate; WorkerCount=0 không cần worker prefab. Thay default không tự đổi train/build/unlock của civ.

Main hiện tham chiếu [GameReg.asset](../../Assets/_Project/Resources/GameReg.asset); các definition/prefab Tests/Fixtures/TwoCivDogfood được game dùng trực tiếp, không phải test artifact. MapGenConfig chỉ tham chiếu bootstrap settings, không giữ worker/townhall preset song song.

## Registry lookup và blob lifetime

Nguồn: [RegistryAuthoring.cs](../../Assets/_Project/Scripts/Runtime/GameData/Authoring/RegistryAuthoring.cs), [GameDataRegistryComponent.cs](../../Assets/_Project/Scripts/Runtime/GameData/Component/GameDataRegistryComponent.cs), [EntityDefinitionBaker.cs](../../Assets/_Project/Scripts/Runtime/GameData/Baking/EntityDefinitionBaker.cs) và [Blobs](../../Assets/_Project/Scripts/Runtime/GameData/Baking/Blobs).

- RegistryBlobElement chứa TypeKey, ID và untyped blob reference. Baker sắp xếp ổn định theo (TypeKey, ID); lookup dùng cùng comparator và lower-bound binary search.
- Create<T> lấy ID từ đúng blob type; không đọc ID bằng đoán layout. Generic GetBlobByID<T>, overload type-key/ID và GameBlobReference cùng dùng lookup gốc. Fixture/manual buffer phải sorted trước lookup, không sort mỗi lần đọc.
- RegistryPrefabElement giữ nguyên thứ tự; không sort prefab để sort blob. PrefabIndex chỉ có nghĩa với registry cùng World.
- GameBlobReference là CatalogKey + ID, không phải index trong offer hoặc raw pointer. Producer phải đối chiếu offer phù hợp trước sử dụng.
- Đọc struct root/nested có BlobArray qua `ref`, không copy rồi dereference relative offsets. Chỉ reinterpret untyped blob sau lookup thành công và xác định đúng TypeKey.
- Unity sở hữu blob được Baker/AddBlobAsset đăng ký. Không Dispose blob mượn từ registry, không serialize raw pointer/Entity handle local ra mạng.

## Registry cho UI

RegistryAuthoring.Instance trả **GameDataRegistry asset**. Getter không tìm GameObject, không tự Resources.Load và không đọc ECS registry. Source hiện gán static ở Baker.Bake, còn ResetInstance xóa tại SubsystemRegistration. Vì vậy cần kiểm chứng Play không rebake, Domain Reload bật/tắt và player build; không mặc định static luôn được publish lại. Khi chưa có asset, EntityPresentation trả null hoặc ID làm tên dự phòng.

## Thêm kiểu dữ liệu mới

1. Thêm SO subtype và unmanaged blob schema, dùng ID kế thừa hiện có.
2. Thêm mapping trong RegistryAuthoring **BakeBase** (tham chiếu type key/ID) và **BakeAbilities/BakeJobs** (copy payload); không chỉ làm một phía.
3. Thêm validation trong [DefinitionValidator.cs](../../Assets/_Project/Scripts/Runtime/GameData/Validation/Definitions/DefinitionValidator.cs); Inspector Min/Range không thay validation.
4. Thêm capability/runtime state và system handler nếu cần hành vi. Config SO tự nó không triển khai gameplay; không đăng ký bằng enum/factory mới khi lookup type-key đã đủ.
5. Kiểm tra asset → validate → bake → lookup → hành vi, gồm thiếu registry, ID trùng, thiếu prefab và rebake.

Không có Heal được cài chỉ vì tài liệu cũ từng dùng nó làm ví dụ. Thanh toán, offer và quyền nằm ở [Gameplay](../Architecture/Gameplay.md); lịch sử PASS không thay nghiệm thu GUI Undo/Validate hoặc SubScene baking riêng.

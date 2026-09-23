# Blob definitions — 2026-09-21

## Hiện hành 2026-09-22 — chỉ dùng blob đã bake, không có Registry system

Theo yêu cầu người dùng, đã bỏ native map, RegistryCatalogMapSystem và RegistryMapCleanup. RegistryBlobElement giữ các blob references đã bake; RegistryBlobLookup.GetBlobByID<T> tra trực tiếp buffer theo type + ID (duyệt tuyến tính, không cấp phát). BlobBuilder/AddBlobAsset, BlobArray và DependsOn giữ nguyên. Unity quản lý buffers và lifetime blob theo scene; không có map Persistent để cleanup.

Lấy Registry bằng `entityManager.GetBuffer<RegistryBlobElement>(registryEntity, true)`, sau đó gọi `registry.GetBlobByID<T>(id, out result)` và đọc `ref var value = ref reference.Value`. Không cần chờ system khởi tạo map; chỉ cần Registry entity/buffer đã được load. Bộ kiểm tra blob riêng vẫn đã bỏ, không tạo lại. Bước kế tiếp là entity bakers/bindings. Nội dung lịch sử nói dùng map/system bên dưới đã được thay thế.

## Lịch sử trước khi bỏ system

## Hiện hành — BlobAsset + map type/ID (2026-09-21)

Theo yêu cầu mới: giữ các hàm bake theo loại SO và map generic; mỗi record là một BlobAssetReference<T>, lists lồng nhau dùng BlobArray. Baker dựng bằng BlobBuilder, đăng ký AddBlobAsset, không gọi validator. DependsOn Registry và các SO con thực sự đọc; dependency của Registry không tự thay thế dependency của SO con.

RegistryBlobElement là một buffer chung chứa type key + untyped blob reference để Unity serialize/remap. RegistryCatalogMapSystem dựng map runtime và là bên duy nhất dispose map khi Registry entity mất source hoặc system/world bị hủy. Unity sở hữu lifetime blob; system không dispose blob. Không còn CustomList/NativeList hoặc Dispose đệ quy trong blob structs.

Lookup giữ GetBlobByID<T>(id, out result), nay trả BlobAssetReference<T>; đọc root/nested arrays qua ref. Type key dùng BurstRuntime.GetHashCode64<T>(), không dùng TypeManager cho blob structs và không cần enum liệt kê loại. AddBlobToReg<T> nhận blob hoàn chỉnh và mượn reference, không nhận T by-value để tạo blob.

Base.PrefabIndex và WeaponDefinitionBlob.ProjectilePrefabIndex tra RegistryPrefabElement trên cùng Registry entity; Entity không nằm trong blob. Đã copy Research.Cost, Build.Range, ProjectileSpeed, Unit/Building ability/job references, Civ/tree/nested prerequisites. Tech identity theo source người dùng: TechDefinition.IDs.

Runtime và Editor Roslyn compile với Unity references/source generators đã pass ở lượt trước. Ngày 2026-09-22, theo yêu cầu người dùng, đã bỏ công cụ kiểm tra blob riêng và không yêu cầu chạy lại hoặc tạo lại bộ checks này. Chưa có evidence actual Unity baking/rebake/PlayMode; không coi việc bỏ checks là tests đã pass. Evidence compile lịch sử: Temp/RegistryFixCompile. Các snapshots bên dưới không thay thế mục hiện hành này.

## Snapshot trước — không thay thế trạng thái trên

Theo yêu cầu hiện tại: chỉ chuyển schema SO sang structs dùng trong BlobAsset và thêm GetID, lấy AbiilityBlob.cs làm mẫu. Không khôi phục các baker/helper/map system cũ đã bị người dùng thay đổi hoặc xóa.

## Files trong NewDataRefactor/Baking/Blobs

- AbiilityBlob.cs: giữ Attack/Build/Gather/Storage; NativeList đổi thành BlobArray; ID public để BlobBuilder có thể gán; thêm GetID và giữ getID tương thích IGameBlobAsset hiện tại. Giữ BuildingIDsInReg là indices theo thiết kế người dùng, không tự đổi sang loại khác.
- BaseBlob.cs: ID, MaxHP, WorkRate, JobCapacity, abilities/jobs references gồm CatalogKey + ID và gameplay PrefabIndex.
- UnitBlob.cs: Base inline, PopulationCost, ResourceCosts, UnitTypes.
- BuildingBlob.cs: Base inline, Tags, Cost, PopulationCapacity, WorkLoad.
- JobBlob.cs: thay template MonoBehaviour bằng JobBlob; TrainBlob có Job inline và OutputUnitID; ResearchBlob có Job và Tech inline.
- TechBlob.cs: TechBlob có ID lấy từ Research.Id và Cost; TechTreeBlob có Nodes; node giữ TechID và Prerequisites.
- CivBlob.cs: ID, TechTreeID, UnitUnlocks; mỗi entry giữ UnitID và prerequisite IDs.

Unit/Building.GetID trả Base.ID; Train/Research.GetID trả Job.ID. Helper structs không tương ứng SO độc lập (node/unlock/reference) không có identity riêng. Không thêm ID field vào SO.

Blob không chứa dữ liệu UI: không Icon/IconIndex, Name/DisplayName hoặc Position hiển thị node TechTree. UI nhận ID của blob và tự tra dữ liệu presentation từ Resources; các fields trên SO vẫn giữ nguyên. Base.PrefabIndex là liên kết prefab gameplay, không phải UI, nên được giữ (-1 khi thiếu). Chưa tạo bảng mới hoặc viết baker. WeaponDefinitionBlob do người dùng đặt trong AttackAbilityDefinition.cs vẫn giữ nguyên; Entity projectile trong blob cần xem xét remapping khi tích hợp baking.

## Cách dùng và giới hạn Registry hiện tại

- BlobArray phải allocate bằng BlobBuilder; blob hiện không giữ BlobString vì tên hiển thị thuộc UI. Không dùng NativeList làm storage bên trong blob.
- Đọc root và nested structs chứa BlobArray qua ref/BlobAssetReference; không copy struct ra khỏi blob rồi đọc relative offsets.
- GameDataRegistryComponent hiện trả T bằng giá trị và dùng BlobAssetReference<T>.Create(T): API đó chưa phù hợp các blob có arrays. Cần sửa riêng khi làm Registry, chưa sửa trong lượt này.
- nameof(T) trong Registry hiện trả chuỗi "T", không phải tên kiểu concrete. Đây cũng là vấn đề Registry cần xử lý riêng; không thay kiến trúc map trong lượt chỉ viết struct.
- Nested Native containers trong map và lifetime/serialization chưa được kiểm chứng. Các struct compile không có nghĩa catalog/baking pipeline đã hoạt động.

Kiểm chứng: compile toàn runtime bằng Roslyn của Unity với references/source generators hiện có pass; chỉ còn diagnostics legacy CS0618/SGFE009. Evidence tại Temp/BlobDefinitionsCompile. Chưa chạy Unity baking/serialization/Burst/PlayMode.


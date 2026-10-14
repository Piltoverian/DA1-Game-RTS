# Registry và dữ liệu UI — 2026-10-14

## Cách dùng hiện tại

`RegistryAuthoring.Instance` trả về **tài nguyên `GameDataRegistry`**, không trả về thành phần `RegistryAuthoring`. Các hàm `GetIcon`, `GetName`, `GetBuildingSO` và `GetBuildingPrefab` nằm trên `GameDataRegistry`; các overload generic `GetIcon<T>` và `GetName<T>` cũng nằm tại đó.

Luồng tra cứu UI:

```text
DefinitionID / JobID từ ECS
    → EntityPresentation
    → RegistryAuthoring.Instance (GameDataRegistry)
    → SO tương ứng → icon / tên / prefab preview
```

Blob và thành phần lối chơi tiếp tục giữ ID/config lối chơi. Icon và tên được đọc từ ScriptableObject, không đưa vào blob. mẫu đối tượng xem trước là GameObject từ SO; mẫu đối tượng lối chơi được bộ chuyển dữ liệu sang ECS chuyển sang Entity và lưu trong `RegistryPrefabElement`.

## Thiết lập cảnh

1. Giữ một `RegistryAuthoring` trong cảnh và gán tài nguyên vào trường `gameDataRegistry` trên Inspector.
2. Đăng ký Unit, Building, Job, khả năng và Civ cần dùng trong tài nguyên đó.
3. RegistryAuthoring đi qua Baker trong luồng chuyển GameObject sang ECS; không dựa vào Awake của authoring đã bake.

Sửa đổi của chủ dự án ngày 14/10/2026 bỏ Awake và gán `__instance = authoring.gameDataRegistry` tại đầu `Baker.Bake`. Baker đồng thời kiểm tra số RegistryAuthoring và tạo registry ECS. `ResetInstance`, gọi ở `SubsystemRegistration`, xóa tham chiếu cũ khi bắt đầu Play, kể cả khi tắt Domain Reload. Getter `Instance` chỉ trả về biến tĩnh; không tìm GameObject, không tự tải `Resources/GameReg` và không đọc danh mục dữ liệu từ ECS.

Asset hiện được đặt tại `Assets/_Project/Resources/GameReg.asset`, nhưng cơ chế này dùng tham chiếu Inspector, không phụ thuộc tên hay đường dẫn tài nguyên.

Ngày 14/10/2026, registry dogfood được chuyển thành asset chính tại đường dẫn trên, giữ GUID `f1769d193e7cb494eb67a0d8bf379fbc`; registry cũ (GUID `ce4d37957f8cf7448a79152e5e5939fd`) đã xóa. Main/EntitySubscene và fixture TwoCivDogfood cùng tham chiếu registry này. Main giữ 8 player/spawn slot, dùng Civ A cho player lẻ và Civ B cho player chẵn; PlayerContext ban đầu dùng Civ A. Các definition/prefab của hai civ vẫn nằm trong Tests/Fixtures/TwoCivDogfood và được game tham chiếu trực tiếp.

Helper Editor TwoCivDogfood đã xóa khi chốt session; registry và fixture runtime giữ nguyên. Validate dữ liệu bằng Inspector GameDataRegistry hoặc Tools/Game Data/Validate Selected Registry.

Worker/Town Hall hiện dùng bộ TwoCivDogfood: Worker.asset → WorkerBase.asset → Worker.prefab, với ID `dogfood.worker`; cả Civ A/B dùng prefab này cho StartWorker và đăng ký Worker trong UnitUnlocks. TrainWorker.asset (`dogfood.train.worker`) tạo Worker mới; TownHallBase có job đó và TrainSoldier. Build.asset (`dogfood.build`) của Worker chỉ cung cấp TownHall.asset (`dogfood.townhall`). Registry đã bỏ Worker/TownHall/TrainWorker/Build cũ của MapGeneration. Gather, tốc độ làm việc, range, chi phí và thông số movement của Worker giữ theo bộ ECS đã dùng trước. Helper Setup đã xóa; asset MapGeneration giữ cho các fixture khác.

## Quy luật prefab khởi đầu

Registry có `DefaultWorker` (UnitSO) và `DefaultTownHall` (BuildingSO), phải nằm trong danh sách đăng ký tương ứng nếu được gán. Mặc định hiện dùng bộ Worker/Town Hall mới. MapGenConfig không có trường Worker/Town Hall; map chỉ tham chiếu PlayerBootstrapSettings cho số worker, tuổi khởi đầu, tài nguyên và bán kính placement.

Khi baking từng CivBlob, `GetStartingWorker` ưu tiên CivDef.StartWorker, nếu null mới lấy DefaultWorker.basedSO.Prefab. `GetStartingTownHall` ưu tiên CivDef.TownHallPrefab, nếu null mới lấy DefaultTownHall.basedSO.Prefab. Hai quyết định độc lập; civ có đủ prefab không cần mặc định. Thay đổi registry/default definition khiến baking cập nhật lại prefab index của civ.

PlayerBootstrapSystem dùng các index đã resolve trong CivBlob. Prefab được chọn vẫn phải thỏa component/health/footprint/movement; Worker được chọn phải có trong UnitUnlocks của civ, không có tech prerequisites. Prefab civ có nhưng không hợp lệ sẽ báo lỗi, không âm thầm dùng mặc định. Nếu cả civ và default thiếu prefab cần dùng, bootstrap Failed trước khi instantiate; WorkerCount=0 không yêu cầu prefab Worker. Cấu hình mặc định không thay đổi train/build/unlock của civ.

## Khi dữ liệu chưa sẵn sàng

Trước khi Baker gán tài nguyên, `Instance` có thể null. `EntityPresentation` trả icon/prefab/SO là null hoặc dùng ID làm tên dự phòng. Các hàm tra cứu trên tài nguyên kiểm tra danh sách và phần tử null để tránh lỗi khi danh mục chưa đầy đủ. Overload generic chỉ hỗ trợ `UnitSO`, `BuildingSO`, `Job`, `CivDef`; kiểu khác báo `NotSupportedException`.

`Instance` là static C# tham chiếu ScriptableObject, không thuộc ECS World. Blob/buffer registry thuộc ECS World là phần riêng. Asset không bị mất do baking; authoring đã chuyển sang entity không đi qua Awake như GameObject thông thường. Mở SubScene không bảo đảm Awake chạy. Hiện static được gán bởi Baker, trong khi ResetInstance vẫn xóa nó tại SubsystemRegistration. Cần kiểm chứng Play Mode khi không rebake, Domain Reload bật/tắt và player build dùng dữ liệu bake sẵn; chưa kết luận static được khôi phục trong các trường hợp đó.

## Kiểm chứng đã thực hiện

Test Baker đã chạy trước khi source test được xóa theo yêu cầu chốt session. Dùng CivOnly với roster rỗng để cô lập publication; không kiểm tra toàn bộ prefab baking hay Play Mode. Kết quả giữ lại: [51/51 EditMode PASS](../../../Artifacts/TwoCivDogfood/registry-baker-results.xml), 0 fail/skip, 14/10/2026 12:41:51 UTC. Chi tiết và giới hạn trong [Handoff](../../Maintenance/Handoff.md).

Source: Assets/_Project/Scripts/Runtime/GameData/Authoring/RegistryAuthoring.cs, Assets/_Project/Scripts/Runtime/GameData/DefinitionScript/GameDataRegistry.cs và Assets/_Project/Scripts/Runtime/Presentation/UI/EntityPresentation.cs.

# Registry và dữ liệu UI — 2026-10-06

## Cách dùng hiện tại

`RegistryAuthoring.Instance` trả về **asset `GameDataRegistry`**, không trả về component `RegistryAuthoring`. Các hàm `GetIcon`, `GetName`, `GetBuildingSO` và `GetBuildingPrefab` nằm trên `GameDataRegistry`; các overload generic `GetIcon<T>` và `GetName<T>` cũng nằm tại đó.

Luồng tra cứu UI:

```text
DefinitionID / JobID từ ECS
    → EntityPresentation
    → RegistryAuthoring.Instance (GameDataRegistry)
    → SO tương ứng → icon / tên / prefab preview
```

Blob và component gameplay tiếp tục giữ ID/config gameplay. Icon và tên được đọc từ ScriptableObject, không đưa vào blob. Prefab preview là GameObject từ SO; prefab gameplay được baker chuyển sang Entity và lưu trong `RegistryPrefabElement`.

## Thiết lập scene

1. Giữ một `RegistryAuthoring` trong scene và gán asset vào trường `gameDataRegistry` trên Inspector.
2. Đăng ký Unit, Building, Job, Ability và Civ cần dùng trong asset đó.
3. Workflow Editor hiện tại giữ SubScene mở. Kiểm tra component authoring vẫn tồn tại và `Awake` chạy khi vào Play.

`Awake` gán asset vào biến static. `ResetInstance`, gọi ở `SubsystemRegistration`, xóa tham chiếu cũ khi bắt đầu Play, kể cả khi tắt Domain Reload. Getter `Instance` chỉ trả về biến static; không tìm GameObject, không tự tải `Resources/GameReg` và không đọc registry từ ECS.

Asset hiện được đặt tại `Assets/_Project/Resources/GameReg.asset`, nhưng cơ chế này dùng tham chiếu Inspector, không phụ thuộc tên hay đường dẫn Resources.

## Khi dữ liệu chưa sẵn sàng

Trước khi `Awake` gán asset, `Instance` có thể null. `EntityPresentation` trả icon/prefab/SO là null hoặc dùng ID làm tên dự phòng. Các hàm tra cứu trên asset kiểm tra list và phần tử null để tránh lỗi khi catalog chưa đầy đủ. Overload generic chỉ hỗ trợ `UnitSO`, `BuildingSO`, `Job`, `CivDef`; kiểu khác báo `NotSupportedException`.

Singleton này được khởi tạo bằng lifecycle MonoBehaviour, không phải kết quả của baking. Việc mở SubScene là giả định của workflow Editor hiện tại; chưa xác nhận cơ chế khởi tạo này trong Player build hoặc khi authoring bị unload. Nếu thay workflow đó, cần kiểm chứng lại nguồn khởi tạo trước khi dùng UI.

## Kiểm chứng đã thực hiện

Sau khi chuyển các hàm tra cứu sang asset, build `Assembly-CSharp-Editor.csproj` thành công: 0 lỗi, 5 cảnh báo có sẵn (1 API `AddComponent` obsolete và 4 field trong `InfoIconMapping`). Sau đó đã bỏ fallback Resources theo yêu cầu; thay đổi này chưa được build lại hoặc kiểm chứng trong Play Mode. Không coi kết quả build trước đó là nghiệm thu runtime UI.

Source: `Assets/_Project/Scripts/Runtime/GameData/Authoring/RegistryAuthoring.cs`, `Assets/_Project/Scripts/Runtime/GameData/DefinitionScript/GameDataRegistry.cs` và `Assets/_Project/Scripts/Runtime/Presentation/UI/EntityPresentation.cs`.

# Manual — tạo asset và thêm kiểu dữ liệu vào Game Registry

Cập nhật 2026-09-21, theo source BlobAsset + lookup type/ID trực tiếp trên buffer (không có Registry system). Đây là hướng dẫn chính cho việc mở rộng kiểu dữ liệu; các snapshot cũ trong DesignerSetup không thay thế hướng dẫn này.

## 1. Chọn đúng công việc

| Mục tiêu | Ai làm | Có sửa C# không? |
|---|---|---|
| Thêm một cấu hình Attack/Gather/Build/... đã có | Designer | Không |
| Đổi giá, range, weapons hoặc các thông số của asset | Designer | Không |
| Thêm một loại hoàn toàn mới, ví dụ Heal | Người có thể sửa C# chuẩn bị mẫu; designer tạo các asset sau đó | Có |
| Làm cho Heal thực sự hồi máu trong trận | Lập trình gameplay | Có; tạo data chưa tạo hành vi |

Map mở rộng theo kiểu C#, không có nghĩa Unity tự hiểu cách bake mọi SO mới. Mỗi kiểu mới vẫn cần định nghĩa dữ liệu và nhánh bake tương ứng.

## 2. Designer tạo thêm asset thuộc kiểu đã có

1. Trong Project, dùng Create → ScriptableObjects → Abilities → Attack/Gather/Build/Storage để tạo asset tương ứng. Không tạo thêm class chỉ để có một preset khác.
2. Điền các thông số gameplay. Ví dụ hai Attack assets có thể khác Range, Weapons và Damage nhưng đều bake thành AttackBlob.
3. Thêm asset vào `GameDataRegistry.Abilities`.
4. Nếu một Unit/Building dùng ability đó, thêm **cùng asset** vào `BasedSO.Abilities` của nó. Chỉ thêm vào Base không thay thế đăng ký trong Registry.
5. Chọn Registry, bấm **Assign Missing IDs**, rồi Save Project. Giữ ID đã cấp; không tự đổi ID khi đổi tên asset. Duplicate asset có thể mang theo ID cũ: tool chỉ cấp ID thiếu, không tự sửa ID trùng.
6. Bấm **Validate** hoặc menu **Tools → Game Data → Validate Selected Registry**, xử lý errors, rồi lưu và để Unity bake lại.
7. Trong fixture dùng để kiểm tra catalog đầy đủ, chọn `BakeAll` trên RegistryAuthoring. Kiểm tra giá trị runtime sau khi sửa SO.

Một Base hiện chỉ được chứa một ability của mỗi concrete subtype: hai Attack assets trong cùng Base sẽ bị validator báo lỗi. Muốn nhiều vũ khí thì dùng danh sách Weapons của Attack. Cùng một ability asset có thể dùng chung cho nhiều Base.

Đối với Job, đăng ký ở `Registry.Jobs` và `Base.Jobs`. Với Unit/Building/Civ, dùng danh sách tương ứng trong Registry. Các references không tự mở rộng danh sách đăng ký.

## 3. Sơ đồ để đối chiếu khi thêm kiểu mới

```text
HealAbilityDefinition.asset       ← designer điền dữ liệu
        ↓ Registry.Abilities
RegistryAuthoring.BakeAbilities  ← chuyển từng field
        ↓ BlobBuilder + AddBlobAsset
HealBlob                        ← ID + gameplay, BlobArray cho lists
        ↓ RegistryBlobElement
RegistryBlobLookup              ← đọc trực tiếp buffer đã bake
        ↓
GetBlobByID<HealBlob>(id)        ← gameplay đọc bằng ref
```

`BasedSO.Abilities` có một đường tham chiếu riêng: `BakeBase` ghi `CatalogKey = RegistryTypeKey.For<HealBlob>()` và `ID = heal.Id`. Vì vậy khi thêm ability mới phải sửa **cả BakeAbilities và BakeBase**.

Trong các đường dẫn dưới đây, `ROOT` nghĩa là:

```text
Assets/Scripts/Game(New Simulation Logic)/NewDataRefactor
```

| Chỗ cần thêm/sửa | Vai trò |
|---|---|
| ROOT/DefinitionScript/Ability/HealAbilityDefinition.cs | Schema SO và menu tạo asset |
| ROOT/Baking/Blobs/HealBlob.cs | Schema unmanaged dùng trong blob |
| ROOT/Authoring/RegistryAuthoring.cs → BakeBase | Map subtype SO sang type key của blob |
| ROOT/Authoring/RegistryAuthoring.cs → BakeAbilities | Copy dữ liệu vào blob |
| ROOT/Validation/DefinitionValidator.cs → Ability | Kiểm tra fields mới |
| Gameplay system/handler tương ứng | Đọc blob và thực hiện hành vi |

Các đoạn Heal bên dưới là **mẫu để thêm**, chưa phải tính năng đã cài trong project. Không sửa RegistryBlobLookup hoặc thêm enum loại để đăng ký Heal. Các helper của baker đã có sẵn.

## 4. Mẫu hoàn chỉnh: thêm Heal ability

Mẫu này có Range và một danh sách các nhịp hồi máu. Mỗi nhịp có Amount và DelaySeconds. Nó minh họa cả scalar lẫn list; quy tắc thời điểm thực thi do gameplay system xử lý sau.

### Bước A — tạo SO

Tạo `ROOT/DefinitionScript/Ability/HealAbilityDefinition.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct HealPulseDefinition
{
    [Min(0.001f)] public float Amount;
    [Min(0)] public float DelaySeconds;
}

[CreateAssetMenu(menuName = "ScriptableObjects/Abilities/Heal")]
public class HealAbilityDefinition : AbilityDefinition
{
    [Min(0)] public float Range = 5f;
    public List<HealPulseDefinition> Pulses = new();
}
```

Id kế thừa từ AbilityDefinition; không khai báo thêm ID trùng tên. Các field tên/icon phục vụ UI, nếu cần thêm vào SO, không chuyển vào blob. `[Min]` hỗ trợ nhập liệu trong Inspector, không thay thế validator.

### Bước B — tạo blob struct

Tạo `ROOT/Baking/Blobs/HealBlob.cs`:

```csharp
using Unity.Collections;
using Unity.Entities;

public struct HealPulseBlob
{
    public float Amount;
    public float DelaySeconds;
}

public struct HealBlob : IGameBlobAsset
{
    public FixedString64Bytes ID;
    public float Range;
    public BlobArray<HealPulseBlob> Pulses;

    public FixedString64Bytes GetID() => ID;
    public FixedString64Bytes getID() => GetID();
}
```

`getID()` là chữ ký interface hiện tại, phân biệt hoa/thường. `GetID()` là alias theo mẫu các blob đang có. Không thêm NativeList, CustomList, List, string managed hoặc reference SO vào blob. Blob không implement Dispose; Unity quản lý allocation đã đăng ký bằng AddBlobAsset.

### Bước C — thêm reference từ Base

Trong `RegistryAuthoring.Baker.BakeBase`, tìm switch expression `long key = source switch` nằm trong vòng lặp **Abilities**. Thêm nhánh sau trước nhánh `_ => throw`:

```csharp
HealAbilityDefinition _ => RegistryTypeKey.For<HealBlob>(),
```

Không chèn vào switch Jobs. Đoạn code hiện có phía sau sẽ tự ghi `source.Id` vào reference. Kiểu tại đây phải đúng `HealBlob`, trùng với bước bake và lookup bên dưới.

### Bước D — bake toàn bộ fields

Trong `RegistryAuthoring.Baker.BakeAbilities`, thêm case sau vào `switch (authoring)`, trước `default`:

```csharp
case HealAbilityDefinition heal:
{
    ref var blob = ref builder.ConstructRoot<HealBlob>();
    blob.ID = heal.Id;
    blob.Range = heal.Range;

    var pulses = builder.Allocate(ref blob.Pulses, heal.Pulses.Count);
    for (int i = 0; i < heal.Pulses.Count; i++)
    {
        var pulse = heal.Pulses[i];
        pulses[i] = new HealPulseBlob
        {
            Amount = pulse.Amount,
            DelaySeconds = pulse.DelaySeconds
        };
    }

    AddBlobToReg<HealBlob>(et, ref builder);
    break;
}
```

Hàm hiện có đã gọi DependsOn(authoring), tạo `builder` trước switch và Dispose builder trong finally. Không cần tạo builder thứ hai hoặc dispose blob trong case. Helper **của baker** `AddBlobToReg<T>(et, ref builder)` tạo blob hoàn chỉnh, gọi AddBlobAsset và lưu reference; không cấp phát map runtime.

`heal.Pulses.Count` có sẵn, không duyệt để đếm. Mỗi phần tử chỉ được copy một lần. Danh sách rỗng hợp lệ theo mẫu này và không tạo nhịp hồi máu.

### Bước E — thêm validation bên ngoài baker

Trong `DefinitionValidator.Ability`, thêm case trước `default`:

```csharp
case HealAbilityDefinition h:
{
    c.Number(h.Range, 0, h, nameof(h.Range));
    if (!c.List(h.Pulses, h, nameof(h.Pulses))) break;
    for (int i = 0; i < h.Pulses.Count; i++)
    {
        var pulse = h.Pulses[i];
        string path = $"Pulses[{i}]";
        c.Number(pulse.Amount, 0, h, path + ".Amount", true);
        c.Number(pulse.DelaySeconds, 0, h, path + ".DelaySeconds");
    }
    break;
}
```

Trong mẫu này: Range/DelaySeconds hữu hạn và không âm; Amount hữu hạn và lớn hơn 0. Common validation cho AbilityDefinition đã kiểm tra Id theo concrete subtype. Chưa thêm case sẽ nhận `SUBTYPE_UNSUPPORTED`. Không đưa lời gọi validator vào BakeAbilities.

### Bước F — tạo asset và đọc thử

Sau khi Unity compile xong:

1. Create → ScriptableObjects → Abilities → Heal; tạo asset `Heal_Test`.
2. Đặt Range = 5; thêm hai Pulses: Amount = 10, DelaySeconds = 0 và Amount = 20, DelaySeconds = 1.
3. Đăng ký asset vào Registry.Abilities và Base.Abilities; Assign Missing IDs, Validate, lưu rồi bake.
4. Truyền **Id thực của asset** vào lookup. Không dùng tên `Heal_Test` thay cho Id.

Ví dụ method đọc trong gameplay code (cần using Unity.Entities và Unity.Collections), dùng tổng Amount để kiểm tra dữ liệu đã copy. Đây chưa phải system thực thi hồi máu:

```csharp
private static float GetConfiguredHealing(
    DynamicBuffer<RegistryBlobElement> registry, FixedString64Bytes abilityId)
{
    var reference = registry.GetBlobByID<HealBlob>(abilityId, out var result);
    if (result != FunctionResult.Success) return 0f;

    ref var heal = ref reference.Value;
    float total = 0f;
    for (int i = 0; i < heal.Pulses.Length; i++)
    {
        ref var pulse = ref heal.Pulses[i];
        total += pulse.Amount;
    }
    return total;
}
```

Kết quả mẫu là 30. Gameplay lấy Registry bằng entityManager.GetBuffer<RegistryBlobElement>(registryEntity, true) trên đúng entity đã load; không cần system khởi tạo map. Phải thêm logic kích hoạt/target/cooldown/ghi Health riêng để Heal có tác dụng thật.

## 5. Type key và ID: không tự nhập key long

`RegistryTypeKey.For<HealBlob>()` tạo hash 64-bit của kiểu C#. `heal.Id` xác định một asset trong loại đó. Lookup cần đúng cả hai. Tạo thêm mười Heal assets vẫn dùng một type key, mỗi asset có ID riêng.

Không dùng GetInstanceID, số thứ tự asset hoặc string.GetHashCode làm key. Không lưu key long thành ID nội dung cho save/network. Lookup nhận loại mới khi có RegistryBlobElement tương ứng, không cần thêm nhánh trong helper tra cứu. Lookup duyệt buffer O(n), không cấp phát map.

Hash 64-bit không bảo đảm tuyệt đối không collision; source hiện chưa có cơ chế phát hiện collision giữa các kiểu. Đây là giới hạn của implementation hiện tại, không phải thao tác designer cần làm thủ công.

## 6. Mẫu mở rộng khi dữ liệu phức tạp hơn

| Dữ liệu SO | Dữ liệu blob | Cách bake |
|---|---|---|
| int/float/bool/enum | Field unmanaged tương ứng | Gán trực tiếp |
| List của giá trị | BlobArray của giá trị | Allocate theo Count, copy từng phần tử |
| List của struct chứa list | BlobArray của struct chứa BlobArray | Allocate array ngoài; allocate từng array con bằng ref |
| Reference SO có identity | ID hoặc cặp CatalogKey + ID | DependsOn SO được đọc, copy ID; đăng ký reference theo membership |
| GameObject prefab | PrefabIndex | Gọi helper PrefabIndex(prefab, et); Entity thật ở RegistryPrefabElement |
| Name/Icon/layout UI | Không có field tương ứng trong blob | Presentation xử lý riêng bằng ID |
| HP hiện tại, cooldown còn lại, queue đang chạy | Component/buffer state riêng | Không ghi state thay đổi vào Registry blob |

Với list lồng nhau, xem `BakeCivs` và `BakeTechTree` đang có. Mẫu quan trọng:

```csharp
// Snippet minh họa: outer[i] nằm trong vùng nhớ của builder.
var outer = builder.Allocate(ref blob.Entries, source.Entries.Count);
for (int i = 0; i < source.Entries.Count; i++)
{
    var input = source.Entries[i];
    var inner = builder.Allocate(ref outer[i].Values, input.Values.Count);
    for (int j = 0; j < input.Values.Count; j++)
        inner[j] = input.Values[j];
}
```

Không copy `outer[i]` ra local rồi Allocate vào local đó: field đích phải nằm trong bộ nhớ của builder. Khi đọc blob runtime cũng dùng ref cho root và struct chứa BlobArray, không copy by-value.

Nếu Heal sau này tham chiếu một SO khác, thêm DependsOn cho SO đó trong case bake và kiểm tra membership/reference trong validator. DependsOn Registry hoặc Heal không tự theo dõi mọi SO con. Không dùng ID collector làm danh sách bake. Nếu reference mới cần tham gia tool cấp ID, cập nhật traversal `RegistryDefinitionIds.Collect` riêng; mẫu Heal chỉ có giá trị nên không cần.

## 7. Nếu thêm Job hoặc một nhóm catalog hoàn toàn mới

| Trường hợp | Những phần cần thêm ngoài blob |
|---|---|
| Subtype của Job | SO kế thừa Job; case ở BakeJobs; nhánh trong switch Jobs của BakeBase; case validator Job; gameplay xử lý kết quả job |
| Nhóm SO độc lập, không phải Ability/Job | Danh sách đăng ký ở GameDataRegistry; vòng lặp gọi bake; membership/validation traversal; ID tooling nếu có ID; consumers |

Job cần copy cả Id và WorkLoad theo mẫu Train/Research. Việc map chứa được blob mới không có nghĩa queue biết thực thi job mới. Chỉ thêm JobKind/dispatch khi gameplay cần; không dùng JobKind để thay type key của map.

`BaseBlob`, `JobBlob` và `TechBlob` hiện được nhúng trong các record khác, không tự có entry riêng chỉ vì implement IGameBlobAsset. Muốn lookup độc lập một kiểu, phải có blob của kiểu đó được bake/đăng ký riêng.

## 8. Kiểm tra trước khi giao cho designer

- [ ] Compile SO, blob, baker, validator không lỗi; không thiếu field gameplay khi copy.
- [ ] Fixture Heal ở trên trả Range = 5, hai Pulses và tổng Amount = 30.
- [ ] Đổi một Amount rồi rebake: lookup phản ánh giá trị mới.
- [ ] Hai Heal assets khác ID lookup đúng từng record; ID không tồn tại trả Failure.
- [ ] Lookup cùng chuỗi ID nhưng sai kiểu không trả record của loại khác.
- [ ] List rỗng và list nhiều phần tử đều đọc được; nested arrays đọc qua ref.
- [ ] Giá trị âm/NaN/Infinity hoặc reference sai bị validator bên ngoài báo đúng field.
- [ ] Save/load SubScene và prefab references chạy đúng; unload/reload không giữ buffer/blob handles cũ.
- [ ] Gameplay handler thực sự dùng dữ liệu nếu tính năng được coi là đã hoàn thành.

Theo yêu cầu 2026-09-22, công cụ kiểm tra blob riêng đã được xóa. Checklist ở trên là đối chiếu khi thêm tính năng, không yêu cầu tạo lại một bộ test blob riêng.

Tình trạng lúc viết manual: các đoạn mã Heal ở bước A–F đã được chèn vào bản sao source trong Temp/DesignerManualCompile và compile cùng runtime/Unity references thành công; không thay production C#. Đây là kiểm tra cú pháp/tích hợp mẫu, chưa phải nghiệm thu Unity baking/PlayMode. Các đoạn mã Heal vẫn chỉ là mẫu tài liệu, không tạo asset/feature thật trong project. Chưa có kết quả nghiệm thu Unity; bộ kiểm tra blob riêng đã bỏ theo yêu cầu.

## 9. Tìm lỗi nhanh

| Triệu chứng | Kiểm tra |
|---|---|
| SUBTYPE_UNSUPPORTED | Thiếu case validator |
| Unknown ability type khi bake Base | Thiếu nhánh type key trong BakeBase |
| Unknown ability type khi bake catalog | Thiếu case BakeAbilities |
| Lookup Failure | Đúng blob type chưa; ID có phải Id asset; đã đăng ký; mode có bake loại đó; Registry entity/buffer đã được load chưa |
| Field bằng 0/list rỗng ngoài dự kiến | Đã gán field/Allocate/copy trước AddBlobToReg chưa |
| Đọc list ra dữ liệu sai | Có copy root/nested struct chứa BlobArray thay vì dùng ref không |
| Sửa SO con không cập nhật | Có DependsOn đúng SO con được đọc không |
| Prefab sai sau load | Dùng PrefabIndex và RegistryPrefabElement của đúng Registry, không nhét Entity vào blob |
| Muốn gọi Dispose sau lookup | Không gọi: reference mượn blob do Unity quản lý |

Tham khảo source flow và ownership tại [R2/README](R2/README.md), schema tại [BlobDefinitions](BlobDefinitions.md).

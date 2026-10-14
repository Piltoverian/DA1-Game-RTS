# Cẩm nang — tạo tài nguyên và thêm kiểu dữ liệu vào Game Registry

Cập nhật 2026-09-21, theo mã nguồn BlobAsset + tra cứu kiểu/ID trực tiếp trên bộ đệm (không có Registry hệ thống). Đây là hướng dẫn chính cho việc mở rộng kiểu dữ liệu; [DesignerSetup](DesignerSetup.md) hướng dẫn cấu hình và Validate; hướng dẫn này tập trung mở rộng kiểu dữ liệu.

## 1. Chọn đúng công việc

| Mục tiêu | Ai làm | Có sửa C# không? |
|---|---|---|
| Thêm một cấu hình Attack/Gather/Build/... đã có | Designer | Không |
| Đổi giá, khoảng, vũ khí hoặc các thông số của tài nguyên | Designer | Không |
| Thêm một loại hoàn toàn mới, ví dụ Heal | Người có thể sửa C# chuẩn bị mẫu; người thiết kế tạo các tài nguyên sau đó | Có |
| Làm cho Heal thực sự hồi máu trong trận | Lập trình lối chơi | Có; tạo dữ liệu chưa tạo hành vi |

bản đồ mở rộng theo kiểu C#, không có nghĩa Unity tự hiểu cách chuyển dữ liệu sang ECS mọi SO mới. Mỗi kiểu mới vẫn cần định nghĩa dữ liệu và nhánh chuyển dữ liệu sang ECS tương ứng.

## 2. Designer tạo thêm tài nguyên thuộc kiểu đã có

1. Trong dự án, dùng `Create → ScriptableObjects → Abilities → Attack/Gather/Build/Storage` để tạo tài nguyên tương ứng. Không tạo thêm class chỉ để có một preset khác.
2. Điền các thông số lối chơi. Ví dụ hai Attack tài nguyên có thể khác khoảng, Weapons và Damage nhưng đều chuyển dữ liệu sang ECS thành AttackBlob.
3. Thêm tài nguyên vào `GameDataRegistry.Abilities`.
4. Nếu một Unit/Building dùng khả năng đó, thêm **cùng tài nguyên** vào `BasedSO.Abilities` của nó. Chỉ thêm vào Base không thay thế đăng ký trong Registry.
5. Chọn Registry, bấm **Assign Missing IDs**, rồi Save dự án. Giữ ID đã cấp; không tự đổi ID khi đổi tên tài nguyên. trùng lặp tài nguyên có thể mang theo ID cũ: tool chỉ cấp ID thiếu, không tự sửa ID trùng.
6. Bấm **Validate** hoặc trình đơn **`Tools → Game Data → Validate Selected Registry`**, xử lý lỗi, rồi lưu và để Unity chuyển dữ liệu sang ECS lại.
7. Trong dữ liệu kiểm thử dùng để kiểm tra danh mục đầy đủ, chọn `BakeAll` trên RegistryAuthoring. Kiểm tra giá trị khi chạy sau khi sửa SO.

Một Base hiện chỉ được chứa một khả năng của mỗi kiểu cụ thể subtype: hai Attack tài nguyên trong cùng Base sẽ bị bộ kiểm tra hợp lệ báo lỗi. Muốn nhiều vũ khí thì dùng danh sách Weapons của Attack. Cùng một khả năng tài nguyên có thể dùng chung cho nhiều Base.

Đối với Job, đăng ký ở `Registry.Jobs` và `Base.Jobs`. Với Unit/Building/Civ, dùng danh sách tương ứng trong Registry. Các tham chiếu không tự mở rộng danh sách đăng ký.

## 3. Sơ đồ để đối chiếu khi thêm kiểu mới

```text
HealAbilityDefinition.asset       ← người thiết kế điền dữ liệu
        ↓ Registry.Abilities
RegistryAuthoring.BakeAbilities  ← chuyển từng trường
        ↓ BlobBuilder + AddBlobAsset
HealBlob                        ← ID+dữ liệu lối chơi,BlobArray cho danh sách
        ↓ RegistryBlobElement
RegistryBlobLookup              ← đọc trực tiếp bộ đệm đã bake
        ↓
GetBlobByID<HealBlob>(id)        ← lối chơi đọc bằng ref
```

`BasedSO.Abilities` có một đường tham chiếu riêng: `BakeBase` ghi `CatalogKey = RegistryTypeKey.For<HealBlob>()` và `ID = heal.Id`. Vì vậy khi thêm khả năng mới phải sửa **cả BakeAbilities và BakeBase**.

Trong các đường dẫn dưới đây, `ROOT` nghĩa là:

```text
Assets/_Project/Scripts/Runtime/GameData
```

| Chỗ cần thêm/sửa | Vai trò |
|---|---|
| ROOT/DefinitionScript/Ability/HealAbilityDefinition.cs | Schema SO và trình đơn tạo tài nguyên |
| ROOT/Baking/Blobs/HealBlob.cs | Schema unmanaged dùng trong blob |
| ROOT/Authoring/RegistryAuthoring.cs → BakeBase | bản đồ subtype SO sang kiểu khóa của blob |
| ROOT/Authoring/RegistryAuthoring.cs → BakeAbilities | sao chép dữ liệu vào blob |
| ROOT/Validation/DefinitionValidator.cs → khả năng | Kiểm tra trường mới |
| lối chơi system/handler tương ứng | Đọc blob và thực hiện hành vi |

Các đoạn Heal bên dưới là **mẫu để thêm**, chưa phải tính năng đã cài trong dự án. Không sửa RegistryBlobLookup hoặc thêm enum loại để đăng ký Heal. Các helper của bộ chuyển dữ liệu sang ECS đã có sẵn.

## 4. Mẫu hoàn chỉnh: thêm Heal khả năng

Mẫu này có khoảng và một danh sách các nhịp hồi máu. Mỗi nhịp có Amount và DelaySeconds. Nó minh họa cả giá trị đơn lẫn danh sách; quy tắc thời điểm thực thi do lối chơi hệ thống xử lý sau.

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

Id kế thừa từ AbilityDefinition; không khai báo thêm ID trùng tên. Các trường tên/icon phục vụ UI, nếu cần thêm vào SO, không chuyển vào blob. `[Min]` hỗ trợ nhập liệu trong Inspector, không thay thế bộ kiểm tra hợp lệ.

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

`getID()` là chữ ký giao diện hiện tại, phân biệt hoa/thường. `GetID()` là alias theo mẫu các blob đang có. Không thêm NativeList, CustomList, danh sách, string được quản lý hoặc tham chiếu SO vào blob. Blob không implement Dispose; Unity quản lý cấp phát đã đăng ký bằng AddBlobAsset.

### Bước C — thêm tham chiếu từ Base

Trong `RegistryAuthoring.Baker.BakeBase`, tìm switch expression `long key = source switch` nằm trong vòng lặp **khả năng**. Thêm nhánh sau trước nhánh `_ => throw`:

```csharp
HealAbilityDefinition _ => RegistryTypeKey.For<HealBlob>(),
```

Không chèn vào switch Jobs. Đoạn mã hiện có phía sau sẽ tự ghi `source.Id` vào tham chiếu. Kiểu tại đây phải đúng `HealBlob`, trùng với bước chuyển dữ liệu sang ECS và tra cứu bên dưới.

### Bước D — chuyển dữ liệu sang ECS toàn bộ trường

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

Hàm hiện có đã gọi DependsOn(thiết lập dữ liệu), tạo `builder` trước switch và Dispose builder trong finally. Không cần tạo builder thứ hai hoặc giải phóng blob trong case. Helper **của bộ chuyển dữ liệu sang ECS** `AddBlobToReg<T>(et, ref builder)` tạo blob hoàn chỉnh, gọi AddBlobAsset và lưu tham chiếu; không cấp phát bản đồ khi chạy.

`heal.Pulses.Count` có sẵn, không duyệt để đếm. Mỗi phần tử chỉ được sao chép một lần. Danh sách rỗng hợp lệ theo mẫu này và không tạo nhịp hồi máu.

### Bước E — thêm kiểm tra hợp lệ bên ngoài bộ chuyển dữ liệu sang ECS

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

Trong mẫu này: Range/DelaySeconds hữu hạn và không âm; Amount hữu hạn và lớn hơn 0. Common kiểm tra hợp lệ cho AbilityDefinition đã kiểm tra Id theo kiểu cụ thể subtype. Chưa thêm case sẽ nhận `SUBTYPE_UNSUPPORTED`. Không đưa lời gọi bộ kiểm tra hợp lệ vào BakeAbilities.

### Bước F — tạo tài nguyên và đọc thử

Sau khi Unity biên dịch xong:

1. Create → ScriptableObjects → khả năng → Heal; tạo tài nguyên `Heal_Test`.
2. Đặt khoảng = 5; thêm hai Pulses: Amount = 10, DelaySeconds = 0 và Amount = 20, DelaySeconds = 1.
3. Đăng ký tài nguyên vào `Registry.Abilities` và `Base.Abilities`; Assign Missing IDs, Validate, lưu rồi chuyển dữ liệu sang ECS.
4. Truyền **Id thực của tài nguyên** vào tra cứu. Không dùng tên `Heal_Test` thay cho Id.

Ví dụ hàm đọc trong mã lối chơi (cần using `Unity.Entities` và Unity.Collections), dùng tổng Amount để kiểm tra dữ liệu đã sao chép. Đây chưa phải hệ thống thực thi hồi máu:

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

Kết quả mẫu là 30. lối chơi lấy Registry bằng entityManager.GetBuffer<RegistryBlobElement>(registryEntity, true) trên đúng thực thể đã load; không cần hệ thống khởi tạo bản đồ. Phải thêm xử lý logic kích hoạt/target/cooldown/ghi Health riêng để Heal có tác dụng thật.

## 5. Type khóa và ID: không tự nhập khóa long

`RegistryTypeKey.For<HealBlob>()` tạo mã băm 64-bit của kiểu C#. `heal.Id` xác định một tài nguyên trong loại đó. Lookup cần đúng cả hai. Tạo thêm mười Heal tài nguyên vẫn dùng một kiểu khóa, mỗi tài nguyên có ID riêng.

Không dùng GetInstanceID, số thứ tự tài nguyên hoặc string.GetHashCode làm khóa. Không lưu khóa long thành ID nội dung cho save/network. Lookup nhận loại mới khi có RegistryBlobElement tương ứng, không cần thêm nhánh trong helper tra cứu. Tạo mục bằng RegistryBlobElement.Create<T>(blob); bộ chuyển dữ liệu sang ECS sắp xếp bộ đệm theo (TypeKey, ID), tra cứu nhị phân O(nhật ký n) với khóa duy nhất, không cấp phát bản đồ.

Hash 64-bit không bảo đảm tuyệt đối không collision; mã nguồn hiện chưa có cơ chế phát hiện collision giữa các kiểu. Đây là giới hạn của implementation hiện tại, không phải thao tác người thiết kế cần làm thủ công.

## 6. Mẫu mở rộng khi dữ liệu phức tạp hơn

| Dữ liệu SO | Dữ liệu blob | Cách chuyển dữ liệu sang ECS |
|---|---|---|
| int/float/bool/enum | Field unmanaged tương ứng | Gán trực tiếp |
| danh sách của giá trị | BlobArray của giá trị | Allocate theo số lượng, sao chép từng phần tử |
| danh sách của struct chứa danh sách | BlobArray của struct chứa BlobArray | Allocate array ngoài; cấp phát từng array con bằng ref |
| tham chiếu SO có identity | ID hoặc cặp CatalogKey + ID | DependsOn SO được đọc, sao chép ID; đăng ký tham chiếu theo thuộc danh mục |
| GameObject mẫu đối tượng | PrefabIndex | Gọi helper PrefabIndex(mẫu đối tượng, et); Entity thật ở RegistryPrefabElement |
| Name/Icon/layout UI | Không có trường tương ứng trong blob | Presentation xử lý riêng bằng ID |
| HP hiện tại, cooldown còn lại, hàng đợi đang chạy | Component/bộ đệm trạng thái riêng | Không ghi trạng thái thay đổi vào Registry blob |

Với danh sách lồng nhau, xem `BakeCivs` và `BakeTechTree` đang có. Mẫu quan trọng:

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

Không sao chép `outer[i]` ra cục bộ rồi Allocate vào cục bộ đó: trường đích phải nằm trong bộ nhớ của builder. Khi đọc blob khi chạy cũng dùng ref cho gốc và struct chứa BlobArray, không sao chép by-value.

Nếu Heal sau này tham chiếu một SO khác, thêm DependsOn cho SO đó trong case chuyển dữ liệu sang ECS và kiểm tra membership/reference trong bộ kiểm tra hợp lệ. DependsOn Registry hoặc Heal không tự theo dõi mọi SO con. Không dùng ID collector làm danh sách chuyển dữ liệu sang ECS. Nếu tham chiếu mới cần tham gia tool cấp ID, cập nhật traversal `RegistryDefinitionIds.Collect` riêng; mẫu Heal chỉ có giá trị nên không cần.

## 7. Nếu thêm Job hoặc một nhóm danh mục hoàn toàn mới

| Trường hợp | Những phần cần thêm ngoài blob |
|---|---|
| Subtype của Job | SO kế thừa Job; case ở BakeJobs; nhánh trong switch Jobs của BakeBase; case bộ kiểm tra hợp lệ Job; lối chơi xử lý kết quả tác vụ |
| Nhóm SO độc lập, không phải Ability/Job | Danh sách đăng ký ở GameDataRegistry; vòng lặp gọi chuyển dữ liệu sang ECS; membership/validation traversal; ID tooling nếu có ID; consumers |

Job cần sao chép cả Id và WorkLoad theo mẫu Train/Research. Việc bản đồ chứa được blob mới không có nghĩa hàng đợi biết thực thi tác vụ mới. Chỉ thêm JobKind/dispatch khi lối chơi cần; không dùng JobKind để thay kiểu khóa của bản đồ.

`BaseBlob`, `JobBlob` và `TechBlob` hiện được nhúng trong các bản ghi khác, không tự có mục riêng chỉ vì implement IGameBlobAsset. Muốn tra cứu độc lập một kiểu, phải có blob của kiểu đó được bake/đăng ký riêng.

## 8. Kiểm tra trước khi giao cho người thiết kế

- [ ] biên dịch SO, blob, bộ chuyển dữ liệu sang ECS, bộ kiểm tra hợp lệ không lỗi; không thiếu trường lối chơi khi sao chép.
- [ ] Fixture Heal ở trên trả khoảng = 5, hai Pulses và tổng Amount = 30.
- [ ] Đổi một Amount rồi chuyển dữ liệu sang ECS lại: tra cứu phản ánh giá trị mới.
- [ ] Hai Heal tài nguyên khác ID tra cứu đúng từng bản ghi; ID không tồn tại trả Failure.
- [ ] Lookup cùng chuỗi ID nhưng sai kiểu không trả bản ghi của loại khác.
- [ ] danh sách rỗng và danh sách nhiều phần tử đều đọc được; nested arrays đọc qua ref.
- [ ] Giá trị âm/NaN/Infinity hoặc tham chiếu sai bị bộ kiểm tra hợp lệ bên ngoài báo đúng trường.
- [ ] Save/load SubScene và mẫu đối tượng tham chiếu chạy đúng; unload/reload không giữ bộ đệm/blob handles cũ.
- [ ] lối chơi handler thực sự dùng dữ liệu nếu tính năng được coi là đã hoàn thành.

Theo yêu cầu 2026-09-22, công cụ kiểm tra blob riêng đã được xóa. Checklist ở trên là đối chiếu khi thêm tính năng, không yêu cầu tạo lại một bộ kiểm thử blob riêng.

Tình trạng lúc viết hướng dẫn: các đoạn mã Heal ở bước A–F đã được chèn vào bản sao mã nguồn trong Temp/DesignerManualCompile và biên dịch cùng runtime/Unity tham chiếu thành công; không thay sản xuất C#. Đây là kiểm tra cú pháp/tích hợp mẫu, chưa phải nghiệm thu Unity baking/PlayMode. Các đoạn mã Heal vẫn chỉ là mẫu tài liệu, không tạo asset/tính năng thật trong dự án. Chưa có kết quả nghiệm thu Unity; bộ kiểm tra blob riêng đã bỏ theo yêu cầu.

## 9. Tìm lỗi nhanh

| Triệu chứng | Kiểm tra |
|---|---|
| SUBTYPE_UNSUPPORTED | Thiếu case bộ kiểm tra hợp lệ |
| Unknown khả năng kiểu khi chuyển dữ liệu sang ECS Base | Thiếu nhánh kiểu khóa trong BakeBase |
| Unknown khả năng kiểu khi chuyển dữ liệu sang ECS danh mục | Thiếu case BakeAbilities |
| Lookup Failure | Đúng blob kiểu chưa; ID có phải Id tài nguyên; đã đăng ký; mode có chuyển dữ liệu sang ECS loại đó; Registry entity/bộ đệm đã được load chưa |
| Field bằng 0/list rỗng ngoài dự kiến | Đã gán field/Allocate/sao chép trước AddBlobToReg chưa |
| Đọc danh sách ra dữ liệu sai | Có sao chép root/nested struct chứa BlobArray thay vì dùng ref không |
| Sửa SO con không cập nhật | Có DependsOn đúng SO con được đọc không |
| mẫu đối tượng sai sau load | Dùng PrefabIndex và RegistryPrefabElement của đúng Registry, không nhét Entity vào blob |
| Muốn gọi Dispose sau tra cứu | Không gọi: tham chiếu mượn blob do Unity quản lý |

Tham khảo [trạng thái ownership/gameplay](../../Maintenance/CurrentState.md), lược đồ dữ liệu tại [BlobDefinitions](BlobDefinitions.md).

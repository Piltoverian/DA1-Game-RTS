# Setup definitions — cập nhật 2026-09-21

> Hướng dẫn hiện tại: [Manual tạo asset và thêm kiểu dữ liệu](DesignerTypeManual.md). Manual có mẫu Heal SO → BlobArray → baker → validator → lookup type/ID theo source hiện tại. Registry baker đã có implementation; các nội dung bên dưới là snapshot lịch sử, đặc biệt các đoạn nói Bake rỗng hoặc Tech không có ID. Xem [R2](R2/README.md) cho ownership hiện hành.

## Hiện hành 2026-09-21 — blob không chứa bất kỳ dữ liệu UI nào

Blob chỉ giữ ID và dữ liệu gameplay. UI nhận ID và tự tra dữ liệu từ Resources; không lưu Icon/IconIndex, Name/DisplayName, đường dẫn/key presentation hoặc vị trí layout TechTree trong blob. Các fields presentation trên SO giữ nguyên. PrefabIndex của gameplay không phải UI nên vẫn giữ.

Đã bỏ UI fields khỏi BaseBlob, CivBlob, JobBlob, TechTreeNodeBlob và bỏ using không còn dùng. Compile toàn runtime với Unity references pass; chỉ còn diagnostics legacy CS0618/SGFE009. Chưa chạy Unity baking/serialization/Burst/PlayMode. Lượt cập nhật docs không sửa source hoặc chạy thêm tests.

Source hiện tại: structs ở Baking/Blobs; RegistryAuthoring.Bake rỗng; Registry component là bản người dùng đang chỉnh với các vấn đề key generic, copy blob structs và Create(T) cần xử lý riêng. Không coi helpers/map system/BakeAll ở các snapshot cũ vẫn tồn tại. Xem [BlobDefinitions](BlobDefinitions.md) và [R2 hiện hành](R2/README.md).

## Các snapshot trước — tham khảo lịch sử

Các tiêu đề "mới nhất/đã triển khai" bên dưới thuộc thời điểm được ghi, không thay thế trạng thái hiện hành ở trên.

## Implementation mới nhất — Registry BakeAll đã triển khai

Người dùng đã yêu cầu triển khai, dùng switch cho từng loại SO và đặt các blob chung file GameDataRegistryComponent.cs. BakeAll đã có Registry baker + Job/Ability helpers; 10 typed catalog blobs, prefab Entity buffer và runtime map key tên kiểu → untyped blob handle có type guard. Base/Job/Ability chung và Tech được nhúng trong records; Base giữ key+ID references tới các catalog abilities/jobs riêng. Layout này thay các đề xuất cũ chép tất cả ability/job config vào mỗi Unit/Building.

Native map không bake vào SubScene: chỉ source tag/blob reference buffers được serialize; RegistryCatalogMapSystem khởi tạo map trong runtime và sở hữu cleanup. Blob lifetime thuộc baking/scene. Unit/Building records có dữ liệu catalog; chưa thay gameplay components/bakers của prefab hoặc nối production/research consumers.

CivOnly và ParameterBake chưa có đủ hợp đồng và đang báo chưa hỗ trợ rõ ràng. Đã giải thích ParameterBake cần input chọn Civ và quy tắc Building theo Civ; không đoán scope. Dùng BakeAll hiện tại.

Compile runtime/Editor Unity Roslyn pass; 35 standalone validation cases pass. Thêm checks ID UTF-8 61 byte và invalid Unicode. Tools > Game Data > Run Catalog Checks đã có code và compile, nhưng chưa chạy Unity/Burst thực; actual SubScene bake/serialization/rebake/PlayMode pending. Xem Docs/DataRefactor/R2/README.md để setup, lookup example và các file thực tế. Không coi compile là nghiệm thu runtime.

## Quyết định mới nhất — bake config đầy đủ theo từng record

Người dùng chốt chấp nhận lặp config: Unit/Building record chứa luôn Base config, abilities và job offers. Train chép WorkLoad, giá/pop cùng liên kết Unit/prefab; Research chép WorkLoad, Cost và TechId từ Research.Id. Không tách bảng Base/Ability/Job chỉ để tránh lặp, không lồng toàn bộ SO references đệ quy. Registry membership/validator hiện có giữ nguyên.

Không bắt buộc hệ thống nhiều struct typed ID/stable-ID lookup. Runtime chỉ giữ ID ở nơi cần identity như Tech; liên kết trong catalog có thể dùng index hoặc prefab Entity reference. Khi dữ liệu Editor đổi thì bake lại; không thiết kế Registry hot reload giữa trận. Ability/Job đăng ký chưa được dùng vẫn validate nhưng không cần record runtime riêng.

Đọc R2/README và R2/02–03 cho layout và flow hiện hành. Assistant làm validation; người dùng làm data/baker/game logic. Lượt cập nhật này chỉ sửa docs, chưa sửa source hoặc chạy thêm tests. Quyết định này thay các mô tả bảng config chuẩn hóa/typed-ID bắt buộc trong các snapshot cũ bên dưới.

## Phạm vi đăng ký để bake — kế hoạch chốt 2026-09-20

**Cập nhật implementation mới nhất:** validation membership đã có. Mỗi Tech trong Nodes của cây đã nhận phải có đúng một Research asset trong Registry.Jobs tham chiếu; Research.Id là nguồn TechId. Hai Research khác nhau cùng Tech báo TECH_RESEARCH_AMBIGUOUS; thiếu Research báo TECH_RESEARCH_MISSING. Nhiều nhà chia sẻ cùng Research là hợp lệ. Không thêm ID vào Tech SO. Các đoạn lịch sử dưới đây nói identity chưa chốt/nhiều Research dùng chung Tech đã được thay thế.

Nút Validate/menu hiện có báo thêm BASE_ABILITY_NOT_IN_REGISTRY, BASE_JOB_NOT_IN_REGISTRY, TRAIN_UNIT_NOT_IN_REGISTRY, RESEARCH_TECH_NOT_IN_CATALOG và lỗi identity Base mơ hồ. Đăng ký đầy đủ dữ liệu, không chỉ cấp ID. Validator không tự thêm đăng ký hoặc sửa asset. 32 ca standalone và compile pass; Inspector/serialization Unity chưa nghiệm thu.

Hợp đồng đích tại [R2/01](R2/01-Contracts.md): đăng ký Unit/Building/Ability/Job/Civ vào đúng list Registry, kể cả Ability/Job đã được Base tham chiếu. Base lấy từ Unit/Building đó; cây lấy từ Civ đó; Tech phải xuất hiện trong Nodes của một cây đã nhận. Train.OutputUnit không tự đăng ký Unit; Research không tự đăng ký Tech. Roster/prerequisites phải thuộc Registry/cây tương ứng.

Validator hiện tại đã kiểm tra các giới hạn membership này. ID tooling bên dưới giữ nguyên: có ID chưa có nghĩa asset được nhận vào catalog. Runtime/baker do người dùng triển khai; khi có Error từ Validate phải chặn catalog. Tech không có field ID riêng vì dùng Research.Id qua mapping lúc bake.

## Schema Research/Tech mới nhất — 2026-09-20

Research hiện là Job: Id, DisplayName, Icon và WorkLoad kế thừa Job; field techDefinition được serialize và đọc qua getter TechDefinition. Đưa Research vào Jobs của Registry/Base. TechDefinition là SO độc lập chứa Cost, được tham chiếu từ Research/TechTree/Civ prerequisites; không đưa TechDefinition trực tiếp vào List<Job> nữa. Source Research hiện chưa có CreateAssetMenu riêng.

Validator kiểm tra ID/WorkLoad của Research, reference Tech bắt buộc và Cost trên Tech. TechTree tiếp tục kiểm tra graph theo asset reference; Tech hiện không có Id/WorkLoad. Nhiều Research có thể cùng tham chiếu một Tech. Validate không cấp ID hoặc sửa asset. Compile runtime/Editor pass; save/reload và kiểm thử Unity còn pending. Phần này thay thế các mô tả Research template hoặc Tech kế thừa Job bên dưới.

## Workflow Base ID mới nhất

BasedSO.ID chỉ hiển thị, không chỉnh được trong Inspector thường (kể cả multi-select). Sau khi Base reachable từ GameDataRegistry qua Unit/Building hoặc Train.OutputUnit, chọn Registry và bấm **Assign Missing IDs** để cấp ID thiếu, rồi Save Project. Thêm reference vào Registry không tự cấp ID cho Base nữa; các loại definition khác giữ auto-ID hiện tại. Nút không đổi ID đã có, không sửa ID trùng; Validate/import/reload không cấp ID. Nếu xóa ID bằng công cụ khác, ưu tiên phục hồi bằng Undo/Git trước khi cấp lại.

Giữ nguyên field/serialization; khóa Inspector không ngăn chỉnh bằng Debug Inspector, script hoặc YAML. Đã compile Editor Roslyn pass; chưa kiểm tra trực tiếp Inspector/Undo/Redo/save-reload. Các mô tả auto-ID mọi definition bên dưới là lịch sử, Base nay là ngoại lệ.

Kế hoạch R2 dùng stable typed ID unmanaged khi bake entity và lookup catalog; không dùng catalog array index làm identity. Xem [kế hoạch R2](R2/README.md). Runtime baking chưa triển khai.

**Chốt cuối session 2026-09-18:** người dùng xác nhận ID cấp phát hoạt động rất tốt; giữ implementation hiện tại. Việc triển khai tiếp theo là **R2: catalog runtime, mapping ID → runtime index, baker ownership và prefab bindings**, sau đó baking/dependency rebake. R1 còn checklist nghiệm thu, không còn thiếu implementation ID tooling. Xem mục “Chốt cuối session” đầu SessionHandoff; mục này ưu tiên hơn các snapshot cũ bên dưới. Kiểm thử tổng thể vẫn để sau.

## Cập nhật ID tooling và tổ chức source 2026-09-18

- ID chỉ cấp trong graph registry được chọn. Thêm reference qua Inspector Registry tự cấp GUID string cho definitions mới reachable còn thiếu ID; ID có sẵn luôn giữ nguyên, trùng ID chỉ báo lỗi.
- Reference thêm bên trong Base/Train/Civ/TechTree, registry cũ hoặc chỉnh bằng script: dùng nút Assign Missing IDs trên Registry. Không cấp ID khi import/reload/Validate. Không có sổ ID hoặc quét toàn project. Unit/Building dùng ID của Base.
- Apply hỗ trợ Undo và đánh dấu assets dirty; lưu bằng Save Project. Xóa reference không xóa ID. Nếu ID đã cấp bị xóa nhầm, khôi phục bằng Undo/Git trước khi cấp bổ sung. Duplicate có ID trùng không tự sửa.
- Source được phân loại trong NewDataRefactor: DefinitionScript/ (schema), Validation/ (read-only runtime API), Authoring/ (RefactoredUnitAuthoring), Baking/ (helpers còn rỗng), Editor/Identity/ (RegistryDefinitionIds), Editor/Registry/ (GameDataRegistryEditor). Di chuyển kèm .meta, giữ GUID. R0Audit vẫn ở Assets/Editor.
- Chưa nghiệm thu Unity Inspector/Undo/save-reload. R1 chưa đóng; baking chưa triển khai. Phần lịch sử nói ID tooling chưa có được thay thế bởi cập nhật này.


## Chạy validation hiện có

Chọn asset GameDataRegistry trong Project, bấm **Validate** dưới các field trong Inspector. Đây là nút custom Inspector do project cung cấp. Có thể dùng menu **Tools > Game Data > Validate Selected Registry** khi đang chọn registry.

Inspector hiển thị kết quả lần kiểm tra gần nhất, số Error/Warning, asset path, field path và mã lỗi. Bấm Locate asset/Locate related asset để tìm asset; Console cũng có diagnostics kèm context. Chạy lại sau khi sửa bất kỳ reference nào. Validator không sửa asset, không cấp ID, không tự chạy mỗi frame hoặc qua OnValidate.

Phạm vi là registry được chọn và toàn bộ definitions truy cập qua references, kể cả Civs → TechTree → Nodes/Prerequisites. Asset ngoài graph này không được quét. Các registry được kiểm tra độc lập.

ID so sánh Ordinal (phân biệt hoa/thường), không tự trim. Base có scope chung; Ability/Job có scope theo concrete subtype; Civ và TechTree có scope riêng. Unit/Building dùng identity của Base. Hai asset khác nhau trùng ID là lỗi; cùng asset được nhiều nơi dùng là hợp lệ. Phần tử lặp trong cùng list references là lỗi. ID tooling chưa triển khai.

Cost rỗng hợp lệ; amount âm/NaN/Infinity, enum sai hoặc tổng theo resource vượt float.MaxValue là lỗi. Resource lặp trong cost là cảnh báo; baker/payment sau này phải cộng gộp trước kiểm tra affordability. Không có normalization hoặc mutation trong validator. Enum/tag lặp là cảnh báo. Icon/tên hiển thị không bị bắt buộc. Queue capacity >= 1 chỉ kiểm tra khi Base có Jobs.

Weapon validation hiện kiểm tra config, projectile reference và SlotId trùng; chưa đối chiếu muzzle binding của prefab. Chưa nối validator vào baker. Validation thành công không chứng minh baking/gameplay đã hoạt động.

Đọc [SessionHandoff.md](SessionHandoff.md) cho trạng thái code và [DevelopmentPlan](DevelopmentPlan.md) cho policy. Các SO mới đã có CreateAssetMenu và validator; **ID tool, baking helpers, TechTree editor và UI gameplay mới chưa triển khai**. Tạo asset chưa đủ để chạy gameplay.

## Dữ liệu chỉnh ở đâu

| Chủ sở hữu | Dữ liệu |
|---|---|
| BasedSO | ID string, Name, Icon, Prefab, MaxHP, WorkRate, JobCapacity, Abilities, Jobs |
| UnitSO | basedSO, UnitTypes tags, PopulationCost, ResourceCosts giá train |
| BuildingSO | basedSO, Tags, Cost giá xây, PopulationCapacity, WorkLoad xây |
| BuildAbilityDefinition | WorkPerSecond, Range |
| GatherAbilityDefinition | Capacity, GatherTime, StopDistance, GatherRate; không filter resource |
| AttackAbilityDefinition | Range, RotationSpeed, Weapons inline |
| StorageAbilityDefinition | AcceptedResourceTypes |
| Train | OutputUnit; WorkLoad, tên/icon, Id kế thừa Job; không nhập giá riêng |
| TechDefinition | Cost research; WorkLoad, tên/icon, Id kế thừa Job |
| GameDataRegistry | Lists Units, Buildings, Abilities, Jobs, Civs; Base được duyệt qua Unit/Building.basedSO, không đăng ký riêng |
| CivDef | Id, Name, Icon, TechTree |
| TechTreeDef | Id, Nodes; mỗi node có techDefinition, Prerequisites, Position |

Base không là lớp cha của Unit/Building; hai loại này tham chiếu một Base. Ability/Job có lớp abstract riêng, không có Kind field. Giá train chỉ có một nguồn ở Unit; nhà khác train cùng quân dùng cùng giá. MaxHP ở Base; HP hiện tại, owner, targets và progress ở runtime.

**Cost dùng ResourcePair ở Unit/Building/Tech.** ResourceCost.cs đã được dọn.

## Menu hiện có

- ScriptableObjects/BasedSO, UnitSO, BuildingSO.
- ScriptableObjects/Abilities/Build, Gather, Attack, Storage.
- ScriptableObjects/Train, Tech.
- ScriptableObjects/Game Data Registry.
- Game/CivDef, Game/TechTreeDef.

Ability.cs và Job.cs là abstract, không tạo asset trực tiếp. Research.cs là MonoBehaviour template rỗng, không dùng nó để cấu hình research; dùng TechDefinition.

## Setup đích sau khi tích hợp

1. Tạo BasedSO; điền ID string ổn định, tên/icon/prefab/MaxHP. WorkRate mặc định 1.
2. Tạo UnitSO hoặc BuildingSO trỏ Base. Tags và giá đặt tại loại tương ứng; không copy lên Base.
3. Tạo abilities cần dùng rồi đưa vào Base.Abilities; không thêm Kind hoặc reference ngược Base.
4. Tạo Train trỏ OutputUnit hoặc Tech có Cost; đưa vào Base.Jobs. JobCapacity chỉ cần dùng khi có Jobs.
5. Đăng ký definitions trong registry. IDs không dùng list index, không đổi theo tên hiển thị. ID allocator chưa có; chưa có menu cấp/validate ID để hướng dẫn chạy.
6. Khi R2 có baker, authoring liên kết definitions và prefab bindings; validator resolve reference/type trước khi bake. Hiện RefactoredUnitAuthoring mới chỉ giữ reference.

## Giới hạn và điều cần tránh

- Không tạo thêm HealthDefinition, JobQueueDefinition asset hoặc bảng quan hệ ID để thay lists đã chốt.
- Không thêm BuildingCategory/AllowedBuildings/AttackMode để duy trì compatibility cũ.
- Không tự suy ra GatherRate là hệ số thời gian; đơn vị/công thức cần rõ trước khi tích hợp.
- Không lấy tài liệu này làm bằng chứng gameplay mới đã hoạt động. Unity import/serialization/baking/PlayMode tests chưa được nghiệm thu.
- **Việc tiếp theo: nghiệm thu validation và hoàn thiện ID tooling R1, rồi baking R2.** Research chỉ dự kiến ghi tech hoàn thành, chưa effect.
- Main/dependencies là phạm vi tích hợp đầu; không mở lại baseline R0. Không tự sửa các scene khác hoặc bật EnemySpawner legacy.




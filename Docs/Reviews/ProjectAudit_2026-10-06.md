# Rà soát kỹ thuật project RTS — 06/10/2026

> Bổ sung từ chủ project: code đã chạy được tới tạo map. Input/UI/SelectManager từng hoạt động ở các phiên bản trước, lần test đã lâu. Các mục liên quan bên dưới là nhận xét tĩnh/rủi ro cần kiểm chứng, không phải xác nhận chức năng hiện tại đang hỏng. Ưu tiên tiếp theo là dọn cấu trúc trước khi sửa logic. Rule ignore Packages đã được sửa trong đợt dọn; manifest/lockfile vẫn cần đưa vào commit cùng các thay đổi được chọn.

## Kết luận và phạm vi

Project biên dịch được ở mức Assembly-CSharp, nhưng chưa đủ bằng chứng để xác nhận bản Player chạy ổn định. Ưu tiên sửa khả năng tái tạo project từ Git, định danh người chơi ở presentation và lỗi xử lý đạn trước khi mở rộng gameplay/multiplayer.

Rà soát trạng thái working tree hiện tại, gồm code gameplay/ECS, input/UI, MapGen/bootstrap, tài liệu, package manifest, Build Settings và log import. Có 201 file C# dưới Assets; đây là rà soát tĩnh có trọng tâm, không phải chứng nhận đã kiểm thử mọi nhánh của từng file. Không sửa code, prefab, scene hay các thay đổi sẵn có. Không thấy AGENTS.md trong kết quả tìm kiếm project.

Kiểm chứng: `dotnet build Assembly-CSharp.csproj --no-restore -v minimal` thành công, **0 error, 5 warning**. Lần đầu bị sandbox chặn đọc Windows SDK; chạy lại ngoài sandbox thành công. Chưa chạy Unity Player build, Play Mode, Burst/AOT hoặc Unity Test Runner. Build từ csproj được Unity sinh không thay thế các kiểm tra đó và có thể không phản ánh file mới chưa được Editor cập nhật vào csproj. File build_results_v2.txt cũ ghi 0 warning không đại diện cho lần kiểm tra này.

P1 = cần xử lý trước mốc tích hợp/build tiếp theo; P2 = lỗi hoặc rủi ro đáng sửa; P3 = vệ sinh/khả năng bảo trì. Phân biệt lỗi xác nhận qua code, dấu vết log và rủi ro chưa tái hiện.

## Các phát hiện ưu tiên

### A01 — P1 — Manifest và lockfile Unity bị Git bỏ qua (xác nhận)

- Vị trí: `.gitignore:199`, `Packages/manifest.json`, `Packages/packages-lock.json`.
- Bằng chứng: `git check-ignore -v` chỉ ra rule `**/[Pp]ackages/*` áp dụng cho cả hai file; `git ls-files Packages` không trả file nào.
- Hậu quả: checkout sạch không mang theo dependency contract của project; không thể đảm bảo cùng Entities, Physics, URP và Input System như máy hiện tại. Cache Library đang tồn tại có thể che khuất vấn đề.
- Xử lý: sửa rule NuGet để không bỏ qua thư mục Unity Packages; track manifest và lockfile. Không track Library/PackageCache.
- Nghiệm thu: clone vào thư mục sạch, dùng đúng Unity 6000.3.13f1, resolve package và build được mà không sao chép Library từ máy cũ.

### A02 — P1 — Input/UI phụ thuộc authoring trong SubScene (xác nhận code; chưa tái hiện Player)

- Vị trí: `Assets/_Project/Scripts/Runtime/Presentation/Input/GameManager/Manager/SelectManager.cs:25,91,114`; `MonoBehaviours/UnitController.cs:132`; `UI/UnitImage.cs:16` cùng cây presentation.
- `FindAnyObjectByType<PlayerContextAuthoring>()` được gọi một lần rồi dereference `currentContext.playerId`. Khi authoring không có trong scene runtime, hoặc SubScene đóng, context có thể null. Khi có nhiều authoring, kết quả tìm không thể hiện người chơi cục bộ mà người dùng đã chọn.
- Hậu quả: chọn quân/ra lệnh/UI có thể lỗi hoặc sử dụng sai player. `PlayerContextAuthoring.cs:10` còn mặc định ID 0, trong khi fixture Main mô tả roster 1–4.
- Xử lý: tạo nguồn LocalPlayerId độc lập, bind bằng roster/session; UI đọc snapshot ECS theo ID. Chỉ bật input khi world và các singleton cần thiết đã sẵn sàng.
- Nghiệm thu: chạy Main với SubScene đóng, build Player, đổi người chơi local và kiểm tra không chọn/điều khiển quân đối thủ.

### A03 — P1 — Đạn có thể dao động qua mục tiêu mà không hit (xác nhận bằng logic)

- Vị trí: `Assets/_Project/Scripts/Runtime/Simulation/Combat/BulletMoverSystem.cs:75–88`.
- Code di chuyển cả bước `speed * dt`, rồi chỉ snap nếu khoảng cách sau lớn hơn khoảng cách trước. Vượt qua mục tiêu nhưng vẫn gần hơn không được snap.
- Ví dụ tái hiện toán học: khoảng cách 0.6, bước 1.0, ngưỡng hit 0.2. Đạn đi qua còn cách 0.4, không hit; frame tiếp theo đi ngược còn cách 0.6 rồi snap. Với trường hợp khoảng cách 0.5 và bước 1.0, khoảng cách trước/sau bằng nhau nên có thể dao động mãi giữa hai phía ngoài ngưỡng hit.
- Xử lý: giới hạn bước theo khoảng cách còn lại, hoặc xét giao đoạn di chuyển với vùng hit. Xác định rõ thiết kế homing/collision.
- Nghiệm thu: kiểm tra bước nhỏ/lớn/bằng khoảng cách, bước bằng hai lần khoảng cách, mục tiêu di chuyển và frame dt lớn; mỗi projectile chỉ gây damage một lần.

### A04 — P2 — Resource cache ghi vượt length và bỏ sót thay đổi cấu trúc (xác nhận)

- Vị trí: `Assets/_Project/Scripts/Runtime/Simulation/Players/PlayerContextSyncSystem.cs:95–108`, `SyncResource:84–92`.
- Nhánh `i >= cacheBuffer.Length` gọi `SyncResource`, nhưng hàm đó vẫn ghi `buffer[index]` mà không resize/add. Điều kiện phát hiện mismatch tự dẫn tới truy cập ngoài giới hạn.
- Chỉ so Amount, không so Type; không nhận biết buffer ngắn đi. Đổi resource type với cùng Amount hoặc xóa entry có thể không cập nhật UI.
- Cache được cập nhật trước khi kiểm tra channel có tồn tại; nếu phát event thất bại, thay đổi đã bị đánh dấu là đồng bộ và có thể không được phát lại.
- Xử lý: so length/type/amount; thay cache đầy đủ sau khi phát event thành công, hoặc dùng dirty version có retry.
- Nghiệm thu: thêm/xóa/reorder loại resource; thay Type giữ Amount; thiếu channel rồi khôi phục channel.

### A05 — P2 — Startup input truy cập world/singleton trước khi sẵn sàng (xác nhận thiếu guard)

- Vị trí: `SelectManager.cs:40–42,86,109,153–158`.
- `World.DefaultGameObjectInjectionWorld`, `Mouse.current` và các singleton selection/physics được dùng trực tiếp. Scene load bất đồng bộ, world teardown hoặc môi trường không có mouse có thể làm đường xử lý lỗi.
- Mỗi phép quy đổi điểm gọi tạo query và lấy physics singleton; click/drag gọi nhiều lần cùng một frame.
- Xử lý: lifecycle binding, query cache do object sở hữu và dispose; dùng một physics snapshot cho một thao tác input; trạng thái Ready rõ ràng.
- Nghiệm thu: click ngay khi tải scene; unload/reload scene; world teardown; không phát exception và không tạo request khi chưa Ready.

### A06 — P2 — Match lifecycle chưa khóa đồng bộ toàn gameplay (rủi ro kiến trúc)

- Vị trí: `System/Construction/ProductionSystem.cs:12`, `System/Construction/BuildingPlacementSystem.cs:18`, `System/Economy/WorkerGatherSystem.cs:13`, `System/Combat/ShootAttackSystem.cs:10–16` dưới cây simulation.
- Production/placement có gate bootstrap; gather/shoot không có gate tương ứng. Không thấy MatchPlaying/MatchState trong lần tìm code. Bootstrap Ready cũng không đại diện cho handshake trận multiplayer.
- Hậu quả: entity đặt sẵn có thể hoạt động trước khi khởi tạo hoàn tất; tương lai multiplayer thiếu hợp đồng pause/start/end thống nhất. Đây là backlog đã được tài liệu Sprint0 nêu, không phải lỗi networking đã tái hiện.
- Xử lý: nhóm gameplay với lifecycle rõ ràng; tách BootstrapReady khỏi MatchPlaying. Định nghĩa phạm vi áp dụng cho movement, economy, combat, production và lệnh.
- Nghiệm thu: chưa Ready thì không thay đổi tài nguyên/HP/production; Ready nhưng chưa start vẫn đứng yên; start/pause/end có hành vi thống nhất.

### A07 — P2 — Player Build đang chọn SampleScene (cấu hình xác nhận; ý định cần đối chiếu)

- Vị trí: `ProjectSettings/EditorBuildSettings.asset:8–10`.
- Chỉ thấy SampleScene bật trong cấu hình này, trong khi `Assets/_Project/Tests/Fixtures/MapGeneration/README.md` và smoke script kiểm tra Main.
- Rủi ro: build bằng cấu hình mặc định có thể mở scene khác scene đã nghiệm thu. Chưa kiểm tra cấu hình Build Profile riêng có override hay không.
- Xử lý: ghi rõ scene khởi động chuẩn, đối chiếu Build Profile đang dùng và đưa scene đó vào quy trình build.
- Nghiệm thu: build thực tế mở đúng scene và bootstrap đúng roster/config.

### A08 — P2 — Cần xác minh destroy LinkedEntityGroup trong death system (rủi ro)

- Vị trí: `System/Combat/HealthDeadTestSystem.cs:95–107`.
- Code duyệt group và enqueue destroy từng entity, có thể bao gồm root. Cancellation lại destroy root một lần (`BuildingCancelSystem.cs:79–80`). Hai luồng cần có cùng hợp đồng với Entities 1.4.5, cleanup components và cấu trúc prefab.
- Tài liệu trong source package của `EntityCommandBuffer.DestroyEntity` cảnh báo playback trên entity đã destroy sẽ lỗi. Chưa chạy fixture để xác nhận thứ tự/group cụ thể gây double destroy; không kết luận đây là lỗi runtime đã tái hiện.
- Xử lý: kiểm tra behavior thực tế của package và chuẩn hóa đường destroy; kiểm tra group lồng nhau, root/child cùng chết và cleanup PopulationAccount/flow-field/blockage.
- Nghiệm thu: không exception ECB, không child mồ côi, population/cost grid/cache trở lại đúng sau chết/hủy.

## Cảnh báo và clean code

### A09 — P2/P3 — Import log có exception cần xử lý riêng

`Logs/AssetImportWorker0.log:5362` và `AssetImportWorker6.log:336` có `UnitAuthoring requires a UnitSO`. Nhiều worker log có `Visual Scripting: couldn't find visual scripting package`. Đây là bằng chứng lỗi từng xảy ra khi import, không chứng minh asset hiện tại vẫn hỏng. Cần xác định asset và import lại để kết luận; compiler C# xanh không xác nhận baking xanh.

### A10 — P3 — 5 warning compiler hiện tại

- `CoreECS/Authoring/Building/Combat/ShootVictimAuthoring.cs:16`: CS0618 dùng AddComponent overload obsolete. Chuyển sang overload nhận Entity rõ ràng.
- `Assets/_Project/Scripts/Runtime/Presentation/UI/InfoIconMapping.cs:42,43,49,50`: bốn CS0649 ở field struct được Unity serialize. Có thể là cảnh báo hợp lệ do compiler không biết dữ liệu Inspector; không coi chúng mặc định là lỗi null. Kiểm tra asset mapping trước khi sửa/suppress có phạm vi.

### A11 — P3 — Runtime source kéo NUnit và thiếu ranh giới assembly

`SelectManager.cs:2`, `GameManager.cs:2`, `InputTracker.cs:2`, `Events/EventBus.cs:1`, `Events/ResourceChangeEvent/ResourceChangeListener.cs:1`, `Assets/_Project/Scripts/Runtime/Presentation/UI/InfoIconMapping.cs:1` có using NUnit trong code runtime. Assembly-CSharp.csproj hiện tham chiếu nunit.framework. Không thấy asmdef riêng dưới Assets trong lần tìm kiếm.

Xóa import không sử dụng và đặt tests trong test assembly rõ ràng; không giả định dependency NUnit này đã làm Player build thất bại vì chưa chạy Player build. Tách runtime/editor/tests khi có nhu cầu dependency cụ thể, tránh refactor thư mục lớn chỉ để làm đẹp.

### A12 — P3 — Quản lý lifetime, cache và chi phí presentation

- `SelectHelper` trong `SelectManager.cs:175,193` tạo Temp NativeArray không dispose rõ ràng, có early return; dùng using và quản lý query ownership. Temp allocator có lifetime ngắn nên không gán nhãn đây là leak persistent đã xác nhận.
- `PlayerContextCacheInitSystem.cs:15,27–45` tạo ECB trước các early return, không dispose ở các nhánh lỗi; dùng using/finally hoặc validate dependency trước khi allocate.
- Resource event tạo List mới khi thay đổi; SelectHelper trả List mới; đo Profiler trước khi đặt mục tiêu tối ưu GC.
- Spatial systems có capacity khởi tạo 10000 và scan entity mỗi tick; chưa đo hiệu năng nên không gọi đây là bottleneck đã xác nhận. Cần đo ở quy mô quân mục tiêu trước khi tối ưu.

### A13 — P3 — Tài liệu và fixture chưa có bằng chứng nghiệm thu đồng nhất

- Sprint0 vẫn đúng về authoring identity; ghi nó là backlog không làm rủi ro biến mất.
- README fixture ghi Play Mode Main chưa test lại sau thay đổi config-only stock. Smoke menu Editor hữu ích nhưng chưa thay thế bộ regression chạy tự động.
- Build report cũ ghi 0 warning khác kết quả hiện tại. Cần gắn report với commit/config/Unity version/thời điểm và loại kiểm tra.
- Assets/_Project/Tests/Fixtures/MapGeneration đang được scene/registry dùng và gitignore có exception cố ý; không xóa thư mục này như cache. Chuyển sang tên Tests/Fixtures chỉ khi kiểm kê reference và giữ meta/GUID.
- Nhiều scene/prefab/code/docs đang modified hoặc untracked. Đây không phải lỗi gameplay, nhưng cần checkpoint toàn bộ file cần thiết để người khác tái tạo được trạng thái đã kiểm thử.

## Thứ tự xử lý và tiêu chí hoàn tất

1. **Tái tạo/build:** sửa ignore Packages, checkpoint source+meta+fixture, xác nhận scene/profile khởi động; clone sạch và Unity build.
2. **Lỗi gameplay rõ ràng:** sửa bullet overshoot và resource cache; thêm regression nhỏ đúng các ví dụ nêu trên.
3. **Lifecycle/input:** local identity độc lập; guard world/singletons và gate gameplay. Test SubScene đóng, reload, roster nhiều player.
4. **Baking/cleanup:** giải quyết asset gây import exception; test destroy group, population, building blockage và flow-field refs.
5. **Vệ sinh:** bỏ import thừa, sửa obsolete API, lifetime rõ ràng, cập nhật docs/test evidence. Chỉ tối ưu performance sau baseline Profiler.

Điều kiện bàn giao nên gồm: Unity import/bake sạch; Player build thành công và vào đúng scene; Play Mode smoke Main với config hiện tại; regression bullet/resource/ownership; clone sạch resolve đúng package; không lỗi ECB hoặc native allocation trong vòng load–play–unload. Không xóa asset/package theo tên “Temp”, “Test” hay “unused” nếu chưa kiểm tra serialized reference/GUID.

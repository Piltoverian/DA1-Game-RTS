# Thiết kế bộ học liệu: từ multiplayer RTS đến FPS Entities có backend

Ngày khảo sát: 03/11/2026. Trạng thái: **người dùng đã duyệt và yêu cầu triển khai**. Trạng thái biên soạn và giới hạn kiểm chứng của giáo trình nằm ở README bộ học liệu; thiết kế này không phải bằng chứng game đã chạy.

## 1. Mục đích và người học

Người học đã biết Unity và ECS cơ bản. Bộ tài liệu bằng tiếng Việt giúp họ hiểu nền tảng game online, các khái niệm và thuật toán cơ bản, cả Netcode for GameObjects (NGO) lẫn Netcode for Entities (NfE), rồi áp dụng một framework backend game.

Hai kết quả người dùng yêu cầu, theo đúng thứ tự:

1. Hiểu và lần theo được luồng multiplayer của project RTS hiện tại.
2. Tự làm FPS 3D dùng **Netcode for Entities**, có backend lưu và hiển thị lịch sử thắng/thua cho player.

Sản phẩm bàn giao của tác vụ này là **bộ tài liệu và hình minh họa**, gồm hướng dẫn thực hành có mã và tiêu chí tự kiểm tra. Không tự biến project RTS thành FPS hoặc bàn giao một game đã build. Người học tạo môi trường thực hành riêng theo hướng dẫn; không cài NGO, backend hoặc sửa gameplay trong project RTS để làm ví dụ.

## 2. Các lựa chọn do người viết đề xuất

Những điểm sau do người viết đề xuất và nằm trong thiết kế được người dùng duyệt; không phải các yêu cầu ban đầu do người dùng tự chỉ định:

- Khoảng **32 bài chính**, chia sáu phần; có mục lục, từ điển thuật ngữ, đáp án gợi ý và thư mục học liệu ngoài.
- Markdown trong `Docs/Learning/OnlineGame/`, đọc trực tiếp trong repo; thêm một mục dẫn vào `Docs/README.md`. Không làm website, PDF hoặc slide trong phạm vi này.
- **Nakama** cho tài khoản, session, storage và lịch sử trận; TypeScript cho server runtime mở rộng của Nakama. Dạy các khái niệm TypeScript/Docker cần dùng ngay tại bài thực hành, không giả định người học đã biết backend.
- Unity dedicated server mô phỏng FPS. Nakama không chạy thêm một bản mô phỏng FPS song song.
- FPS mẫu: arena graybox **1v1**, một súng hitscan, đi/ngắm/bắn, máu, chết/respawn, điểm và kết thúc trận. Có camera góc nhìn thứ nhất; đồ họa dùng primitive để tập trung vào mạng.
- Quy tắc bài tập: đạt 5 kill hoặc hết 3 phút; bằng điểm là hòa. Khi transport xác nhận một người disconnect và đối thủ còn kết nối, người rời chịu thua; cả hai vắng trước khi chốt hoặc server bị dừng thì trận bị hủy, không tự tính thắng. Reconnect đầy đủ giữa trận là bài mở rộng, không phải điều kiện hoàn thành FPS tối thiểu.
- Chạy local với một dedicated server và hai client trước. Endpoint server được cấu hình; matchmaking/lobby/routing được giải thích và đặt đúng vị trí, chưa triển khai hệ thống cấp phát server production.

## 3. Cấu trúc kiến thức và thứ tự học

```text
Nền tảng mạng + thuật toán bằng ví dụ nhỏ
    → đọc trọn flow RTS từ source
    → thực hành NGO để đối chiếu khái niệm
    → NfE: input theo tick, Ghost và prediction
    → FPS 3D với authority phía server
    → Nakama: identity, admission và lịch sử trận
    → kiểm tra toàn tuyến, mạng xấu và lỗi backend
```

Phần nền tảng đủ để đọc RTS; các chi tiết prediction/rewind được học lại bằng API cụ thể khi làm FPS. Phần backend có thể đọc giới thiệu sớm, nhưng chỉ ghép sau khi gameplay hai client đã đúng. Mỗi bài chỉ rõ điều kiện đầu vào và bài cần đọc trước.

| Phần | Bài | Chủ đề và đầu ra |
|---|---|---|
| A — Nền tảng | 01 | Bản đồ game online: client, host, dedicated server, backend; process khác World như thế nào. |
| A | 02 | Packet, TCP/UDP, reliability, ordering, loss, NAT, Relay và HTTP/WebSocket; chọn theo mục đích, không theo khẩu hiệu. |
| A | 03 | Tick, frame, snapshot rate, RTT, jitter, clock; tính một ví dụ timeline và băng thông. |
| A | 04 | State, event, RPC, input/command, serialization, quantization, delta và interest management. |
| A | 05 | Snapshot interpolation, buffer, extrapolation; ví dụ tính vị trí giữa hai snapshot và khi thiếu mẫu. |
| A | 06 | Prediction/reconciliation: input có số thứ tự, ACK, bỏ input cũ và replay; ví dụ số từng bước. |
| A | 07 | Lag compensation, history/ring buffer, lockstep, determinism và rollback; so sánh điều kiện áp dụng và đánh đổi công bằng. |
| B — Đọc project RTS | 08 | Bản đồ source, process/World, bootstrap, scene và dữ liệu player/match. |
| B | 09 | Connect → join → identity; `NetworkId`, `PlayerId`, session, join generation và terrain epoch. |
| B | 10 | Ghost/terrain/memory → manifest → ready → Running; tìm client đang chờ bước nào. |
| B | 11 | Theo một lệnh Move từ input đến RPC, validation, queue, simulation và Ghost; mở rộng sang build/train/cancel. |
| B | 12 | Ownership, relevancy, FOW và neutral memory; ai được nhận dữ liệu nào và vì sao ẩn renderer chưa đủ. |
| B | 13 | Disconnect/rejoin, vé resume, lifetime và kiểm chứng; phân biệt fixture ECS với hai process thật. |
| C — NGO | 14 | Môi trường NGO riêng: NetworkManager, NetworkObject, spawn và ownership. |
| C | 15 | RPC, NetworkVariable, authority và late join; ví dụ một hành động được server xác nhận. |
| C | 16 | Di chuyển, interpolation và anticipation; hiểu giới hạn so với prediction/reconciliation đầy đủ. |
| C | 17 | Bảng đối chiếu NGO ↔ NfE và bài tập cùng một mechanic; các API không ánh xạ một-một. |
| D — NfE | 18 | Client/Server World, baking, SubScene, Ghost prefab và collection; xuất phát từ ví dụ cube đang có. |
| D | 19 | Ghost fields, snapshot, owner, predicted/interpolated mode và relevancy. |
| D | 20 | `IInputComponentData`, input theo tick, command stream, nút bấm và sự kiện; khác RPC lệnh RTS. |
| D | 21 | `PredictedSimulationSystemGroup`, `Simulate`, rollback/resimulation, smoothing và side effect chạy lặp. |
| D | 22 | Physics/history, thứ tự system, disconnect và công cụ debug mạng; dựng thí nghiệm nhỏ trước FPS. |
| E — FPS 3D Entities | 23 | Tạo arena, player Ghost, controller đơn giản, camera local và di chuyển predicted; remote player interpolated. |
| E | 24 | Ngắm/bắn, fire input, cooldown/ammo, server raycast và damage; thêm rewind có giới hạn và kiểm tra tick. |
| E | 25 | Health, death, respawn, score và VFX; không nhân đôi damage/VFX khi replay. |
| E | 26 | State machine trận, điều kiện thắng/hòa/hủy, disconnect và một kết quả cuối do server chốt. |
| E | 27 | Build/chạy dedicated server + hai client, đo hành vi dưới latency/jitter/loss và sửa lỗi thường gặp. |
| F — Backend và ghép toàn tuyến | 28 | Nakama local và Unity SDK: đăng nhập, session/refresh, HTTP, runtime RPC và storage. |
| F | 29 | Account → admission ticket → connection → player entity; endpoint/routing, roster và vai trò matchmaking/lobby. |
| F | 30 | Hợp đồng kết quả, trusted server RPC, validation, quyền storage, batch/create-only và idempotency. |
| F | 31 | Đọc lịch sử của chính player: phân trang, sắp xếp, UI loading/empty/error, đăng nhập lại vẫn thấy kết quả. |
| F | 32 | Kiểm tra end-to-end, retry/backoff khi backend lỗi, phạm vi bảo đảm khi process chết và bước đi tiếp khi deploy. |

Các bài 23–32 là chuỗi thực hành có cùng namespace, kiểu dữ liệu và hợp đồng xuyên suốt; không phải mười ví dụ rời không ghép được. Không giả định NGO có sẵn trong project hiện tại.

## 4. Quy cách từng bài

Mỗi bài phải dạy được một việc cụ thể, không chỉ liệt kê định nghĩa:

1. Mục tiêu và kiến thức cần có; giải thích thuật ngữ tiếng Anh ngay lần đầu.
2. Một tình huống gần game RTS/FPS, hình minh họa và ví dụ số hoặc diễn biến có thể lần theo.
3. Giải thích cơ chế/thuật toán rồi mới ánh xạ sang API Unity/Nakama.
4. Khi liên quan RTS: link tới source và symbol thực tế, trình bày input → validation → đổi state → output.
5. Khi là lab: môi trường, đường dẫn file cần tạo, mã theo bước, cấu hình Editor/prefab, cách chạy, kết quả mong đợi và lỗi thường gặp. Đoạn trích phải chỉ rõ phần bị lược và nơi lấy phần còn lại; không gắn nhãn chạy được cho pseudocode.
6. Bài tập quan sát/thay đổi có mục tiêu, 2–4 câu hỏi tự kiểm tra và gợi ý đáp án ở phụ lục.
7. Nguồn đọc thêm đặt sát chủ đề, ghi rõ nên đọc phần nào và phiên bản liên quan.

Độ dài theo nội dung: bài nền tảng thường khoảng 800–1.500 từ; bài lab dài hơn khi cần mã và thao tác. Đây là hướng dẫn biên tập, không phải chỉ tiêu để kéo dài bài. Tài liệu tự giải thích kiến thức cần thiết, không buộc người học đọc hết nguồn tiếng Anh mới làm được bài.

## 5. Hình minh họa và khả năng đọc

- Mỗi bài chính có ít nhất một hình phục vụ nội dung: sơ đồ luồng, sequence, state machine, timeline, bảng vị trí hoặc bố cục scene/UI.
- Vẽ lại bằng ví dụ riêng, nhãn tiếng Việt; giữ tên API nguyên bản. Không chép hình hoặc dịch nguyên bài của nguồn ngoài.
- PNG nhúng trong Markdown để xem thuận tiện; SVG là nguồn chỉnh sửa bàn giao. Hai định dạng này là kết quả sử dụng lâu dài, không phải artifact kiểm tra tạm.
- Dùng nhất quán client A/client B/server/backend, chú giải màu và mũi tên. Không dựa chỉ vào màu để phân biệt trạng thái; kèm alt text và diễn giải bằng chữ.
- Các hình bắt buộc: process/World; join/ready RTS; vòng đời Move; FOW và quyền nhận; interpolation; replay input; hitscan rewind; account/connection; ghi kết quả và retry; màn hình lịch sử trận.
- Không tạo ảnh trang trí thay cho sơ đồ kỹ thuật. Kiểm tra render, chữ có dấu, độ tương phản và cắt chữ ở kích thước đọc thực tế.

## 6. Ranh giới kỹ thuật phải dạy đúng

### Unity và protocol riêng của RTS

Unity cung cấp connection, `NetworkId`, RPC/Ghost, ownership/relevancy và framework input/prediction. Project tự xây admission, manifest/ready, persistent slot, vé resume, terrain streaming, memory, command receipt/quota/dedup. Không giới thiệu các cơ chế tự xây như API bắt buộc của Unity.

`NetworkStreamInGame` chưa đồng nghĩa trận Running. Backend session, match session và join generation là ba phạm vi khác nhau. Snapshot bộ nhớ do ứng dụng gửi RPC không phải cùng cơ chế với Ghost snapshot.

Source RTS chính dùng adapter RPC cho lệnh chiến thuật. Ví dụ cube riêng có `IInputComponentData` và predicted simulation; dùng nó làm cầu nối, không mô tả RTS hiện có như một FPS đã có prediction/rewind. Không mang terrain/FOW handshake nặng hoặc RPC mỗi bước di chuyển sang FPS theo thói quen.

### FPS và backend

```text
Unity client → Nakama: đăng nhập → session
Unity client → backend: xin admission cho trận được cấp
Unity dedicated server → kiểm tra admission: user ID + match + hạn dùng + chống dùng lại
Unity client → Unity server: input theo tick
Unity server → validate → mô phỏng → Ghost tới client
Unity server → chốt kết quả → Nakama RPC chỉ dành cho trusted server
Nakama → validate match/roster/payload + ghi idempotent → history
Unity client → API theo session của chính mình → history UI
```

- Nakama user ID phải được xác thực rồi mới gắn connection/player entity. Client tự khai user ID không phải xác thực; không nhúng credential server vào client.
- Lab admission phải có hợp đồng và mã minh họa cụ thể, không bỏ qua khoảng nối giữa login SDK và NfE. Có thể dùng ticket ngẫu nhiên hạn ngắn được backend giữ, server đổi/tiêu thụ qua RPC trusted; validation và consume phải chống race. Chi tiết API được chốt từ tài liệu runtime khi viết bài.
- Unity server xác nhận hit/damage/score. Client gửi input, không gửi lời khai thắng hoặc sát thương để được tin trực tiếp.
- Backend dùng RPC dành cho server theo hướng dẫn headless chính thức: credential cấu hình ở server, từ chối player-context caller, kiểm tra match/roster và trạng thái đã kết thúc. Phân biệt HTTP runtime key với client server key của SDK; chỉ môi trường local dùng HTTP, triển khai qua mạng cần TLS và bảo vệ log credential.
- Kết quả có `schemaVersion`, `matchId`, roster/user ID, score, outcome, lý do kết thúc và thời gian do server quản lý. Một match có một kết quả chuẩn; retry cùng payload trả kết quả cũ, payload mâu thuẫn bị từ chối.
- History server-write-only, chỉ đọc dữ liệu được phép theo session. Không tin `userId` tùy ý trong request đọc; không dùng thứ tự mặc định của storage làm bằng chứng lịch sử đã sắp theo thời gian.
- Chống ghi trùng bằng create-only/conditional write và batch nguyên tử theo API đã kiểm chứng; không dùng check-exists rồi write như một khóa. Tránh cộng counter ngoài cùng đơn vị ghi kết quả; phiên bản tối thiểu có thể suy thống kê từ history.
- Giải thích retry/backoff, idempotency và outbox; bản lab cần nêu rõ có/không giữ hàng đợi qua restart. Không hứa giao đúng một lần hoặc khôi phục trận sau server crash nếu chưa có persistence tương ứng.
- Prediction có thể chạy lại nhiều lần trong một frame. Tách simulation replay khỏi VFX/audio/backend writes; kiểm tra fire tick/cadence/ammo và giới hạn history, không cho client rewind tùy ý.

## 7. Source và học liệu đã khảo sát

### Project

Phiên bản lấy từ `Packages/packages-lock.json`, không chỉ manifest: Unity `6000.3.13f1`, Entities `1.5.0`, NfE `1.14.3`, Transport `2.7.4`, Physics `1.4.5`. NGO chưa được cài; bài NGO ghim nhánh tài liệu và package riêng khi biên soạn, không nâng package RTS.

Các entry point đã đọc/đối chiếu: `CustomBootstrap.cs`, `ConnectionUI.cs`, `CubeInputAuthoring.cs`; nhóm `Players` gồm bootstrap, connection, handshake, ownership, relevancy, command RPC, session slots và resume store; terrain server/receiver; neutral memory; `Shared/MatchState/MatchlLifeCycleSystem.cs`; `NetworkingProtocolChecks.cs`.

Tài liệu kiến trúc tại `Docs/Multiplayer/` vẫn là nguồn cập nhật implementation chính. Giáo trình diễn giải và dẫn tới nguồn đó, không tạo một bản CurrentState thứ hai. Lobby/matchmaking/ban-pick/driver migration hiện là dự kiến; không viết thành chức năng đã hoàn tất. Trong lúc khảo sát phát hiện một số đường dẫn/mô tả lifecycle cũ ở `Docs/ProjectStructure.md`; bài học phải dùng source thật, không nhân bản sai lệch đó. Việc sửa tài liệu kiến trúc ngoài mục dẫn giáo trình cần giữ phạm vi riêng.

### Nguồn bên ngoài

- [Unity NfE 1.14 — Prediction](https://docs.unity3d.com/Packages/com.unity.netcode@1.14/manual/prediction.html) và [prediction trong NfE](https://docs.unity3d.com/Packages/com.unity.netcode@1.14/manual/prediction-n4e.html): API đúng nhánh package và thuật toán replay.
- [IInputComponentData](https://docs.unity3d.com/Packages/com.unity.netcode@1.14/api/Unity.NetCode.IInputComponentData.html), [PhysicsWorldHistorySingleton](https://docs.unity3d.com/Packages/com.unity.netcode@1.14/api/Unity.NetCode.PhysicsWorldHistorySingleton.html): input theo tick và collision history cho FPS.
- [Unity Netcode Samples](https://github.com/Unity-Technologies/EntityComponentSystemSamples/tree/master/NetcodeSamples): ví dụ nhỏ để đối chiếu; ghim revision tương thích nếu trích API, không mặc định nhánh master cùng version project.
- [NGO 2.7 — Client anticipation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/advanced-topics/client-anticipation.html): phân biệt anticipation với prediction/reconciliation loop; không hứa hai package có cùng khả năng sẵn có.
- [Gambetta — Prediction/reconciliation](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html), [lag compensation](https://www.gabrielgambetta.com/lag-compensation.html), [Gaffer — Snapshot interpolation](https://gafferongames.com/post/snapshot_interpolation/): giải thích thuật toán bằng ví dụ riêng trước API.
- [Nakama Unity SDK](https://heroiclabs.com/docs/nakama/client-libraries/unity/), [headless server authentication](https://heroiclabs.com/docs/nakama/guides/concepts/headless-server-auth/), [storage collections](https://heroiclabs.com/docs/nakama/concepts/storage/collections/) và [storage permissions](https://heroiclabs.com/docs/nakama/concepts/storage/permissions/): đăng nhập, trusted RPC, conditional/batch writes và quyền dữ liệu.

Không kết luận Nakama là framework phổ biến nhất hoặc các mẫu trên đã chạy trong project. Nguồn ngoài được chọn theo độ phù hợp và tính chính thống; ghi ngày kiểm tra, phiên bản và phần nên đọc ở thư mục học liệu.

## 8. Phạm vi file và pipeline biên soạn

Sau khi thiết kế được duyệt:

1. Chốt mục lục và ma trận bài → source → nguồn ngoài → hình → bài tập; đối chiếu version/API.
2. Viết phần nền tảng và walkthrough RTS, kiểm tra mỗi diễn biến theo source hiện tại.
3. Viết NGO/NfE và chuỗi lab FPS/backend với một hợp đồng dữ liệu nhất quán; bổ sung hướng dẫn cài và file listing đủ để người học tái tạo.
4. Vẽ/render hình, gắn vào bài, thêm glossary/đáp án/nguồn và điều hướng trước/sau.
5. Rà soát kỹ thuật, link, hình, tính tự học và trạng thái kiểm chứng; sửa lỗi trong phạm vi giáo trình.

File được phép tạo/sửa: bộ `Docs/Learning/OnlineGame/**`, mục dẫn trong `Docs/README.md` và thiết kế này. Không sửa runtime, scene, prefab, package, backend thật hoặc dữ liệu người dùng. Không tự commit/push. Không xóa hoặc ghi đè các thay đổi working tree có từ trước.

## 9. Tiêu chí hoàn thành và giới hạn kiểm chứng

- Có đủ 32 bài với mục tiêu, diễn giải, hình, ví dụ, bài tập và nguồn phù hợp; README nêu hai đích học và thứ tự đọc.
- Người học lần được join → ready → command → Ghost/FOW → rejoin trong source; giải thích đúng phần Unity và protocol riêng.
- Chuỗi FPS/backend có hướng dẫn tái tạo từ môi trường sạch: player không điều khiển người khác; server quyết định damage/kết quả; history còn sau khi mở lại client; retry không thêm trận; client không sửa kết quả; đọc lịch sử không lộ dữ liệu riêng của người khác.
- Có ma trận tự kiểm tra cho happy path, join chưa sẵn sàng, loss/jitter, bắn lặp/replay, disconnect, ticket sai/hết hạn/dùng lại, session hết hạn, backend timeout, duplicate/conflicting result và phân trang history.
- Kiểm tra toàn bộ link nội bộ/ảnh/source path, code fences và tên API; render xem tất cả hình bàn giao. Đoạn lấy từ project phải đúng source đang có; snippet mới ghi rõ phiên bản, dependencies và mức đã kiểm chứng.
- Tác vụ viết tài liệu không tự chứng minh toàn bộ lab đã compile/chạy. Chỉ công bố compile, Unity Play Mode, hai process hay backend integration khi thực sự chạy; nếu chưa chạy phải ghi cụ thể ngay ở bài liên quan và báo khi bàn giao. PASS lịch sử trong docs RTS không nghiệm thu code minh họa mới.
- Không mở rộng sang MMO, ranked MMR, fleet orchestration, nhiều loại vũ khí, chống cheat hoàn chỉnh, host migration, art hoàn chỉnh hoặc deploy dịch vụ trả phí. Các hướng đó được giới thiệu và có học liệu tiếp, không nhận là sản phẩm lab.
- Dọn script/log/ảnh thử dùng một lần. Giữ bài học, hình sử dụng trong bài và source hình; không giữ receipt kiểm tra chỉ để chứng minh công việc.

## 10. Phần người biên soạn tự quyết

Tên file, bố cục chi tiết, ví dụ số, màu sơ đồ, cách chia tiểu mục, API Nakama cụ thể đáp ứng các hợp đồng đã nêu và phiên bản NGO/Nakama tương thích được người biên soạn chốt sau khi đối chiếu nguồn chính thức. Ghi rõ quyết định phiên bản trong README; không tự thay đổi FPS Entities, hai đích học hoặc ranh giới quyền ghi kết quả.

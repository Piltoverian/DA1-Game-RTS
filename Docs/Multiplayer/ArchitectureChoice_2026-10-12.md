# Lựa chọn chơi nhiều người theo tải RTS đã chốt

## Quyết định của tác nhân theo ủy quyền

Chọn **máy chủ quyết định trạng thái + ảnh trạng thái/phần thay đổi đồng bộ trạng thái + nội suy phía máy khách**, dùng ECS và Netcode for thực thể làm hướng tích hợp. máy chủ là nơi quyết định chuyển động, collision/blockage, sát thương, economy, sản xuất và nghiên cứu. máy khách gửi lệnh, nhận trạng thái và dựng hình. Cập nhật 17/10/2026: client presentation/input/UI/camera dùng Mono qua adapter ECS/Netcode; Ghost authoring và serializer quyết định interpolation, không tạo tầng nội suy song song. Quyết định dữ liệu/RPC/FOW mới nhất ở [Multiplayer](README.md).

Mục tiêu người dùng: khoảng 20 tick/s, 2.000 lính tổng toàn trận, bản đồ tối đa 512 ×512, 2–10 người chơi. Địa hình cố định, động chặn lối thay đổi; lối chơi trên XZ; camera thu phóng, không xoay; không triển khai đội hình.

Ưu tiên **chuyên dụng máy chủ** làm môi trường nghiệm thu tải. Khi phát triển có thể chạy máy chủ+máy khách trên một máy hoặc máy chủ kiêm máy chơi kiêm máy khách, nhưng mô phỏng authority/command contracts giữ chung. Listen máy chủ kiêm máy chơi không chứng minh phần cứng máy chủ đạt tải, và máy chủ kiêm máy chơi phải gánh cả dựng hình lẫn mô phỏng. Chưa chọn dịch vụ hosting, chưa cấp tài nguyên trả phí.

Đây là lựa chọn kiến trúc, không phải đo hiệu năng chứng minh đạt 2.000 lính. Package danh mục hiện có Entities/Physics nhưng chưa có Netcode for thực thể hay lớp truyền dữ liệu. Không tự thêm phiên bản package khi chưa kiểm tra compatibility và chưa có kế hoạch tích hợp riêng.

## Vì sao phù hợp

Unity xác định Netcode for thực thể là giải pháp máy chủ quyết định trạng thái cho DOTS và xử lý logic chơi nhiều người phức tạp: [Unity netcode packages](https://docs.unity.com/en-us/multiplayer/netcode/netcode). Client/server thế giới ECS là ranh giới có sẵn của ECS/Netcode: [Client/server worlds](https://docs.unity3d.com/Packages/com.unity.netcode@1.6/manual/client-server-worlds.html).

mã hiện dùng float, physics và native mã băm maps, chưa có kiểm chứng mô phỏng deterministic trên nhiều máy. Vì vậy không chọn lockstep ở giai đoạn này: nó đòi hỏi chứng minh tính deterministic cho toàn lối chơi, RNG, thứ tự xử lý và sai số, chứ không chỉ có bước cố định bước thời gian. Không kết luận rằng lockstep không thể làm; đó là một migration lớn hơn mốc so sánh authoritative hiện tại.

máy khách interpolation phù hợp các lính RTS không được điều khiển liên tục kiểu nhân vật hành động. Đề xuất ban đầu không prediction/rollback 2.000 lính trên máy khách; phản hồi chọn unit/marker ngay, hành vi authoritative theo lệnh ack và snapshots. Đây là quyết định của tác nhân, chưa phải trải nghiệm đã nghiệm thu.

## Bước mô phỏng, lệnh và dữ liệu

- máy chủ mô phỏng cố định 20 Hz: ngân sách trung bình 50 ms/tick. lối chơi hệ thống phải dùng bước mô phỏng phần thay đổi thống nhất; chỉ đặt chuyển động trong FixedStep không tự chuyển production/combat/economy thành 20 Hz. Đề xuất ngân sách đo p95/p99, tránh sustained bước mô phỏng backlog; ngưỡng cụ thể cần chốt sau khi có máy máy chủ mốc so sánh.
- ảnh trạng thái bắt đầu với 10 Hz như cấu hình lượt thử nhỏ của tác nhân, đo lại latency/bandwidth để quyết định 10–20 Hz. Render máy khách độc lập simulation/network bước mô phỏng, không giới hạn FPS máy khách về 20.
- lệnh gồm match/session identity, authenticated người chơi, sequence/tick, lính IDs, action và mục tiêu. máy chủ phân giải ID → thực thể và kiểm tra owner/team/capability/resources. PlayerID của gói không được tự coi là danh tính đã xác thực.
- Group lệnh cùng đích dùng target/field chia sẻ; không tạo một mục tiêu+trường hướng cho mỗi lính để dựng đội hình. Mỗi bước mô phỏng có ngân sách trường requests và thống kê hàng đợi depth/cache hit/build time.
- địa hình ảnh trạng thái gửi lúc loading: algorithm/config version, lưới, cells/spawns, revision/hash. máy chủ sinh bản đồ; không yêu cầu máy khách nhiễu float tạo lại cùng kết quả trên mọi máy. máy khách dựng visuals từ ảnh trạng thái. động vật chặn lifetime/cost và lính trạng thái được replicate riêng.
- Unit trạng thái tối thiểu là ID/owner/team, XZ/orientation, state/HP và thông tin cần dựng hình. trường hướng trường và ORCA internals ở máy chủ. Resource/research/job là dữ liệu gameplay cần phục hồi khi reconnect; snapshot render chỉ có ability/job đang active, không mặc định gửi toàn queue.
- AOE/target validity đọc quan hệ đội ở mô phỏng. Same PlayerID không đủ để bảo vệ người chơi đồng minh; quyền sở hữu vẫn riêng cho mỗi người chơi.

Ước lượng minh họa, không phải số đo: nếu mỗi lính cần 16 byte và gửi toàn bộ 2.000 lính ở 10 Hz thì 320 kB/s/client, khoảng 3,2 MB/s cho 10 máy khách, chưa tính header/reliability. phần thay đổi, quantization, mức liên quan và event đồng bộ trạng thái cần được đo; không mặc định full-trạng thái broadcast cho toàn trận. Camera loại phần không nhìn thấy không thay thế sương mù-of-war hoặc quyền nhìn dữ liệu của người chơi.

## Môi trường cần chuẩn bị và điều kiện đạt

| Môi trường | Mục đích | Điều kiện kiểm tra |
|---|---|---|
| cục bộ mô phỏng Editor | kiểm tra hồi quy nhanh, hạt giống và hợp đồng lối chơi | Ownership/payment, đặt nhà nền phẳng, động chặn lối, hàng đang mang rules, AOE đội |
| máy chủ thế giới ECS không hiển thị | Kiểm chứng ranh giới quyền điều khiển | Không Camera.main/TerrainVisualSurface/MonoBehaviour/EventBus/SO graphics bắt buộc; xử lý logic hoạt động khi không bộ dựng hình |
| Dedicated máy chủ bản dựng headless | CPU/tick và vòng đời thực tế | 2.000 lính, 512 ×512, 2/5/10 người chơi; commands đồng thời, movement/chokepoint/combat/economy; bước mô phỏng p50/p95/p99, backlog, memory |
| Một máy chủ + 2 máy khách | End-to-end đầu tiên | Authenticated commands, ack/order/dedup, owner/team, spawn/death, bản đồ mã băm, interpolation và disconnect |
| Một máy chủ + 10 client/bot | Tải networking | CPU serialize, bytes/client, hàng đợi, mất gói, kết nối lại và đồng bộ lại; lối chơi cùng authoritative truth |
| máy khách graphics ở nhiều thu phóng | Hình ảnh/gameplay projection | Unit/cliff che khuất tự nhiên, chọn bằng tia đúng vị trí, thu phóng không đổi góc quay ngang/góc nghiêng, không còn flat mode |

Các môi trường ngoài cục bộ Editor ở bảng này là kế hoạch chuẩn bị, **chưa được dựng hoặc chạy** trong lượt dọn dẹp. Mục tiêu chơi nhiều người ban đầu được ghi nhận; không coi kiểm tra nhanh cục bộ là máy chủ kiểm tra hợp lệ.

## Gaps phải xử lý trước khi dùng môi trường mạng

1. TerrainHeightCacheSystem hiện đã lấy height từ grid simulation, không phụ thuộc renderer. FixedTerrainHeightSystem/TerrainHeightSystem hiện chỉ áp Y ở local/client; khi chuyển server phải chốt nơi áp height cho collider/projectile và projection/picking, không tùy tiện ép mọi entity Y=0. Client terrain phải giữ cùng TerrainBaseLevel để hình không lệch simulation.
2. Gắn WorldSystemFilter phù hợp cho gameplay/presentation; bỏ mặc định-world/static được quản lý phụ thuộc và dùng explicit match lifecycle/tick. máy khách không tự chạy lại economy/damage/production.
3. Ownership/payment đã có backend; kiểm chứng lại hợp đồng trong máy chủ thế giới ECS. Team relationship/AOE và vòng đời bên sản xuất death/destruction cần nghiệm thu trong môi trường đó, giữ chính sách hoàn tiền hiện hành.
4. Ghost identity/serialization, RPC giữ command payload hiện tại, tải trận và reconnect theo [hướng 17/10](README.md). Frame FOW không mang WorldId/MapId/MapRevision; không tự dựng lại protocol nội bộ Netcode. Chọn package tương thích trước cài đặt.
5. Chạy đo hiệu năng tải và kiểm thử mạng adverse conditions; chưa chọn máy/dịch vụ bằng suy đoán FPS Editor.

Quyết định này thay mốc so sánh host/Relay của lộ trình cũ đã loại khỏi bộ tài liệu. Quyết định ở đây thay mốc so sánh nghiệm thu thành chuyên dụng máy chủ quyết định trạng thái, đồng thời vẫn cho phép máy chủ kiêm máy chơi kiêm máy khách trong development. Relay là lựa chọn kết nối cho máy chủ kiêm máy chơi mode, không phải máy chủ chạy mô phỏng của trò chơi.

# Server tạo map và client tải trận

Cập nhật 17/10/2026 theo [quyết định multiplayer hiện hành](README.md). Đây là hợp đồng dự kiến, chưa có transport hoặc nghiệm thu server+2client.

## Trách nhiệm

Server authoritative chốt cấu hình và gen một map bất biến cho cả trận. Client không gen lại từ seed; client Mono dựng hình từ dữ liệu được phép qua adapter Netcode. Dedicated server không cần mesh/material/camera.

[Pipeline ECS](../MapGeneration/EcsMapPipeline.md) và MapTerrainSnapshot.Apply đã có ở local: nhận terrain/spawn, revision/hash và chặn thay map active. API nội bộ này chưa phải network codec, cũng chưa tự tạo Netcode session.

Client không nhận trước toàn bộ terrain/resource/spawn bí mật. Quyết định FOW hiện tại là gửi **full tập terrain Explored** mỗi lần cập nhật logic, không lọc cache; ô chưa khám phá vẫn không được gửi. Full tập được phép không đồng nghĩa một packet hoặc một RPC. Gameplay entity/resource đi theo luồng được server cho phép thấy, không được bootstrap thêm bản simulation trùng trên client.

Metadata hình học cần giữ: kích thước grid, cell size, origin và TerrainBaseLevel. Mốc height cố định phải giống cache nâng unit; placeholder unknown không được thay đổi mốc chuẩn hóa. Asset/prefab/catalog nằm sẵn ở client; định nghĩa dùng khóa registry, không gửi mesh/material hoặc pointer blob local.

## Luồng dự kiến

```text
Connection/session gắn player → server gen và validate map
→ truyền cấu hình/dữ liệu khởi tạo được phép theo khối
→ client kiểm tra, dựng presentation → Ready
→ server nhận đủ Ready hợp lệ → bắt đầu simulation/gameplay
→ Ghost cập nhật entity + terrain Explored đầy đủ theo quyền nhìn
```

Ready phải thuộc đúng connection và phiên tải hiện tại; giới hạn dung lượng trước cấp phát, có timeout/hủy và xử lý thiếu/trùng. Nếu dùng hash/schema/catalog version trong bắt tay, xác định chính xác tập dữ liệu được hash để không yêu cầu client biết phần map chưa được phép nhận. Thiết kế chunk/Ready/reconnect còn mở, chưa có wire format cuối.

WorldId/MapId/MapRevision đã bỏ khỏi frame FOW ứng dụng vì mỗi trận có một map bất biến. Vòng đời session reset cache/hình khi đổi trận. Không suy rộng thành bỏ tick/identity/protocol metadata mà Netcode cần, hoặc xóa revision nội bộ MapBuildState/MapTerrainSnapshot.

## Kiểm chứng cần có

- Client không chạy generator hoặc economy/damage/production server.
- Hai client khác player nhận đúng tập terrain/entity được phép; unknown không lộ qua resource, minimap, picking hay UI.
- Height unit/collider/render khớp mặt terrain, kể cả datum dương, dốc và ranh giới chưa biết.
- Tải bị ngắt, dữ liệu trùng/thiếu, Ready sai phiên, reconnect/đổi trận không dùng lại cache cũ.
- Đo thời gian gen/serialize/truyền/dựng hình riêng, không quy tổng thời gian chờ cho generator.

Bắt đầu bằng kiểm kê dữ liệu và chọn package tương thích theo [Multiplayer](README.md); không tự viết thêm transport/manifest scaffolding trước pipeline được duyệt.

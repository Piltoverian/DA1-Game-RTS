# Tài liệu hỗ trợ code

Đọc trang theo phần đang sửa. Mỗi chủ đề chỉ có một nguồn dưới đây; code hiện tại là căn cứ khi mô tả và implementation khác nhau.

| Việc cần làm | Nguồn chính |
|---|---|
| Mở project, tìm code, hiểu World và quy tắc sửa | [Cấu trúc và cách làm việc](ProjectStructure.md) |
| Command, build/repair, gather, production, combat và UI | [Gameplay](Architecture/Gameplay.md) |
| Flow field, target, avoidance, cost và field lifetime | [Movement](Architecture/Movement.md) |
| Sinh map, bootstrap/resource, height, renderer và atlas tool | [Terrain](Architecture/Terrain.md) |
| Visibility, ký ức entity, preview và thông tin được phép biết | [Fog of War](Architecture/FogOfWarRendering.md) |
| SO, registry, blob, authoring và thêm kiểu dữ liệu | [GameData](Guides/GameData.md) |
| Connection/player, terrain Ghost, ACK và phần mạng chưa hoàn tất | [Multiplayer](Multiplayer/README.md) |
| Kết quả đã kiểm chứng, giới hạn và việc đầu tiên cần test | [Trạng thái hiện tại](Maintenance/CurrentState.md) |

## Cách cập nhật

- Sửa trang sở hữu chủ đề ngay khi đổi API/luồng/invariant; trang khác chỉ dẫn link.
- Ghi entry point, input → validation → thay đổi state → output, lifetime và cách kiểm tra. Không chép lại từng dòng implementation.
- Phân biệt source đã có, kiểm tra thực sự đã chạy và việc còn dự kiến. Kết quả trước khi sửa code không nghiệm thu code mới.
- Giữ một CurrentState cho việc chưa hoàn tất; không tạo thêm handoff, roadmap, bản tóm tắt song song hoặc nhật ký từng phiên.
- Script, runner, log và receipt dùng một lần phải được dọn sau khi dùng. Không tạo tool chỉ để ghi nhận việc dọn; giữ kết quả cần thiết trong trang chủ đề/CurrentState.

README cạnh asset chỉ giải thích asset/import/GUID tại chỗ; không tạo thêm bộ kiến trúc. Asset runtime, nguồn art và license không phải artifact tạm.

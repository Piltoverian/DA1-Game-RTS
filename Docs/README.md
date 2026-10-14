# Tài liệu dự án RTS

Đây là bộ docs chính duy nhất. Mỗi chủ đề có một nguồn; không còn bộ GianLuoc song song.

| Cần làm | Tài liệu |
|---|---|
| Tiếp tục công việc | [Trạng thái](Maintenance/CurrentState.md) · [Handoff](Maintenance/Handoff.md) · [Roadmap](Maintenance/Roadmap.md) |
| Tìm code | [Cấu trúc](ProjectStructure.md) |
| Thiết kế dữ liệu game | [GameData](Guides/GameData/README.md) · [Thanh toán](Guides/GameData/ResourcePayment.md) · [Quyền điều khiển](Guides/GameData/CommandAuthority.md) |
| Import/design atlas | [Atlas](TerrainVisualizer/14_SuDung_Atlas_Implementation.md) · [Nguồn art](Guides/ArtAssets.md) |
| Bản đồ | [MapGeneration](MapGeneration/MapGen_INDEX.md) |
| Xây dựng | [BuildingPipeline](Architecture/BuildingPipeline.md) |
| Chuyển động | [Tổng quan](Architecture/Movement/MovementAgent/Architecture_Overview.md) |
| Multiplayer | [Kiến trúc và lộ trình](Multiplayer/README.md) |
| Tra bằng chứng cũ | [Lịch sử hợp nhất](Maintenance/VerificationHistory.md) |

Tool import/design và validator dữ liệu còn giữ; helper/menu chỉ để test đã xóa. Preview atlas chạy trong bộ nhớ, không phụ thuộc fixture/report test. Nguồn tham chiếu thiết kế atlas duy nhất là JadeSlate; Main hiện dùng asset runtime MoonTerrain được giữ nguyên.

Mã hiện tại và xác nhận của chủ dự án quyết định trạng thái. Báo cáo lịch sử không nghiệm thu source mới. Quy trình cập nhật theo [AGENTS.md](../AGENTS.md).

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
| Fog of War local | [Render và bộ lệnh](Architecture/FogOfWarRendering.md) · [Handoff local](Maintenance/FogOfWarHandoff.md) |
| Multiplayer | [Kiến trúc và lộ trình](Multiplayer/README.md) · [Bài 1: bảng source và trách nhiệm](Multiplayer/SourceResponsibilityMap.md) |
| Tra bằng chứng cũ | [Lịch sử hợp nhất](Maintenance/VerificationHistory.md) |

Tool import/design và validator dữ liệu còn giữ; các test Editor FOW, terrain và cache server đã xóa theo yêu cầu ngày 20/10. [Thư mục Fixtures](../Assets/_Project/Tests/README.md) giữ asset gameplay đang dùng. Preview atlas chạy trong bộ nhớ, không phụ thuộc fixture/report test. Nguồn tham chiếu thiết kế atlas duy nhất là JadeSlate; Main hiện dùng asset runtime MoonTerrain được giữ nguyên.

Mã hiện tại và xác nhận của chủ dự án quyết định trạng thái. Báo cáo lịch sử không nghiệm thu source mới. Quy trình cập nhật theo hướng dẫn AGENTS.md do người dùng cung cấp trong phiên.

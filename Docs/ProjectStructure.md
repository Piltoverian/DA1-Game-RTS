# Sơ đồ project và quy tắc sắp xếp

## Code và asset hiện tại

| Vị trí | Vai trò |
|---|---|
| `Assets/Scripts/Game(New Simulation Logic)` | Simulation ECS, MapGen, movement, economy, combat, construction, player và dữ liệu bake |
| `Assets/Scripts/ClientSide (Presentation)` | Input, camera, UI và cầu nối MonoBehaviour/ECS |
| `Assets/Scripts/Events` | Event channels và listeners |
| `Assets/Scripts/EditorTools`, `Assets/Editor` | Công cụ Unity Editor |
| `Assets/Scripts/MovementAgentDocs` | Tài liệu movement đặt cạnh code; entrypoint là Architecture_Overview và Manual/Movement_Manual_2026 |
| `Assets/Scripts/SO` | Asset ScriptableObject hiện có, dù nằm trong Scripts; cần kiểm kê trước khi gom sang Data |
| `Assets/Resources` | Asset được tra cứu qua Resources và một số script liên quan |
| `Assets/Scenes`, `Assets/Prefab` | Scene và prefab gameplay |
| `Assets/Temp/MapGenTest` | Fixture Main/MapGen đang được sử dụng, gồm asset và smoke tool Editor |

Giữ nguyên các đường dẫn Unity hiện tại trong đợt dọn 06/10/2026. Đổi tên thư mục simulation/presentation hoặc gom SO cần một đợt riêng, di chuyển qua Unity hoặc kèm meta, rồi kiểm tra GUID/reference và import.

## Tài liệu

| Điểm vào | Cách sử dụng |
|---|---|
| [Docs/README](README.md) | Portal designer và liên kết các hệ thống |
| [DataRefactor](DataRefactor/README.md) | Hướng dẫn dữ liệu/authoring |
| [MapGen_INDEX](Multiplayer/MapGen_INDEX.md) | Điểm tra cứu MapGen hiện hành |
| [MapGen_Unity_Rules](Multiplayer/MapGen_Unity_Rules.md) | Hợp đồng và thuật toán Unity |
| [Multiplayer/README](Multiplayer/README.md) | Thiết kế multiplayer và roadmap |
| `Multiplayer/Labs` | Tham khảo trực quan, không thay thế code gameplay |
| `Multiplayer/Archive` | Lịch sử và tài liệu đã lưu trữ |
| [Reviews](Reviews/README.md) | Review tĩnh, trạng thái kiểm chứng và log build lịch sử |

Kế hoạch cũ còn có liên kết chéo nên giữ đường dẫn hiện tại; MapGen_INDEX đã chỉ rõ nguồn hiện hành và nguồn lịch sử. Tài liệu mới nên ghi ngày, trạng thái (hiện hành/kế hoạch/lịch sử) và kết quả test thực tế, tránh tạo thêm nhiều entrypoint cạnh tranh.

## Quy tắc dọn gọn tiếp theo

1. Nội dung sinh tự động dùng gitignore; không xóa cache Unity khi Editor đang sử dụng.
2. Log/report mới đặt trong Artifacts theo công cụ. Báo cáo cần giữ lâu dài đưa vào Docs/Reviews, ghi rõ phiên bản và phạm vi kiểm tra.
3. Asset thử có reference vẫn là nguồn cần giữ. Trước khi bỏ asset, kiểm tra GUID trong scene/prefab/SO và đường tra cứu runtime.
4. Di chuyển asset/script giữ meta, không đổi class/namespace/logic trong cùng đợt dọn thư mục.
5. Sau khi dọn Unity asset, nghiệm thu import/bake và mở scene Main; thay đổi chỉ ở tài liệu/gitignore không cần test gameplay lại.

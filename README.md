# Dự án RTS trên Unity

Game chiến thuật thời gian thực dùng DOTS/ECS: điều khiển unit, khai thác, xây dựng, sản xuất và nghiên cứu trên terrain nhiều tầng với Fog of War.

**Bắt đầu tại [Docs/README.md](Docs/README.md)** — bộ tài liệu hỗ trợ code duy nhất.

- [Mở project, phiên bản và tìm source](Docs/ProjectStructure.md)
- [Trạng thái đã kiểm chứng và việc cần test tiếp](Docs/Maintenance/CurrentState.md)

Mở project qua Unity Hub bằng phiên bản trong [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt), chờ import/compile rồi mở [Main](Assets/_Project/Scenes/Main.unity). Cách chọn local/host/client nằm trong docs.

Giữ `.meta` cùng asset. Dữ liệu Tests/Fixtures đang được registry/Main dùng không phải artifact tạm. Không xóa runtime asset, nguồn art hoặc file người dùng khi dọn tool/log dùng một lần.

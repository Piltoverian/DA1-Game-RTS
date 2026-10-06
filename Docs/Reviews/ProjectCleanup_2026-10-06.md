# Dọn cấu trúc project — 06/10/2026

## Nội dung đã làm

- Gom code, art, dữ liệu, scene, prefab, settings và fixture vào Assets/_Project.
- Tách Runtime/Simulation, Presentation, GameData, Events và Editor; giữ class/namespace/gameplay.
- Gom movement; SO ra Data/Definitions; script icon mapping ra UI; Resources giữ tên/key.
- Gom docs movement vào Architecture/Movement, dữ liệu vào Guides/GameData, MapGen/lab/research/history vào MapGeneration.
- Cập nhật đường dẫn trong docs, Build Settings, R0Audit, BootstrapSpawnSmokeTest và outputFolder của RevealMaterialAutoSetup. Giữ scene được chọn trước đó.
- Chuyển solution đã track vào archive, bỏ exception gitignore cho fixture Temp cũ.

## Nội dung rời Assets nhưng được giữ để khôi phục

Archive/ProjectCleanup-2026-10-06 chứa TutorialInfo và Readme.asset của Unity template; New Terrain.asset và renderer asset tên mặc định chưa thấy reference; thư mục _Recovery rỗng; hai solution .slnx và meta container đã dọn rỗng.

Giữ HDRPDefaultResources, SceneDependencyCache, VisualScripting.Generated, SkySeries Freebie và TextMesh Pro vì chưa đủ căn cứ loại bỏ. Giữ NewMat dưới tên file LegacySample.mat vì SampleScene đang tham chiếu; giữ root URP global settings và volume profile trong Settings/Rendering vì GraphicsSettings đang dùng.

## Đối chiếu

- Migration đối chiếu 1.066 meta gốc và bảo toàn GUID.
- Kiểm tra manifest sau di chuyển không thiếu file, không trùng GUID trong Assets.
- [Manifest đường dẫn](CleanupManifest_2026-10-06.json) lưu mapping từng file và danh sách move để truy vết/khôi phục.
- csproj cục bộ cập nhật theo manifest để kiểm tra C#; Unity có thể sinh lại. dotnet build không thay thế import/bake hoặc Player build.

Sau khi Unity refresh, mở Assets/_Project/Scenes/Main.unity và chạy smoke test MapGen để kiểm tra scene/config trên Editor. Không có thay đổi chủ ý về gameplay.

Kết quả biên dịch: Runtime thành công với 5 warning code cũ; lần chạy đồng thời có thêm cảnh báo/tranh chấp file output. Đã chạy lại Editor tuần tự (bao gồm Runtime dependency), thành công 0 error. Build tuần tự sau đó là incremental nên 0 warning không có nghĩa các warning code cũ đã được sửa.

Liên kết tương đối đã được kiểm tra; các link lịch sử tới R2/SessionHandoff/DevelopmentPlan vốn thiếu file được ghi rõ là tài liệu chưa có trong checkout.

## Xóa lịch sử MapGen theo yêu cầu chủ project

Đã xóa Labs, Archive, Research trong Docs/MapGeneration cùng bản chốt Lab5, kế hoạch port Lab6 và handoff 04/10. Tổng 77 file, khoảng 6,27 MiB. Các link tới nội dung đã xóa được bỏ/cập nhật về rule Unity hiện hành. Code, prefab, theme và fixture MapGen trong Assets không thay đổi. Archive/ProjectCleanup-2026-10-06 của đợt dọn chung vẫn được giữ.

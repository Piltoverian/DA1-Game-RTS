# Asset gameplay trong Fixtures

Fixtures được Main/registry tham chiếu qua GUID là dữ liệu gameplay; giữ asset, prefab và meta, không xóa theo tên thư mục.

Đã xóa ba test Editor FowCommandChecks, TerrainFogChecks và ServerMatchChecks cùng meta theo yêu cầu ngày 20/10/2026. Không còn menu hoặc batch runner của các test này; không tái tạo harness đã nghỉ dùng.

20/20 FOW là kết quả lịch sử trước khi tách material Unknown; không thay nghiệm thu source mới. Test cache server chỉ từng build, chưa chạy được vì Unity batch thoát trước executeMethod. [Handoff](../../../Docs/Maintenance/FogOfWarHandoff.md) giữ giới hạn của các lượt kiểm chứng; [luật FOW](../../../Docs/Architecture/FogOfWarRendering.md) là nguồn chính.

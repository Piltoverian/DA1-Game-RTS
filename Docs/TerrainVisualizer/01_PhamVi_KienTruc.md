# Phạm vi và kiến trúc địa hình

Hiện hành sau cleanup2026-10-13. [Unity quy trình](14_SuDung_Atlas_Implementation.md), [dọn dẹp](16_Render_Cleanup.md).

## Ranh giới dữ liệu

bản đồ sinh dữ liệu sở hữu lưới, kết nối lưới, địa hình ô, dốc nối tầng, sinh đối tượng và tài nguyên. Navigation/cost/ORCA/arrival dùng XZ. `TerrainSurfaceData` đọc dữ liệu bản đồ, chuẩn hóa tầng và lấy bốn góc theo exact triangle/ramp. `TerrainHeightCacheSystem` sở hữu bộ nhớ đệm NativeArray theo lưới entity/revision; bộ nhớ đệm không cần bộ dựng hình hoặc camera.

Ground thực thể gốc có một LocalTransform cho dựng hình và khối va chạm. PendingHeight chỉ lấy mẫu khi spawn/di chuyển hoặc lưới phiên bản thay đổi; Y=địa hình độ cao+điểm gốc độ lệch. bước cố định đạt trước physics, late-sinh đối tượng đạt trước transform propagation. Không còn offset/restore LocalToWorld.

## Một luồng xử lý hiển thị

Atlas3 ×2 → TerrainAtlasAuthoring/TerrainCanonicalBaker → TerrainTheme → TerrainChunkRenderer → CanonicalIsometricSprites → địa hình độ sâu chương trình tô bóng.

TerrainChunkRenderer đóng gói42mặt chuẩn và hình học đi đôi vào hai khi chạy atlas, tạo một vật liệu và các batch2048sprite. ảnh sprite quads Y0; chương trình tô bóng tái dựng virtual bề mặt và ghi SV_Depth. Núi chỉ bổ sung hình khối trang trí bên trong vùng chiếm chỗ đã bị chặn; không đổi địa hình lối chơi độ cao.

TerrainVisualSurface.Active phục vụ chọn bằng tia máy khách. TerrainMountainFade dùng vùng con trỏ/đã chọn giới hạn, không khối va chạm riêng từng ảnh sprite. Camera pitch30/yaw45, pan/zoom, không rotate. MapTerrainPresentationSystem lấy snapshot/revision để dựng lại đúng lúc.

## Quyền sở hữu và vòng đời

- Grid/revision sở hữu độ cao bộ nhớ đệm và là dữ liệu có thể dùng trong headless/server thế giới ECS.
- TerrainChunkRenderer sở hữu khi chạy atlas, hình học atlas, vật liệu và mesh/root; Clear/OnDestroy giải phóng chúng và active bề mặt của chính bộ dựng hình.
- Importer sở hữu các đầu ra có biên nhận kết quả GUID/hash. Override người thiết kế bảo vệ actual color/depth tham chiếu trước mọi write; sameSO/GUID và thiết lập giữ khi nhập lại.
- Shader chỉ dùng pixel vật liệu atlas; không ghi lối chơi dữ liệu. hạt giống hình ảnh độc lập RNG map/resource.

bộ mẫu cũ phương án dự phòng, tấm hướng camera núi, mặt đất variant nhóm và hệ thống độ lệch ma trận đã xóa. Thiếu cặp color/geometry thì báo lỗi nhập cụ thể; không chọn bộ dựng hình khác. Main gán MoonTerrain/LunarTheme.asset tại Data/Terrain/Generated.

## Kiểm chứng và giới hạn

Core kiểm tra độ cao lưới, importer/ownership/rollback, luồng xử lý và chuyển động. Graphics kiểm tra occlusion/làm mờ/restore bằng pixel oracle, chọn bằng tia, shared ridges và xem trước. Main kiểm tra nhanh kiểm tra gốc trong Play. Chưa nghiệm thu30FPS/2.000 lính, GPU ngân sách hoặc server20Hz; dữ liệu lịch sử chỉ đại diện phiên bản/khối lượng công việc được ghi.

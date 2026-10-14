# Atlas địa hình — bắt đầu ở đây

**Mở [hướng dẫn Unity](14_SuDung_Atlas_Implementation.md) để import/chạy địa hình — khoảng 3 phút để bắt đầu.**

luồng xử lý: atlas vật liệu3 ×2 → bộ nhập → cùng TerrainTheme → ảnh sprite quads + độ sâu hình học. Main dùng MoonTerrain/LunarTheme.asset; núi theo vùng chiếm chỗ, scale4/tối đa6 tầng và làm mờ vùng thao tác.

| Bạn cần | Trang |
|---|---|
| Gen hình ảnh mỹ thuật và đưa vào Unity | [Prompt](11_Manual_Prompt_ThietKe_Render.md) · [Quy trình Unity](14_SuDung_Atlas_Implementation.md) |
| Hiểu data/presentation | [Kiến trúc](01_PhamVi_KienTruc.md) · [Projection/depth](02_PhepChieu_RenderOrder.md) |
| Ground/ramp/mountain | [Thuật toán](03_ThuatToan_Tile_Nui.md) |
| chọn bằng tia và độ cao | [Surface/ECS](04_Surface_Picking_ECS.md) |
| Schema hình ảnh mỹ thuật và phần đã loại bỏ | [Art luồng xử lý](05_Atlas_ArtPipeline.md) · [dọn dẹp](16_Render_Cleanup.md) |

bản thử nghiệm cũ tách khỏi bộ tài liệu khi chạy; bản thử nghiệm lưu trong Artifacts. bộ mẫu cũ renderer/offset quy trình đã loại khỏi mã nguồn. Artifacts giữ bằng chứng các lượt trước; ảnh hoặc kiểm tra nhanh không chứng minh tải2.000 lính/30FPS.

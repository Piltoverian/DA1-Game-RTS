# Terrain asset cleanup — 2026-10-05

Theo yêu cầu tiết kiệm dung lượng, TerrainAssets-2026-10-05.zip đã được xóa khỏi project. Các bộ ảnh thử nghiệm cũ, raw/review/audit V17, output render Lab6 và PreviewAtlas không còn được lưu trong project.

Unity giữ chín sprite V17 được V17Theme.asset tham chiếu và hai atlas đang được lab HTML/build scripts sử dụng. Các lab HTML vẫn là tài liệu tham khảo; các script test và ảnh preview thử nghiệm đã bị xóa.

Scene preview, công cụ Editor terrain, harness TerrainPort và RegistryValidation cũng đã được xóa. TerrainMapRenderer trong Main dựng map gameplay từ grid đã bake. Các kiểm tra bảo vệ địa hình cần thiết nằm trong generator vẫn được giữ.

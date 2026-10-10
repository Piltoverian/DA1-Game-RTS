# Sample Meadow — nguồn artwork

terrain-atlas.png gom sáu material theo layout 3×2 cố định; terrain-atlas.schema.json ghi pixel rect/Unity UV. Xem Docs/MapGeneration/MapGen_AtlasTemplate.md. Atlas nguồn chưa thay thế diamond theme đang dùng trong runtime.

Tạo bằng built-in image_gen ngày 2026-10-09. ground-source.png: bốn vùng variation, diffuse cỏ olive. ramp-source.png/ramp-512.png: đất ochre nâu cho mặt thoải. cliff-source.png/cliff-512.png: đá gray/olive/brown cho mặt đứng. Prompt đầy đủ ở prompts.json.

Ground đã đóng gói thành bốn diamond 65×33 và theme tại Assets/_Project/Data/Maps/SampleMeadow. Ramp/cliff là nguồn cho schema tương lai, chưa được shader hiện tại đọc. Giữ source ngoài Assets để không import ảnh lớn chưa dùng vào build. Tiling được yêu cầu khi generate nhưng chưa bảo đảm pixel edges seamless; cần kiểm tra/sửa seam khi tích hợp.

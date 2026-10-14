# Máy chủ tạo bản đồ và đồng bộ tải trận

Quyết định của chủ dự án ngày 2026-10-06: máy chủ tạo bản đồ một lần và gửi dữ liệu kết quả cho máy khách. Giai đoạn tiếp theo ưu tiên triển khai chơi nhiều người. Đây là hợp đồng thiết kế; chưa có implementation mạng hoặc nghiệm thu hai máy.

Cập nhật 2026-10-12: đã có [luồng xử lý ECS và API ảnh trạng thái](../MapGeneration/EcsMapPipeline.md). MapGenerationRecipe là dữ liệu giá trị; sinh dữ liệu thuộc local/server thế giới ECS, presentation/camera thuộc local/client. MapTerrainSnapshot.Apply nhận terrain/spawn, revision/hash, bỏ qua gói dữ liệu trùng và chặn thay bản đồ đã active. Chưa có transport/mã hóa dữ liệu mạng, đồng bộ resource/gameplay thực thể hoặc tích hợp khởi tạo máy khách nhận trạng thái Ready từ máy chủ; thế giới ECS filter tự nó chưa tạo một phiên chơi nhiều người.

## Phân chia trách nhiệm

máy chủ (có thể là máy máy chủ kiêm máy chơi) chốt cấu hình, chạy bộ sinh dữ liệu, validate bản đồ, giữ dữ liệu lối chơi có thẩm quyền và khởi tạo mô phỏng. máy khách nhận dữ liệu bản đồ đã tạo và dựng presentation/minimap cùng dữ liệu cục bộ cần thiết. máy khách không chạy lại thuật toán sinh bản đồ từ hạt giống.

máy chủ kiêm máy chơi cũng dùng kết quả bản đồ đã tạo cho phần hiển thị, không gen lần thứ hai. Dedicated máy chủ chỉ cần phần dữ liệu/physics phục vụ mô phỏng; không bắt buộc dựng lưới hình học hiển thị.

Chỉ truyền dữ liệu tĩnh cần tái dựng bản đồ: lược đồ dữ liệu ô địa hình/height/walkability, vị trí và loại tài nguyên cùng dữ liệu khởi đầu, điểm sinh đối tượng. mẫu đối tượng, ảnh sprite, vật liệu và lưới hình học có thể dựng từ tài nguyên đã có trên máy khách. Cần kiểm kê lược đồ dữ liệu thực tế trước khi chốt định dạng; không mặc định ảnh vật liệu RGBA chứa đủ mọi trường. hạt giống giữ làm metadata/debug.

Unit, công trình và tài nguyên có trạng thái lối chơi do máy chủ sở hữu. Tái dựng hình ảnh bản đồ không được tạo thêm một bản thực thể lối chơi trùng với thực thể đồng bộ mạng. ID prefab/catalog phải được ánh xạ ổn định giữa các bản trò chơi; không truyền Unity Entity handle như ID dùng chung giữa các thế giới ECS.

## Luồng tải trận dự kiến

```text
Chốt danh sách người chơi/cấu hình trận
    → ServerGenerating
    → Truyền MapManifest và các khối dữ liệu
    → Máy khách kiểm tra,giải nén và dựng bản đồ
    → Máy khách gửi MapReady(matchId, mapHash)
    → Máy chủ hoàn tất khởi tạo và nhận đủ xác nhận sẵn sàng hợp lệ
    → Máy chủ phát MatchStart(startTick)
    → Playing
```

Trong các bước tải, tiếp tục xử lý kết nối, tiến độ, hủy và hết thời gian chờ. lối chơi chưa được chạy; các hệ thống thiết lập và networking vẫn được phép chạy. Không dùng LocalTestPlayer làm nguồn identity của session chơi nhiều người.

Manifest cần matchId, phiên bản schema/generator/catalog, kích thước và hệ tọa độ lưới, kích thước gói dữ liệu nén/giải nén, số khối và mã băm dữ liệu chuẩn. Giới hạn dung lượng trước khi cấp phát/giải nén; kiểm tra khối thiếu/trùng và dữ liệu sai phiên bản. Ready phải thuộc đúng kết nối, trận và mã băm hiện tại. Progress chỉ phục vụ UI, không thay điều kiện ready.

Truyền gói dữ liệu theo khối với delivery bảo đảm, giới hạn lượng đang gửi và thời hạn; không đặt toàn bộ bản đồ vào một RPC lớn. Kích thước khối và phương pháp nén sẽ được chọn khi đo gói dữ liệu và xác nhận lớp truyền dữ liệu. Chính sách hết thời gian chờ, người rời trận và late mép nối cần được chốt trước nghiệm thu.

Dữ liệu địa hình tĩnh và trạng thái lối chơi động là hai luồng riêng. Trạng thái tài nguyên thay đổi, lính và công trình tiếp tục được máy chủ đồng bộ trong trận theo quyền nhìn thấy; không gửi lại toàn bản đồ mỗi bước mô phỏng.

## Thứ tự triển khai tiếp

1. Đo riêng sinh dữ liệu, kiểm tra hợp lệ, tài nguyên planning, visual/collider và thực thể khởi tạo. Chủ dự án báo tổng thời gian khoảng 2 phút; chưa có số đo từng bước. Chuyển gen sang máy chủ không tự loại bỏ thời gian dựng bản đồ trên máy khách.
2. Tách bộ sinh dữ liệu và dữ liệu kết quả khỏi bên xử lý visual/gameplay để máy chủ chạy một lần, máy khách nhận kết quả. Thiết kế lược đồ dữ liệu và codec bản đồ có version.
3. Tạo match vòng đời và gán PlayerId từ session/kết nối; chặn lối chơi trước Playing.
4. Dựng lượt thử nhỏ chuyên dụng máy chủ + hai máy khách, truyền bản đồ, xác nhận hash/Ready và bắt đầu cùng mốc máy chủ bước mô phỏng. Listen máy chủ kiêm máy chơi là môi trường development bổ sung.
5. Đồng bộ lệnh/trạng thái, nội suy và reconnect/resync theo lộ trình hiện hành. Lobby/Relay/draft thuộc phần sau, chưa là phạm vi của lượt thử nhỏ hoặc một kế hoạch đã nghiệm thu.

## Tiêu chí nghiệm thu bản tối thiểu

- máy chủ chạy bộ sinh dữ liệu một lần; máy khách không chạy bộ sinh dữ liệu và cả hai dùng cùng dữ liệu map/hash.
- Không nhân đôi thực thể khi máy chủ kiêm máy chơi dựng hình ảnh hoặc máy khách nhận ảnh trạng thái.
- lối chơi chỉ bắt đầu khi máy chủ khởi tạo xong và các máy khách bắt buộc đã ready; lệnh gửi sớm được xử lý theo chính sách rõ ràng.
- Tải chậm vẫn có tiến độ và kết nối hoạt động. Dữ liệu hỏng/sai phiên bản, mất kết nối hoặc hết thời gian chờ có kết quả lỗi rõ ràng, không bắt đầu trận dở dang.
- Đo thời gian tạo, truyền và dựng bản đồ riêng trong người chơi bản dựng; không chỉ kiểm thử với SubScene mở trong Editor.

Xem [lộ trình chơi nhiều người](README.md) và [quyết định kiến trúc](ArchitectureChoice_2026-10-12.md). Các đề xuất cũ gửi hạt giống để máy khách tự gen không phải phương án đã chọn.

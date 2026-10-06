# Server tạo map và đồng bộ tải trận

Quyết định của chủ project ngày 2026-10-06: server tạo map một lần và gửi dữ liệu kết quả cho client. Giai đoạn tiếp theo ưu tiên triển khai multiplayer. Đây là hợp đồng thiết kế; chưa có implementation mạng hoặc nghiệm thu hai máy.

## Phân chia trách nhiệm

Server (có thể là máy host) chốt cấu hình, chạy generator, validate map, giữ dữ liệu gameplay có thẩm quyền và khởi tạo simulation. Client nhận dữ liệu map đã tạo và dựng presentation/minimap cùng dữ liệu cục bộ cần thiết. Client không chạy lại thuật toán sinh map từ seed.

Host cũng dùng kết quả map đã tạo cho phần hiển thị, không gen lần thứ hai. Dedicated server chỉ cần phần dữ liệu/physics phục vụ simulation; không bắt buộc dựng mesh hiển thị.

Chỉ truyền dữ liệu tĩnh cần tái dựng map: schema ô địa hình/height/walkability, vị trí và loại tài nguyên cùng dữ liệu khởi đầu, điểm spawn. Prefab, sprite, material và mesh có thể dựng từ asset đã có trên client. Cần kiểm kê schema thực tế trước khi chốt định dạng; không mặc định texture RGBA chứa đủ mọi field. Seed giữ làm metadata/debug.

Unit, công trình và tài nguyên có trạng thái gameplay do server sở hữu. Tái dựng visual map không được tạo thêm một bản entity gameplay trùng với entity đồng bộ mạng. ID prefab/catalog phải được ánh xạ ổn định giữa các bản game; không truyền Unity Entity handle như ID dùng chung giữa các World.

## Luồng tải trận dự kiến

```text
Chốt roster/cấu hình trận
    → ServerGenerating
    → Truyền MapManifest và các khối dữ liệu
    → Client kiểm tra, giải nén và dựng map
    → Client gửi MapReady(matchId, mapHash)
    → Server hoàn tất bootstrap và nhận đủ ready hợp lệ
    → Server phát MatchStart(startTick)
    → Playing
```

Trong các bước tải, tiếp tục xử lý kết nối, tiến độ, hủy và timeout. Gameplay chưa được chạy; các hệ thống setup và networking vẫn được phép chạy. Không dùng LocalTestPlayer làm nguồn identity của session multiplayer.

Manifest cần matchId, phiên bản schema/generator/catalog, kích thước và hệ tọa độ grid, kích thước payload nén/giải nén, số khối và hash dữ liệu chuẩn. Giới hạn dung lượng trước khi cấp phát/giải nén; kiểm tra khối thiếu/trùng và dữ liệu sai phiên bản. Ready phải thuộc đúng kết nối, trận và hash hiện tại. Progress chỉ phục vụ UI, không thay điều kiện ready.

Truyền payload theo khối với delivery bảo đảm, giới hạn lượng đang gửi và thời hạn; không đặt toàn bộ map vào một RPC lớn. Kích thước khối và phương pháp nén sẽ được chọn khi đo payload và xác nhận transport. Chính sách timeout, người rời trận và late join cần được chốt trước nghiệm thu.

Dữ liệu địa hình tĩnh và trạng thái gameplay động là hai luồng riêng. Trạng thái tài nguyên thay đổi, unit và công trình tiếp tục được server đồng bộ trong trận theo quyền nhìn thấy; không gửi lại toàn map mỗi tick.

## Thứ tự triển khai tiếp

1. Đo riêng generation, validation, resource planning, visual/collider và entity bootstrap. Chủ project báo tổng thời gian khoảng 2 phút; chưa có số đo từng bước. Chuyển gen sang server không tự loại bỏ thời gian dựng map trên client.
2. Tách generator và dữ liệu kết quả khỏi consumer visual/gameplay để server chạy một lần, client nhận kết quả. Thiết kế schema và codec map có version.
3. Tạo match lifecycle và gán PlayerId từ session/kết nối; chặn gameplay trước Playing.
4. Dựng bản multiplayer tối thiểu host + một client, truyền map, xác nhận hash và ready, bắt đầu cùng mốc server tick.
5. Đồng bộ lệnh và trạng thái gameplay; sau đó nối lobby/Relay và các tính năng draft theo lộ trình tổng thể.

## Tiêu chí nghiệm thu bản tối thiểu

- Server chạy generator một lần; client không chạy generator và cả hai dùng cùng dữ liệu map/hash.
- Không nhân đôi entity khi host dựng visual hoặc client nhận snapshot.
- Gameplay chỉ bắt đầu khi server bootstrap xong và các client bắt buộc đã ready; lệnh gửi sớm được xử lý theo chính sách rõ ràng.
- Tải chậm vẫn có tiến độ và kết nối hoạt động. Dữ liệu hỏng/sai phiên bản, mất kết nối hoặc timeout có kết quả lỗi rõ ràng, không bắt đầu trận dở dang.
- Đo thời gian tạo, truyền và dựng map riêng trong Player build; không chỉ test với SubScene mở trong Editor.

Xem [lộ trình multiplayer](README.md) và [audit Sprint 0](Sprint0_CodeAudit.md). Các đề xuất cũ gửi seed để client tự gen không phải phương án đã chọn.

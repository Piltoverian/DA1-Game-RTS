# Bản vá sự cố dừng đột ngột khởi tạo — 2026-10-12

sự cố dừng đột ngột gần nhất được kiểm tra: `Crash_2026-10-11_195018721`, tức 02:50:18 ngày 12/10 theo giờ Bangkok. Unity 6000.3.13f1 sự cố dừng đột ngột native tại `AtomicSafetyHandle::GetInternalNode` khi `PlayerBootstrapSystem` gọi `GetBuffer<PlayerBootstrapSpawned>` trong vòng lặp tạo tài nguyên. tài nguyên bộ lập kế hoạch trước đó đã hoàn thành với 1.952 nút.

Stack trace xác định được điểm sự cố dừng đột ngột; chưa chứng minh nguyên nhân gốc là lỗi engine, hỏng bộ nhớ hay race. Việc lấy lại bộ đệm sau structural change vốn hợp lệ trong ECS; bản vá tránh đường thực thi đang gây sự cố dừng đột ngột, không khẳng định mọi lần GetBuffer sau tạo bản sao đối tượng đều sai.

## Thay đổi

`PlayerBootstrapSystem.cs` giữ nguyên quy trình lập và kiểm tra kế hoạch đặt nhà. Khi kế hoạch hợp lệ, hệ thống ghi tạo bản sao đối tượng, SetComponent, Add/RemoveComponent, enableable tags và AppendToBuffer vào một EntityCommandBuffer; sau đó Playback một lần rồi chuyển sang Finalizing.

Trong lúc ghi lệnh, dữ liệu gốc được đọc từ mẫu đối tượng. Không đọc thực thể deferred trước Playback. Các tham chiếu thực thể trong PlayerBootstrapSpawned được ECB remap sang thực thể thực. hệ thống không còn gọi GetBuffer cho từng tài nguyên giữa chuỗi structural change.

`MainRuntimeBenchmark` bổ sung kiểm tra số mục bằng tổng nhà chính + thợ + tài nguyên đã lập kế hoạch; mỗi mục phải tồn tại, không phải mẫu đối tượng và không trùng. Các kiểm tra GameplayReady, bộ dựng hình và camera vẫn chạy trong Main 512 ×512.

## Giới hạn

Trong lúc kiểm tra còn gặp một sự cố dừng đột ngột trước Play: Windows event 1000 ghi `ntdll.dll`, mã `0xc0000374` (heap corruption), PID 26212 lúc 02:55:48. Hai sự cố dừng đột ngột cũ lúc 02:12 và 02:30 nằm trong `burst-llvm-21.dll`. Vì vậy không thể quy mọi sự cố dừng đột ngột cho khởi tạo chỉ dựa vào một stack trace.

bộ nhớ đệm `Library/BurstCache` đã được sao lưu để cho Unity tạo lại. Sau hai lượt xác nhận đạt, bản sao bộ nhớ đệm cũ đã được dọn ngày 2026-10-12; bộ nhớ đệm hiện tại được giữ. Không đổi version package hay tắt Burst trong cấu hình dự án.

Sau bản vá và tạo lại bộ nhớ đệm, hai lượt Main 512 ×512 đều đạt, mỗi lượt 600 khung hình:

- chưa làm nóng bộ nhớ đệm, Burst biên dịch đồng bộ: Ready 37,95 giây.
- Burst biên dịch mặc định: Ready 26,55 giây.
- Cả hai kiểm tra được 2.000 thực thể sống và duy nhất, trong đó 1.952 tài nguyên; GameplayReady, bộ dựng hình và camera đạt; không có sự cố dừng đột ngột safety handle hoặc cảnh báo JobTempAlloc trong nhật ký.

Log: `Artifacts/MainRuntimeBenchmark-bootstrap-crash-fix-cold.log` và `Artifacts/MainRuntimeBenchmark-bootstrap-crash-fix-normal.log`. JSON kết quả tương ứng nằm trong `Artifacts/MainRuntimeBenchmark/bootstrap-crash-fix-cold-results.json` và `bootstrap-crash-fix-normal-results.json`.

Pass trong lô Editor chứng minh đường khởi tạo và remapping hoạt động trong bài kiểm tra đó. Cần kiểm tra Play tương tác để xác nhận sự cố dừng đột ngột native không còn tái diễn trong điều kiện Editor của người dùng.

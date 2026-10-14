# Nguồn art và thiết kế

JadeSlate là bộ tham chiếu atlas duy nhất trong `ArtSource/Terrain/JadeSlate`. `materials.png`, prompt và decorations là đầu vào thiết kế; các texture được Unity sinh ra là asset runtime. Main hiện tham chiếu `MoonTerrain/LunarTheme.asset`, nên bộ runtime đó được giữ nguyên. Tool tạo ví dụ vẫn repack nguồn JadeSlate.

## Atlas vật liệu

Atlas import có bố cục 3×2 với sáu panel vuông: ground-lush, ground-sparse, ramp-earth, mountain-slate, cliff-slate, ground-detail. Tool kiểm tra Read/Write, kích thước và ownership khi cập nhật cùng SO. Giữ nguồn vật liệu phẳng, không phối cảnh/vật thể/bóng đổ; hình học UV và độ sâu do baker xử lý.

[Import và preview](../TerrainVisualizer/14_SuDung_Atlas_Implementation.md).


## ArtSource/Terrain/JadeSlate

# Jade Slate — thư viện vật liệu đã tạo hiện tại

`materials.png` là art nguyên bản tạo bằng ImageGen tích hợp ngày2026-10-10. Bốn phần bằng nhau:cỏ dày,đá phiến núi phẳng,cỏ thưa,đá phiến vách. Mô tả gốc trong `prompt.md`; `build.json` ghi mã băm nguồn,số đầu ra,cách raster hóa.

78texture chuẩn đã lưu trong repo. bộ sinh dữ liệu Python ngoài Unity đã bỏ ngày2026-10-10. Quy trình nhập ở thời điểm thư viện tạo dùng `RTS/Terrain/Import Jade Slate Material Library`;đây là mục menu lịch sử,tra quy trình atlas hiện hành trước thao tác. Bốn trang trí nhỏ giữ art Photoroom đã được người dùng làm sạch ở `Decorations/`. Sheet tạo ra là nguồn vật liệu,không phải ảnh sprite atlas. Không kèm ảnh game/asset trò chơi bên thứ ba.

## Nguồn nghiên cứu phong cách

Các trang cửa hàng chính thức của nhà phát hành/phát triển:

- https://store.steampowered.com/app/955900/Amazing_Cultivation_Simulator/
- https://store.steampowered.com/app/1468810/Tale_of_Immortal/
- https://store.steampowered.com/app/1696440/The_Matchless_Kungfu/

Nguồn xác lập bối cảnh tu tiên/võ hiệp;bảng màu,nền dịu và xử lý đá phẳng là định hướng art của dự án,không phải tuyên bố về thuật toán các trò chơi đó. Một số lượt lấy ảnh chính thức thất bại;không khẳng định đã phân tích đầy đủ ảnh.


## Assets/_Project/Art/Models/Worker

# Thợ — bộ tài nguyên Blender

Thợ ít đa giác nguyên bản: đồng phục xanh ngọc,mũ/áo bảo hộ,ba lô,găng,ủng và dụng cụ cầm tay. Hai bộ hoạt ảnh dùng chung lưới hình học và bộ xương.820 đỉnh,898 đa giác,16xương. Trọng số cứng phù hợp phong cách chia đoạn.

Source:`Artifacts/Worker/Worker.blend` nằm ngoài Assets để tránh Unity tự nhập Blender. Script dựng lại và danh mục ở cạnh source.

## Tệp hoạt ảnh

Mỗi FBX gồm lưới hình học,rig và một hoạt ảnh bake30FPS. `Normal_` và `Tired_` đều có Idle,Walk,Gather,Carry,Build,Rest. Bộ mệt dùng lưng khom,đầu thấp,bước ngắn,chu kỳ dài. Mọi clip chạy tại chỗ. Carry gợi ba lô có hàng,không kèm vật thể hàng tháo rời. Gather/Build cùng động tác vung dụng cụ nhưng khác nhịp.

## Thiết lập Unity

Nhập với `Rig > Animation Type = Generic`, `Avatar Definition = Create From This Model`,gốc `WorkerRig`. Bật `Import Animation` và `Loop Time` cho clip lặp. Dùng `Normal_Idle` làm mô hình hình ảnh; clip tệp khác nhắm cùng cây đối tượng. Tắt `Apply Root Motion`. Tách vật liệu và dùng URP/Lit với màu vật liệu nhúng nếu chương trình tô bóng FBX không hợp URP dự án.

## Đối chiếu hệ thống thợ và trạng thái hoạt ảnh

Ở phiên bản được rà soát,thợ mẫu đối tượng là biến thể Sodier,ghi đè lưới hình học tĩnh bằng `Assets/_Project/Art/Meshes/Zombie_Walk_0.asset`. Cả hai mẫu đối tượng không có Animator/SkinnedMeshRenderer. dự án dùng Entities1.4.5và Entities Graphics. Các FBX là tài nguyên,không phải giải pháp hoạt ảnh DOTS đã tích hợp.

| Điều kiện mô phỏng | Clip đề xuất |
|---|---|
| Không có việc đang chạy,vận tốc gần0 | Idle |
| MovementAgentComponent.velocity khác0,không mang hàng | Walk |
| WorkerGatherData bật,State=Gathering | Gather |
| Đang đi,CarryAmount>0 | Carry |
| BuilderComponent bật,State=Building | Build |
| Có trạng thái nghỉ rõ ràng | Rest |

WorkerGatherData/BuilderComponent bật/tắt được. thành phần tắt không được tiếp tục điều khiển hoạt ảnh việc cũ. chuyển động nên đọc vận tốc thật,không chỉ GoingToNode/ReturningDepot/GoingToSite,vì chúng có thể giữ khi bị chặn. mã thợ ở lần rà soát chưa có thành phần mệt/trạng thái nghỉ;Tired cần tham số hình ảnh mệt rõ ràng hoặc hệ thống lối chơi sau này.

Lượt tạo tài nguyên không đổi simulation thợ,tốc độ khai thác/di chuyển hoặc mẫu đối tượng thợ cũ. Tích hợp khi chạy cần cầu nối presentation giữa thực thể ECS và instance hình ảnh hoạt ảnh,hoặc renderer hoạt ảnh ECS. Chỉ gắn Animator thông thường chưa tạo cầu nối đó.

## Kiểm chứng

Đã nhập lại12FBX vào Blender:16xương,khoảng khung hình không rỗng và chuyển động đỉnh theo xương đo được đều đạt. Chi tiết:`Artifacts/Worker/validation.json`. Đã render/xem Normal_Idle,Tired_Rest. Chưa xác minh nhập Unity/phát trong trò chơi. Đây là hoạt ảnh khởi đầu thiết kế thủ công,không phải ghi chuyển động người thật.


## Assets/_Project/Art/Design/UI/XianxiaRTS/Concepts

# Giao diện RTS tu tiên — bản phác thảo v1

Tạo bằng công cụ image_gen tích hợp và skill imagegen. Đây là ảnh phác thảo mỹ thuật,chưa phải ảnh sprite đã cắt hoặc UI Unity hoạt động.

## Định hướng mỹ thuật

- Bố cục RTS cổ điển:thanh tài nguyên trên cùng,minimap dưới trái,khung lựa chọn dưới giữa,lưới lệnh dưới phải.
- Ngọc tối,gỗ than,đồng cũ,chữ trên giấy da;họa tiết mây tiết chế.
- Tài nguyên:Linh mộc,Linh thạch,Đan dược,Linh khí,Môn đồ.
- Lựa chọn:Kiếm tu/Trúc Cơ,máu/năng lượng,chỉ số,chân dung nhóm.
- Lệnh:di chuyển,tấn công,bảo vệ,dừng,phi kiếm,kiếm trận,thiền,hồi máu,tông môn,mỏ linh thạch,luyện đan,nghiên cứu.
- Bộ UI:trạng thái nút,gợi ý,hàng đợi huấn luyện,thông tin nhà,nghiên cứu tu luyện,trình đơn.

## Mô tả tạo ảnh


## Assets/_Project/Art/Design/Characters/XianxiaRTS/References

# Tham chiếu dựng nhân vật RTS tu tiên

Tạo bằng image_gen tích hợp và skill imagegen. PNG là phác thảo để dựng mô hình,chưa phải model3D. Chưa tạo/kiểm chứng kết quả chuyển ảnh sang3D.

- Kiếm tu:áo trắng ngà/xanh ngọc,dáng mảnh.
- Luyện đan sư:áo xanh ngọc,búi tóc gọn,bình đan đất nung.
- Đệ tử lao động:áo nâu nhạt,dáng chắc,tay áo ngắn.

## Cách dựng mô hình dự kiến

Dùng riêng mặt trước cho công cụ chỉ nhận một ảnh. Không đưa cả bảng ba nhân vật xoay nhiều góc vào công cụ một ảnh,vì có thể tạo nhiều cơ thể. Với công cụ nhận nhiều góc rõ ràng,đưa mặt trước,bên,sau vào từng ô tương ứng.

Tách vũ khí/dụng cụ khỏi lưới hình học nhân vật. Vạt áo là bề mặt kín đơn giản;vẽ nếp nhỏ/viền thay vì điêu khắc. Tóc chia vài khối đặc. Giữ vòng cạnh vai,khuỷu,hông,gối để hoạt ảnh.

Ngân sách khởi đầu đề xuất (mục tiêu thiết kế,chưa phải kết quả đo):2,000–4,000tam giác/lính ởLOD0,800–1,500 ởLOD1,một atlas512px/1024px mỗi nhân vật. Chỉnh sau khi thử khoảng cách camera RTS và số lính thật.

Kiểm tra lưới hình học tạo ra:tay dính,ngón tay,khoảng chân,hình học trùng,độ dày áo,dáng phía sau,UV và biến dạng rig. Góc bên là phép chiếu A-pose,có thể nhìn chi chồng nhau. Bất nhất nhỏ trong ảnh là điểm chưa rõ của phác thảo,cần quyết định khi dựng.

Hình ảnh có mặt phẳng đa giác không chứng minh lưới hình học,topology,atlas,rig hoặc hiệu năng khi chạy.

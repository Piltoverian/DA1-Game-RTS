# Multiplayer — chuẩn bị sau FOW local, 17/10/2026

Bước local đã được người dùng chốt để chuyển sang multiplayer. [Handoff FOW](../Maintenance/FogOfWarHandoff.md) ghi source, bằng chứng và việc đầu tiên; [render local](../Architecture/FogOfWarRendering.md) mô tả implementation đang chạy. Tài liệu này là nguồn chính cho quyết định multiplayer mới nhất, thay các đề xuất transport/schema cũ khi có khác biệt.

## Hiện trạng và mục tiêu

Project dùng Unity 6000.3.13f1, Entities 1.4.5; chưa có com.unity.netcode trong manifest. Chưa cài/pin Netcode hoặc nghiệm thu server+2client. Hướng chọn là Netcode for Entities, server authoritative; mục tiêu cũ simulation 20Hz, 2.000 unit, map 512×512, 2–10 player vẫn là mục tiêu, chưa có đo tải chứng minh. [Lý do kiến trúc](ArchitectureChoice_2026-10-12.md).

Bản đầu chuyển đổi phải bao phủ chức năng hiện có: movement, combat/đạn, build/repair/cancel, gather/resource, production/train/research, health/owner/team, UI, FOW và terrain. Có thể kiểm chứng từng đối tượng bên trong, nhưng không dừng bàn giao ở Worker/TownHall. Không tự sửa luật gameplay để thuận tiện cho network.

## Server ECS và client Mono

Người dùng muốn client code bằng MonoBehaviour để dễ quản lý. Camera, input, render, animation, UI và ký ức FOW thuộc Mono; một adapter đưa dữ liệu giữa ECS/Netcode và Mono. Client Netcode World vẫn cần để nhận Ghost/RPC, không có nghĩa loại bỏ ECS nội bộ Netcode. Hiện presentation local vẫn có ECS systems; yêu cầu chuyển Mono chưa được code.

```text
Client Mono input → RPC adapter → server validate → backend command/request hiện có
Server ECS simulation → Ghost serializers + relevancy → client Ghost data
    → adapter → Mono render/UI/camera + ký ức building/resource
Terrain Explored đầy đủ → đường truyền phù hợp → client terrain/mask
```

Ghost authoring có phiên bản prefab Server / InterpolatedClient / PredictedClient; PrefabType quyết định component có mặt ở đâu, GhostField quyết định field đồng bộ. System gameplay cũng phải giới hạn World phù hợp, không chỉ strip component. Phần hình Mono được bind từ prefab/asset phía client, không clone gameplay server để dựng hình.

## Định nghĩa, data runtime và template

- Định nghĩa bất biến đã có trong registry hai phía: dùng GameBlobReference theo CatalogKey + ID string hiện có, resolve blob local và cast đúng loại. Không thêm ID int thay BasedSO.ID string ở lượt này.
- Trạng thái runtime: chỉ phần đang hoạt động/cần hiển thị. Ability hiện tại, job hiện tại; không gửi danh sách khả năng hay toàn queue chỉ để render. Resource chỉ Amount vì type được định lúc gen; phía client phải được khởi tạo đúng type nếu cần UI.
- Schema local nullable FogEntityRenderData chưa phải Ghost schema. Kiểm kê field hiện có trước khi quyết định dùng GhostField, component variation hay template, tránh tạo thêm struct trùng ý nghĩa.
- Template là quy tắc sinh serializer lúc compile. Đường nhận chung có thể dispatch theo khóa loại như GameReg, nhưng mỗi loại vẫn có layout và encoder/decoder hai phía thống nhất. Không tự cast byte thành blob mà không có schema.
- BlobAssetReference, unsafe pointer và Entity.Index/Version là tham chiếu local. Muốn gửi nội dung blob phải serialize nội dung và tái dựng; definition có sẵn nên gửi reference thay vì toàn blob lặp lại.
- Chọn Interpolated/Predicted ở Ghost authoring. Field smoothing/quantization theo serializer/variant/template; không thêm một hệ nội suy Mono trùng Netcode. Mono trình bày giá trị đã được adapter lấy đúng timeline. Chưa chốt mode cuối cho từng ghost.

Tài liệu Unity đã đọc trong ca: [Ghost prefab/component variants](https://docs.unity.cn/Packages/com.unity.netcode@1.5/manual/ghost-snapshots.html), [serializer templates 1.14](https://docs.unity3d.com/Packages/com.unity.netcode@1.14/manual/ghost-types-templates.html), [blob assets](https://docs.unity.cn/Packages/com.unity.entities@1.0/manual/blob-assets-concept.html). Các phiên bản tài liệu này là tham khảo đã đọc, **không phải package version đã chọn tương thích**. Khi chọn package phải kiểm tra API/codegen/template đúng phiên bản, kể cả enum, nullable và kiểu payload được hỗ trợ; không copy nguyên pseudocode trong chat thành API đã xác minh.

## Command qua RPC

Người dùng chốt RPC bọc đúng payload command/request hiện có, không thiết kế thêm schema gameplay song song. Nhận RPC → lấy PlayerID từ connection → kiểm tra quyền/reference → chuyển vào backend tương ứng.

- Giữ CommandType/GameBlobReference/vị trí/target và ý nghĩa hiện tại; ánh xạ entity qua identity của Ghost thay vì truyền handle local nguyên trạng.
- Entity SourcesEntity đang giữ buffer local không thay thế nội dung danh sách trên mạng; adapter truyền/khôi phục danh sách khi payload cần.
- CancelBuildingRequest vẫn đi riêng; FIFO chỉ của build. Không ép mọi request vào CommandQueue.
- Lệnh một lần không prediction phù hợp IRpcCommand; input theo tick/prediction có thể cần ICommandData, chưa tự chuyển toàn bộ lệnh RTS sang command stream.
- RPC delivery reliable không tự xác định tick áp dụng, tính hợp lệ gameplay hoặc phục hồi trạng thái khi reconnect. Chỉ thêm sequence/ack ứng dụng nếu có yêu cầu xác định và ca kiểm chứng; không tự dựng lại phần Netcode đã xử lý.

[Unity RPC](https://docs.unity3d.com/Packages/com.unity.netcode@1.14/manual/rpcs.html) và [command stream](https://docs.unity.cn/Packages/com.unity.netcode@1.0/manual/command-stream.html).

## FOW và lifecycle

Server quyết định visibility theo connection/player và luật team. Relevancy có thể ngừng replicate/despawn Ghost ở một client dù entity server còn sống. Client giữ ký ức building/resource độc lập Ghost; unit/đạn khuất thì bỏ hình. Cần thiết kế identity ổn định trong vòng đời đối tượng và đường xóa ký ức khi nhìn lại footprint trống. Không coi despawn là tín hiệu chết để chạy death animation. [Unity relevancy](https://docs.unity.cn/Packages/com.unity.netcode@1.0/manual/optimizations.html#relevancy).

Không đồng bộ thay đổi mới của building/resource đang khuất. Entity mới xây trong ô đã Explored không tự được biết. Reconnect/late join cần chính sách phục hồi Explored và ký ức; chưa có implementation. Picking/panel UI/targeting phải dùng dữ liệu được phép, không chỉ ẩn mesh.

## Terrain và session

Một map gen một lần, bất biến trong trận; client nhận kết quả, không chạy lại generator. Theo quyết định hiện tại, payload terrain là **full tập Explored**, không lọc theo cache; không phải toàn bộ map chưa khám phá. Truyền metadata hình học width/height/cellSize/origin/TerrainBaseLevel cùng dữ liệu cần thiết; unknown giữ đen. Không gửi trước resource/spawn/terrain bí mật chỉ để dựng map.

Không thêm WorldId/MapId/MapRevision vào mỗi frame FOW. Session/connection và vòng đời trận chịu trách nhiệm routing/reset; tick/identity/header nội bộ Netcode vẫn do Netcode quản lý. Revision/hash của MapTerrainSnapshot nội bộ hiện có không bị xóa bởi quyết định này. Cách đóng gói/chia khối/Ready sẽ chốt khi chọn transport, không dồn map vào một RPC lớn. [Tải trận và bản đồ](ServerMapLoading.md).

## Thứ tự triển khai

1. Kiểm kê component/system/prefab/caller của Main thành server simulation / dữ liệu Ghost / client Mono / adapter, bao phủ chức năng hiện có. Đây là việc đầu tiên, chưa cài package khi bảng còn mơ hồ.
2. Kiểm tra và chốt phiên bản Unity–Entities–Netcode–Transport, trình pipeline cụ thể; sau đó cấu hình World, Ghost authoring/variants và serializer/template.
3. Chuyển client presentation qua Mono, giữ local chạy qua cùng adapter; nối RPC command payload và backend hiện tại, không thay luật.
4. Nối FOW relevancy, static memory, terrain datum/Explored, UI/picking và session lifecycle.
5. Nghiệm thu dedicated server + 2 client: quyền điều khiển, mọi chức năng hiện có, khuất/thấy lại/destroy, đổi trận, disconnect/reconnect, mất gói và tài nguyên. Sau correctness mới đo tải mục tiêu.

Không có server/transport/config deployment đã được tạo. Không tự stage/commit/push/PR/merge. Lượt docs hiện tại chỉ cập nhật tài liệu, không chạy lại runtime tests hoặc cài dependency.

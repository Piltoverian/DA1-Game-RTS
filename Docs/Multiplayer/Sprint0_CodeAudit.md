# Sprint 0: Báo Cáo Kiểm Toán Hạ Tầng Code (Code Infrastructure Audit)

> **Cập nhật 2026-10-06:** Multiplayer là giai đoạn tiếp theo; đã chọn [server tạo map và truyền kết quả](ServerMapLoading.md). Audit bên dưới là snapshot: cần đối chiếu lại code, đặc biệt player identity đã được sửa cho test local; chưa thay thế identity từ session mạng.

> **Mục đích**: Rà soát toàn bộ mã nguồn C# và ECS Systems hiện tại để phát hiện các điểm nghẽn kiến trúc, các vị trí hardcode, và chuẩn bị sẵn các hợp đồng dữ liệu (Data Contracts) trước khi bắt tay vào tích hợp mạng ở Sprint 1. Phần dữ liệu ScriptableObjects/Civs sẽ do Game Designer cấu hình sau.

> [!NOTE]
> **Snapshot ưu tiên 2026-09-27 (đã được thay bởi cập nhật ở trên):** Các đầu việc Sprint 0 trong tài liệu này đã được chuyển vào backlog. Nhóm hiện ưu tiên Hạng mục B theo [MapGen_INDEX.md](../MapGeneration/MapGen_INDEX.md), sau đó mới quay lại `LocalPlayerManager`, data contracts và match lifecycle. Nội dung audit bên dưới vẫn là backlog kỹ thuật hợp lệ.

---

## 🚨 1. Các Điểm Nghẽn & Lỗi Kiến Trúc Được Phát Hiện

### ① Lỗi Định danh Người chơi Cục bộ (Local Player Identity)
* **Vị trí**: `Assets/_Project/Scripts/Runtime/Presentation/Input/GameManager/Manager/SelectManager.cs` (Dòng 25-26).
* **Đoạn code hiện tại**:
  ```csharp
  PlayerContextAuthoring playerContextAuthoring = FindAnyObjectByType<PlayerContextAuthoring>();
  currentContext = playerContextAuthoring;
  ```
* **Vấn đề cốt tử**:
  1. `PlayerContextAuthoring` là MonoBehaviour nằm trong SubScene. Khi build game chạy độc lập (hoặc khi đóng SubScene trong Editor), toàn bộ SubScene được bake thành Entity thuần túy trong RAM. Lệnh `FindAnyObjectByType` sẽ trả về `null` ➔ Gây lỗi `NullReferenceException` liên tục mỗi frame trên các script điều khiển (`UnitController`, `CommandButton`, `CommandMenu`, `UnitImage`).
  2. Cả 2 máy người chơi mở game lên đều tự gán mình là `playerId = 0`. Client không có cách nào biết mình là `Player 1`.
* **Giải pháp**: Xây dựng `LocalPlayerManager.cs` quản lý `LocalPlayerId` độc lập, không phụ thuộc vào `PlayerContextAuthoring`.

---

### ② Thiếu Cơ chế Đóng Băng Mô phỏng Trước Trận Đấu (Match State Lifecycle)
* **Vị trí**: Toàn bộ hệ thống trong `Assets/_Project/Scripts/Runtime/Simulation/`.
* **Vấn đề cốt tử**:
  * Khi SubScene vừa được nạp, toàn bộ các system ECS (`ShootAttackSystem`, `WorkerGatherSystem`, `ProductionSystem`, `MovementAgentPathRequestSystem`) đều chạy ngay lập tức.
  * Nếu máy Host load xong trong 2 giây còn máy Client mất 8 giây, thì trong 6 giây chênh lệch, nông dân của Host đã đi khai thác tài nguyên và quân Host đã tràn sang phá nhà Client trước khi Client kịp nhìn thấy bản đồ.
* **Giải pháp**:
  * Bổ sung cờ `MatchPlayingTag : IComponentData`.
  * Các hệ thống Gameplay gắn điều kiện: `state.RequireForUpdate<MatchPlayingTag>();`.
  * Chỉ khi nhận được tín hiệu Handshake 100% từ cả 2 máy, Host mới add `MatchPlayingTag` để mở van mô phỏng cùng một tích tắc.

---

## 🌟 2. Điểm Sáng Hạ Tầng Hiện Có (Sẵn Sàng Tái Sử Dụng)

1. **`SelectSystem.cs` đã hỗ trợ lọc theo Player**:
   * Code tại dòng 137: `if (selectable.playerID != request.playerId) continue;`.
   * Hệ thống quét chuột chọn lính trong ECS đã được thiết kế chuẩn mực để ngăn người chơi chọn nhầm lính đối thủ.
2. **`TechUnlockHelper.cs` và `PlayerContext.cs` đã tích hợp `CivID`**:
   * `PlayerContext` đã có sẵn trường `FixedString64Bytes CivID`.
   * `TechUnlockHelper` đã có sẵn hàm tra cứu cây công nghệ và điều kiện mở khóa theo `CivID` và `CivBlob`. Khi Ban/Pick xong, chỉ cần nạp mã Civ vào `PlayerContext` là toàn bộ logic công nghệ tự động kích hoạt chính xác.

---

## 📄 3. Hợp Đồng Dữ Liệu Trung Gian (Data Contracts)

Cần tạo file `MatchDataContracts.cs` để làm cầu nối giữa Tầng Ban/Pick và Tầng Trận Đấu ECS:

```csharp
using System;
using Unity.Collections;

public enum DraftMode : byte
{
    BlindPick,   // Chọn tự do, vào trận mới biết đối thủ
    EsportsDraft // Cấm chọn theo lượt 30s
}

[Serializable]
public struct PlayerSlotData
{
    public int PlayerId;                 // 0 cho Host, 1 cho Client
    public string PlayerName;
    public FixedString64Bytes CivID;     // Mã Civ được chọn từ Ban/Pick
    public int SpawnIndex;               // Vị trí xuất phát (0 hoặc 1)
    public bool IsReady;
}

[Serializable]
public struct MatchInitConfig
{
    public string MapId;
    public DraftMode Mode;
    public PlayerSlotData Player0;
    public PlayerSlotData Player1;
}
```

---

## 🛠️ 4. Kế Hoạch Thực Hiện Cho Session Kế Tiếp

Trong session tiếp theo, chúng ta sẽ bắt tay xử lý dứt điểm 3 file mã nguồn cốt lõi này:

1. **Tạo `LocalPlayerManager.cs`**:
   * Cung cấp biến tĩnh `LocalPlayerManager.LocalPlayerId` (mặc định Host = 0, Client = 1).
   * Cập nhật các điểm gọi cũ (`SelectManager`, `UnitController`, `CommandMenu`) sang dùng API mới, triệt tiêu nguy cơ crash khi đóng SubScene.
2. **Tạo `MatchDataContracts.cs`**:
   * Đặt trong thư mục `Game(New Simulation Logic)/Multiplayer/Contracts/` chứa các struct hợp đồng ở trên.
3. **Tạo `MatchStateComponent.cs` & Cập nhật `MatchPlayingTag`**:
   * Thiết lập cơ chế khóa van mô phỏng ECS trong lúc chờ kết nối mạng và màn hình tải trận.


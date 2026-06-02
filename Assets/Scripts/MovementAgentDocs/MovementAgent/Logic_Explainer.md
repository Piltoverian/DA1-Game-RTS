# Movement Agent - Giải thích Logic & Toán học

Tài liệu này giải thích các thành phần cốt lõi trong hệ thống di chuyển (Movement Agent) của project RTS. Các đoạn code được trích dẫn trực tiếp từ các file `Helper` để làm rõ cơ chế vận hành.

---

## 1. Hệ thống Lưới (GridHelper)
Hệ thống di chuyển dựa trên một lưới 2D phẳng (Grid). Việc chuyển đổi giữa tọa độ thế giới (float3) và tọa độ lưới (int2) là bước đầu tiên để truy cập dữ liệu Flow Field.

### Chuyển đổi từ World sang Grid
Sử dụng phép chia cho `cellsize` và làm tròn xuống (`floor`) để tìm đúng ô lưới mà Unit đang đứng.
```csharp
public static int2 WorldToGrid(float3 worldPos, GridComponent grid)
{
    float xLocal = (worldPos.x - grid.origin.x) / grid.cellsize;
    float yLocal = (worldPos.z - grid.origin.z) / grid.cellsize;
    return new int2((int)math.floor(xLocal), (int)math.floor(yLocal));
}
```

---

## 2. Quản lý Flow Field (FlowFieldHelper)
Hệ thống sử dụng **Reference Counting** (Đếm tham chiếu) để quản lý bộ nhớ của Flow Field. Điều này đảm bảo rằng:
- Khi có ít nhất 1 Unit sử dụng Flow Field, nó sẽ được giữ lại trong Cache.
- Khi Unit cuối cùng rời đi hoặc đổi mục tiêu, Flow Field sẽ bị đánh dấu để dọn dẹp.

### Cơ chế gán Field và đếm Ref
```csharp
public static void AssignFieldToMoveComponent(...)
{
    // 1. Giảm Ref của Field cũ
    if (unit.FieldEntity != Entity.Null && em.Exists(unit.FieldEntity))
    {
        var oldRef = em.GetComponentData<FlowFieldRefCount>(unit.FieldEntity);
        oldRef.value--;
        ecb.SetComponent(unit.FieldEntity, oldRef);
    }

    // 2. Gán Field mới
    unit.FieldEntity = field;
    
    // 3. Tăng Ref của Field mới
    var newRef = em.GetComponentData<FlowFieldRefCount>(field);
    newRef.value++;
    ecb.SetComponent(field, newRef);
}
```

---

## 3. Toán học Di chuyển (UnitMovementMath)
Đây là "trái tim" của hệ thống di chuyển, xử lý việc hợp nhất các lực đẩy và né tránh.

### 3.1 Nội suy hướng di chuyển (Bilinear Interpolation)
Thay vì Unit đi giật cục theo từng ô lưới, chúng ta lấy 4 ô xung quanh vị trí Unit và nội suy hướng di chuyển để Unit rẽ hướng mượt mà hơn.
```csharp
public static float3 CalculateFlowVelocity(...)
{
    // Tìm 4 ô hàng xóm (00, 10, 01, 11)
    // Nội suy theo trục X, sau đó nội suy theo trục Y
    float2 interpolatedDir = math.lerp(
        math.lerp(d00, d10, t.x),
        math.lerp(d01, d11, t.x),
        t.y
    );
    return new float3(interpolatedDir.x, 0, interpolatedDir.y) * speed;
}
```

### 3.2 Né vật cản Grid (Gradient Avoidance)
Thay vì chỉ check va chạm vật lý, hệ thống tính toán một "Vector Gradient" hướng ra xa các ô có chi phí cao (tường/vật cản). Lực này tỉ lệ nghịch với bình phương khoảng cách, giúp Unit không bao giờ đâm sầm vào tường.
```csharp
if (gridCosts[idx].cost >= 250) // Là vật cản
{
    float2 diff = new float2(worldPos.x - obstacleWorldPos.x, worldPos.z - obstacleWorldPos.z);
    float distSq = math.lengthsq(diff);
    // Lực đẩy mạnh dần khi càng gần vật cản
    gradient += math.normalizesafe(diff) / math.max(0.1f, distSq);
}
```

### 3.3 Hội tụ bầy đàn (First-come-first-settled)
Thay vì các Unit liên tục xô đẩy nhau để giành giật một `slotTarget`, hệ thống áp dụng chiến lược hội tụ:
- **Settled (Đã neo đậu)**: Khi một Unit đi vào bán kính đích (ví dụ `0.1` đơn vị) và vận tốc đã chậm lại, nó sẽ chuyển trạng thái sang `isSettled = true`. Lúc này Unit trở thành một "vật cản tĩnh" cực kỳ vững chắc.
- Ưu điểm: Các Unit tới sau bắt buộc phải né Unit đã "neo đậu" bằng thuật toán ORCA, tạo ra đội hình bao quanh mục tiêu mượt mà thay vì dồn cục.

---

## 4. Local Avoidance (Mô hình Hybrid ORCA & Separation)
Hệ thống sử dụng mô hình kết hợp giữa không gian Vận tốc (Velocity-based) và Vị trí (Position-based). Trọng tâm bao gồm:

### 4.1 Không gian Vận Tốc - ORCA (Optimal Reciprocal Collision Avoidance)
Dựa trên thuật toán cắt nửa mặt phẳng (Half-plane constraints), hệ thống sẽ tìm một vận tốc an toàn:
1. **Velocity Obstacle (VO)**: Tính toán tập hợp tất cả các vận tốc tương đối sẽ gây ra va chạm trong khoảng thời gian `timeHorizon` (thường là 1-2 giây) đối với các Unit xung quanh.
2. **Half-plane Creation**: Tạo một ràng buộc dưới dạng đường thẳng giới hạn (Line), trong đó vùng hợp lệ bảo đảm khoảng cách an toàn. Hệ số `reciprocalFactor` (0.5) yêu cầu cả 2 Unit đều có trách nhiệm nhường đường, giúp giải quyết nút thắt cổ chai.
3. **Linear Programming (LP)**: Giải bài toán quy hoạch tuyến tính 2D qua 3 bước (LP1, LP2, LP3) để tìm ra vận tốc an toàn nhất nhưng có độ ưu tiên cao nhất, vẫn đảm bảo bám sát `PreferredVelocity`.

```csharp
// Tạo đường ORCA Line giữa 2 Unit
Line line = ORCAMath.CreateAgentLine(
    relPos, agentVel, neighborVel, combinedRadius, timeHorizon, invDt, 0.5f);
```

### 4.2 Lực đẩy không gian (Position-based Separation)
ORCA hoạt động rất tốt cho các va chạm dự báo trong tương lai. Nhưng nếu 2 Unit rủ nhau đi chung và _chồng luôn_ vào nhau tại cùng khung hình (Hard Overlap), ORCA không kịp đẩy xa. Lớp Separation giải quyết việc này:
- Tính khoảng cách vật lý của Unit. Nếu nhỏ hơn `combinedRadius` (chồng chéo), tính Vector phân li:
```csharp
float2 diff = new float2(worldPos.x - nPos.x, worldPos.z - nPos.z);
// Lực đẩy dạt ra ngoài tỉ lệ với mức độ chồng lấn
separationForce += math.normalizesafe(diff) * overlapAmount * SeparationMultiplier;
```
Từ sức đẩy Separation cộng với kết quả tính của ORCA, ta có Vận tốc chuẩn xác và hoàn toàn không bị overlap khi di chuyển đoàn lớn.

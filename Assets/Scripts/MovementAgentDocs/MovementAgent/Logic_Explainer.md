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

### 3.3 Tính toán nguy hiểm (Time-To-Collision Lite)
Mức độ nguy hiểm (`Danger`) của một tia trong Context Steering không chỉ phụ thuộc vào khoảng cách mà còn phụ thuộc vào **vận tốc tương đối** của hai Unit.
- Nếu hai Unit đi cùng hướng (Consensus cao): Giảm nguy hiểm để chúng có thể đi sát nhau (bầy đàn).
- Nếu hai Unit đối đầu: Tăng nguy hiểm để chúng né nhau từ sớm.

```csharp
float consensus = math.dot(math.normalizesafe(myVel), math.normalizesafe(neighborVel));
if (consensus > 0.8f) danger *= 0.1f;    // Đi song song
else if (consensus < -0.5f) danger *= 1.5f; // Đi đối đầu
```

---

## 4. Local Avoidance (Context Steering)
Hệ thống sử dụng **Context Steering** với 16 hướng (Resolution = 16).
1. **Interest Map**: Các tia chỉ về phía Flow Field hoặc Slot đội hình.
2. **Danger Map**: Các tia bị cản bởi Unit khác hoặc tường.
3. **Solver**: Chọn tia có `Interest` cao nhất và `Danger` thấp nhất để làm hướng đi cuối cùng.

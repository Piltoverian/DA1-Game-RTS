using Unity.Entities;
using Unity.Mathematics;

public struct MovementAgentComponent : IComponentData
{
    public float speed;
    public bool hastarget;
    public Entity FieldEntity;
    public float3 currentworldtarget;
    public float3 realTarget;      // Điểm đến thực tế (đã tính theo đảo)
    public float3 slotTarget;      // Tọa độ vị trí cụ thể trong đội hình
    public bool useSlotTarget;     // Flag chuyển đổi từ FlowField sang Direct Slot Steering
    public float3 velocity;        // Velocity thực tế (output từ ORCA, collision-free)
    public float3 preferredVelocity; // Velocity mong muốn (output từ TargetSystem, input cho ORCA)
    public float3 lookAtPoint;     // Điểm mà lính sẽ nhìn vào sau khi dừng hẳn
}

public enum FormationType { Box, Circle }

public struct MovementAgentFormationComponent : IComponentData
{
    public FormationType SelectedType;
}

public struct MovementAgentAvoidanceComponent : IComponentData
{
    public float radius;
    public int gridIndex;
    public float3 avoidanceForce; // Lực đẩy context steering (hướng mồi)
    public float3 lastAvoidDir; // COMMITTED avoidance direction (Anti-Jitter)
    public float3 separationForce; // Lực đẩy vật lý "cứng" khi đã bị chồng lấn
    public bool IsStatic;          // Trạng thái khóa tĩnh khi đã đến slot
    public float avoidTimer; // Timer to hold lastAvoidDir
    public float closestDistance; // Actual distance to closest neighbor (Fix 4)
    public float3 closestNeighborNormal; // Hướng thoát khỏi neighbor gần nhất (không bị triệt tiêu như separationForce)
    public float sidePreference; // -1 (left) or 1 (right) for consistent avoidance
    public int neighborCount; // Number of neighbors
}


public struct MovementSteeringComponent : IComponentData
{
    public float arrivalRadius;
    public float formationRange; // Khoảng cách bắt đầu dàn hàng (ví dụ 20m)
    public float stoppingDistance;
    public bool isSettled;
    public float rotationSpeed;
    // Các trường mới để phát hiện bị kẹt
    public float stuckTime;
    public float3 lastPosition;
    public float3 lastVelocity; // Theo dõi hướng frame trước để khử Jitter
    public float minDistanceToTarget; // Khoảng cách nhỏ nhất từng đạt được tới đích (Progress Tracking)
}

// --- CONTEXT STEERING COMPONENTS ---

public struct ContextMapElement : IBufferElementData
{
    public float Interest;
    public float Danger;
}

public struct ContextHistoryElement : IBufferElementData
{
    public float LastInterest;
}

public struct ContextSteeringConfig : IComponentData
{
    public int Resolution; // 16 or 32
    public float H_Alpha;  // EMA Hysteresis coefficient (0.1 - 0.2)
    public float DangerThreshold; // threshold to mask interest
}

public struct TargetChangeRequest : IComponentData
{
    public float3 newWorldTarget;
}


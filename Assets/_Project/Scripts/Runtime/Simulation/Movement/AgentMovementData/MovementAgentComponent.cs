using Unity.Entities;
using Unity.Mathematics;

public struct MovementAgentFieldCleanUpData : ICleanupComponentData
{
    public Entity FieldEntity;
}

public enum TargetResolutionKind : byte
{
    None = 0,
    Direct = 1,
    BuildingBlockage = 2,
    NaturalResolved = 3
}

public struct MovementAgentComponent : IComponentData
{
    public float speed;
    public bool hastarget;
    public Entity FieldEntity;
    public float3 currentworldtarget;
    public float3 realTarget;      // Điểm đến thực tế (đã tính theo đảo)
    public int2 navigationTargetCell;
    public uint targetResolutionGeneration;
    public TargetResolutionKind targetResolutionKind;
    public float3 velocity;        // Velocity thực tế (output từ ORCA, collision-free)
    public float3 preferredVelocity; // Velocity mong muốn (output từ TargetSystem, input cho ORCA)
    public float3 lookAtPoint;     // Điểm mà lính sẽ nhìn vào sau khi dừng hẳn
}

public struct MovementAgentAvoidanceComponent : IComponentData
{
    public float radius;
    public int gridIndex;
    public float3 separationForce; // Lực đẩy vật lý "cứng" khi đã bị chồng lấn
    public bool IsStatic;          // Trạng thái khóa tĩnh khi đã đến slot
    public float closestDistance;  // Khoảng cách tới neighbor gần nhất
    public int neighborCount;      // Số lượng neighbors xung quanh
}


public struct MovementSteeringComponent : IComponentData
{
    public float arrivalRadius;
    public float stoppingDistance;
    public bool isSettled;
    public float rotationSpeed;
    public float stuckTime;
    public float3 lastPosition;
    public float minDistanceToTarget; // Khoảng cách nhỏ nhất từng đạt được tới đích (Progress Tracking)
}


public struct TargetChangeRequest : IComponentData
{
    public float3 newWorldTarget;
}


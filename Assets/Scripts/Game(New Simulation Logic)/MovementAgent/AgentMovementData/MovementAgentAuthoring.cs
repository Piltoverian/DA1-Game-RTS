using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
public class MovementAgentAuthoring : MonoBehaviour
{
    public float speed = 10.0f;
    public float radius = 1.0f;
    public float arrivalRadius = 3.5f;
    public float stoppingDistance = 1.5f;
    public float rotationSpeed = 8.0f;

    [Header("Testing")]
    public bool useTestTarget = false;
    public float3 testTarget = new float3(50, 0, 50);

    private void OnValidate()
    {
        if (radius < 0.75)
        {
            radius = 0.75f; // Đặt giới hạn tối thiểu để tránh lỗi vật lý và tránh trường hợp overlapped
        }
        stoppingDistance = radius * 1.5f;

        // Arrival Radius: Cần đủ lớn để bù đắp cho vân tốc speed = 10.
        // Tỷ lệ 0.8 * Speed là tiêu chuẩn cho hãm phanh mượt.
        arrivalRadius = Mathf.Max(radius * 4f, speed * 0.8f);
    }


    public class Baker : Baker<MovementAgentAuthoring>
    {
        public override void Bake(MovementAgentAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new MovementAgentComponent
            {
                speed = authoring.speed,
                hastarget = authoring.useTestTarget, // Bật nếu dùng test target
                FieldEntity = Entity.Null,
                currentworldtarget = authoring.useTestTarget ? authoring.testTarget : float3.zero
            });

            AddComponent(entity, new MovementAgentAvoidanceComponent
            {
                radius = authoring.radius,
                gridIndex = -1,
                separationForce = float3.zero,
                IsStatic = false,
                closestDistance = 999f,
                neighborCount = 0
            });

            AddComponent(entity, new MovementSteeringComponent
            {
                arrivalRadius = authoring.arrivalRadius,
                stoppingDistance = authoring.stoppingDistance,
                isSettled = false,
                rotationSpeed = authoring.rotationSpeed,
                minDistanceToTarget = float.MaxValue,
                stuckTime = 0f
            });

            if (authoring.useTestTarget)
            {
                AddComponent(entity, new TargetChangeRequest { newWorldTarget = authoring.testTarget });
            }
        }
    }
}

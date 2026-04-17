using Unity.Entities;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using Unity.Mathematics;
public class MovementAgentAuthoring : MonoBehaviour
{
    public float speed = 10.0f;
    public float radius = 1.0f;
    public float arrivalRadius = 8.0f;
    public float formationRange = 25.0f; // Mặc định 25m cho dàn quân mượt
    public float stoppingDistance = 1.5f;
    public float rotationSpeed = 8.0f;
    public FormationType formationType = FormationType.Box;

    [Header("Testing")]
    public bool useTestTarget = false;
    public float3 testTarget = new float3(50, 0, 50);
    
    private void OnValidate()
    {
        if(radius<0.75)
        {
            radius = 0.75f; // Đặt giới hạn tối thiểu để tránh lỗi vật lý và tránh trường hợp overlapped
        }
        // Công thức tối ưu cho hệ thống Slotting:
        // Stopping Distance nên nhỏ hơn bán kính dãn cách Đội hình (2.2m)
        // để unit có thể thực sự chạm tới điểm slot của mình.
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
                currentworldtarget = authoring.useTestTarget ? authoring.testTarget : float3.zero,
                slotTarget = float3.zero,
                useSlotTarget = false
            });

            AddComponent(entity, new MovementAgentFormationComponent { SelectedType = authoring.formationType });

            AddComponent(entity, new MovementAgentAvoidanceComponent
            {
                radius = authoring.radius,
                gridIndex = -1,
                avoidanceForce = float3.zero,
                lastAvoidDir = float3.zero,
                separationForce = float3.zero,
                
                IsStatic = false,
                avoidTimer = 0f,
                closestDistance = 999f,
                sidePreference = (entity.Index % 2 == 0) ? 1.0f : -1.0f,
                neighborCount = 0
            });

            AddComponent(entity, new MovementSteeringComponent
            {
                arrivalRadius = authoring.arrivalRadius,
                formationRange = authoring.formationRange,
                stoppingDistance = authoring.stoppingDistance,
                isSettled = false,
                rotationSpeed = authoring.rotationSpeed,
                minDistanceToTarget = float.MaxValue,
                stuckTime = 0f
            });

            // --- CONTEXT STEERING BAKE ---
            int resolution = 16;
            AddComponent(entity, new ContextSteeringConfig
            {
                Resolution = resolution,
                H_Alpha = 0.15f,
                DangerThreshold = 0.8f
            });

            var mapBuffer = AddBuffer<ContextMapElement>(entity);
            var historyBuffer = AddBuffer<ContextHistoryElement>(entity);

            for (int i = 0; i < resolution; i++)
            {
                mapBuffer.Add(new ContextMapElement { Interest = 0, Danger = 0 });
                historyBuffer.Add(new ContextHistoryElement { LastInterest = 0 });
            }

            // Gắn Request ngay frame đầu để FlowField và Formation phát hiện được
            if (authoring.useTestTarget)
            {
                AddComponent(entity, new TargetChangeRequest { newWorldTarget = authoring.testTarget });
            }
        }
    }
}


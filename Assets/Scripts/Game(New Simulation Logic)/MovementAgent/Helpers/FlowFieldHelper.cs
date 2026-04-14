using Unity.Entities;
using Unity.Mathematics;
public static class FlowFieldHelper
{
    public static Entity FlowFieldInit(EntityManager etManager, float3 worldtarget, GridComponent grid, EntityCommandBuffer ecb)
    {
        Entity fieldEntity = etManager.CreateEntity();

        ecb.AddComponent(fieldEntity, new FlowFieldStatus { Value = FieldState.Requested });

        int2 targetcell = GridHelper.WorldToGrid(worldtarget, grid);
        ecb.AddComponent(fieldEntity, new FlowField
        {
            targetcell = targetcell,
            gridgeneration = grid.generation,
        });

        ecb.AddComponent(fieldEntity, new FlowFieldRefCount
        {
            value = 0
        });

        var fieldnodebuffer = ecb.AddBuffer<FieldNode>(fieldEntity);
        fieldnodebuffer.ResizeUninitialized(grid.width * grid.height);

        ecb.AddBuffer<IslandSeed>(fieldEntity);

        for (int i = 0; i < grid.width * grid.height; i++)
        {
            fieldnodebuffer[i] = new FieldNode
            {
                bestcost = int.MaxValue,
                direction = float2.zero
            };
        }

        return fieldEntity;
    }

    public static void AssignFieldToMoveComponent(
        ref MovementAgentComponent unit,
        ref MovementSteeringComponent steering,
        Entity field,
        float3 worldTarget,
        EntityCommandBuffer ecb,
        EntityManager em)
    {
        // 1. Giảm Ref cũ
        if (unit.FieldEntity != Entity.Null && em.Exists(unit.FieldEntity))
        {
            if (em.HasComponent<FlowFieldRefCount>(unit.FieldEntity))
            {
                var oldRef = em.GetComponentData<FlowFieldRefCount>(unit.FieldEntity);
                oldRef.value--;
                ecb.SetComponent(unit.FieldEntity, oldRef);
            }
        }

        unit.FieldEntity = field;
        unit.currentworldtarget = worldTarget;
        unit.hastarget = field != Entity.Null;
        unit.useSlotTarget = false; 
        steering.isSettled = false; 

       
        if (field != Entity.Null)
        {
           
            if (em.Exists(field))
            {
                if (em.HasComponent<FlowFieldRefCount>(field))
                {
                    var newRef = em.GetComponentData<FlowFieldRefCount>(field);
                    newRef.value++;
                    ecb.SetComponent(field, newRef);
                }
                else
                {
                    ecb.AddComponent(field, new FlowFieldRefCount { value = 1 });
                }
            }
        }
    }

    public static void ReleaseFieldFromMoveComponent(ref MovementAgentComponent unit, EntityCommandBuffer ecb, EntityManager em)
    {
        if (unit.FieldEntity != Entity.Null && em.Exists(unit.FieldEntity))
        {
            if (em.HasComponent<FlowFieldRefCount>(unit.FieldEntity))
            {
                var oldRef = em.GetComponentData<FlowFieldRefCount>(unit.FieldEntity);
                oldRef.value--;
                ecb.SetComponent(unit.FieldEntity, oldRef);
            }
        }
        unit.FieldEntity = Entity.Null;
        unit.hastarget = false;
        unit.useSlotTarget = false;
    }
}

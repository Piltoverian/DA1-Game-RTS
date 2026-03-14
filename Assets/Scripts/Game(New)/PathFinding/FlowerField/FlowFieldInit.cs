using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct FieldNode : IBufferElementData
{
    public int bestcost;
    public float2 direction;
}

public struct FlowField : IComponentData
{
    public int2 targetcell;
}

public struct FieldCreateRequest : IComponentData
{

}

public struct FlowFieldRefCount : IComponentData
{
    public int value;
}

public static class PathFindingHelper
{
    public static Entity FlowFieldInit(EntityManager etManager,float3 worldtarget,GridComponent grid,EntityCommandBuffer ecb)
    {
        Entity fieldEntity = etManager.CreateEntity();
        ecb.AddComponent(fieldEntity, new FieldCreateRequest());
        int2 targetcell = GridHelper.WorldToGrid(worldtarget, grid);
        ecb.AddComponent(fieldEntity,new FlowField
        {
            targetcell=targetcell,
        });
        ecb.AddComponent(fieldEntity, new FlowFieldRefCount
        {
            value = 0
        });
        var fieldnodebuffer =ecb.AddBuffer<FieldNode>(fieldEntity);
        fieldnodebuffer.ResizeUninitialized(grid.width*grid.height);

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
    ref UnitMovementComponent unit,
    Entity field,
    ref SystemState state)
    {
        var em = state.EntityManager;
        if (unit.FieldEntity == Entity.Null)
        {
            unit.FieldEntity = field;

            var refc = em.GetComponentData<FlowFieldRefCount>(field);
            refc.value++;

            em.SetComponentData(field, refc);
        }
        else
        {
            var oldRef = em.GetComponentData<FlowFieldRefCount>(unit.FieldEntity);
            oldRef.value--;

            em.SetComponentData(unit.FieldEntity, oldRef);
            unit.FieldEntity = field;

            var newRef = em.GetComponentData<FlowFieldRefCount>(field);
            newRef.value++;

            em.SetComponentData(field, newRef);
        }
    }
}
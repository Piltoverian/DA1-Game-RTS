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

public struct FieldUpdateRequest : IComponentData
{

}

public static class PathFindingHelper
{
    public static Entity FlowerFieldInit(EntityManager etManager,float3 worldtarget,GridComponent grid,EntityCommandBuffer ecb)
    {
        Entity fieldEntity = etManager.CreateEntity();
        ecb.AddComponent(fieldEntity, new FieldUpdateRequest());
        int2 targetcell = GridHelper.WorldToGrid(worldtarget, grid);
        ecb.AddComponent(fieldEntity,new FlowField
        {
            targetcell=targetcell,
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
}
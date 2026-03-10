using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class EntityTransformSync : MonoBehaviour
{
    public Entity entity;

    void LateUpdate()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var em = world.EntityManager;

        if (!em.Exists(entity))
            return;

        LocalTransform transformData = em.GetComponentData<LocalTransform>(entity);

        transform.position = transformData.Position;
    }
}
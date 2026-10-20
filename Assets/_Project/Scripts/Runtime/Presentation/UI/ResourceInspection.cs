using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

/// <summary>Presentation focus only. Never changes Selected, ownership, or command sources.</summary>
public static class ResourceInspection
{
    private static World world;
    private static Entity target;
    private static int playerId = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Clear();

    public static void Clear() { world = null; target = Entity.Null; playerId = -1; }

    public static bool TryGet(out Entity entity)
    {
        entity = Entity.Null;
        if (world == null) return false;
        if (world != World.DefaultGameObjectInjectionWorld || !world.IsCreated ||
            !SelectHelper.TryGetLocalPlayerId(out int currentPlayer) || currentPlayer != playerId ||
            !world.EntityManager.Exists(target) || !world.EntityManager.HasComponent<ResourceNodeData>(target) ||
            world.EntityManager.HasComponent<ResourceNodePendingConfig>(target) ||
            world.EntityManager.GetComponentData<ResourceNodeData>(target).Amount <= 0)
        { Clear(); return false; }
        entity = target;
        return true;
    }

    public static bool TryInspect(Camera camera, Vector2 screenPosition, EntityManager em, int localPlayer)
    {
        if (camera == null) return false;
        UnityEngine.Ray ray = camera.ScreenPointToRay(screenPosition);
        Entity candidate = Entity.Null;
        // Prefer the exact ECS hit, where a resource collider exists.
        using (var physics = em.CreateEntityQuery(typeof(Unity.Physics.PhysicsWorldSingleton)))
        {
            if (physics.CalculateEntityCount() == 1)
            {
                var input = new Unity.Physics.RaycastInput
                {
                    Start = ray.origin, End = ray.GetPoint(500f), Filter = Unity.Physics.CollisionFilter.Default
                };
                if (physics.GetSingleton<Unity.Physics.PhysicsWorldSingleton>().CastRay(input, out var hit) &&
                    em.HasComponent<ResourceNodeData>(hit.Entity)) candidate = hit.Entity;
            }
        }
        // Generated visual colliders use ResourceNodeReference; like the existing gather
        // bridge, resolve a nearby ECS node when the managed reference is not assigned.
        if (candidate == Entity.Null && UnityEngine.Physics.Raycast(ray, out var visualHit, 500f) &&
            visualHit.collider.GetComponentInParent<ResourceNodeReference>() != null)
        {
            var reference = visualHit.collider.GetComponentInParent<ResourceNodeReference>();
            if (em.Exists(reference.Entity) && em.HasComponent<ResourceNodeData>(reference.Entity)) candidate = reference.Entity;
            else
            {
                float best = 25f;
                using var query = em.CreateEntityQuery(typeof(ResourceNodeData), typeof(ResourceNodeTag), typeof(LocalTransform));
                using var nodes = query.ToEntityArray(Allocator.Temp);
                foreach (var node in nodes)
                {
                    if (em.HasComponent<ResourceNodePendingConfig>(node) || em.GetComponentData<ResourceNodeData>(node).Amount <= 0) continue;
                    float3 position = em.GetComponentData<LocalTransform>(node).Position;
                    if (em.HasComponent<BlockageData>(node)) position = em.GetComponentData<BlockageData>(node).GetWorldCenter(position);
                    float distance = math.distancesq(position.xz, new float2(visualHit.point.x, visualHit.point.z));
                    if (distance < best) { best = distance; candidate = node; }
                }
            }
        }
        if (candidate == Entity.Null || em.HasComponent<ResourceNodePendingConfig>(candidate) ||
            em.GetComponentData<ResourceNodeData>(candidate).Amount <= 0) return false;
        world = World.DefaultGameObjectInjectionWorld;
        target = candidate;
        playerId = localPlayer;
        return true;
    }
}

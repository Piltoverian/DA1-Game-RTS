using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PopulationSystem))]
public partial struct ProductionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<PlayerBootstrapState>(out var bootstrap) && bootstrap.Phase != PlayerBootstrapPhase.Ready) return;
        var em = state.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        using var reservedExits = new NativeList<float3>(Allocator.Temp);
        foreach (var pending in SystemAPI.Query<DynamicBuffer<PlayerPendingTech>>())
        {
            for (int i = pending.Length - 1; i >= 0; i--)
            {
                if (!em.Exists(pending[i].Producer) || !em.HasComponent<ProductionData>(pending[i].Producer))
                    pending.RemoveAt(i);
            }
        }
        foreach (var (production, transform, owner, work, health, entity) in
            SystemAPI.Query<RefRW<ProductionData>, RefRO<LocalTransform>, RefRO<EntityOwner>, RefRO<EntityWork>, RefRO<EntityHealth>>().WithEntityAccess())
        {
            if (health.ValueRO.CurrentHP <= 0f) { ProductionJobs.CancelAll(em, entity, true); continue; }
            if (em.HasComponent<BuildingConstruction>(entity) && em.GetComponentData<BuildingConstruction>(entity).Phase != ConstructionPhase.Completed) continue;
            var queue = em.GetBuffer<ProductionQueueElement>(entity);
            production.ValueRW.ExitBlocked = false;
            if (queue.IsEmpty) continue;
            var item = queue[0];
            if (item.PlayerID != owner.ValueRO.PlayerID) { ProductionJobs.CancelAll(em, entity); continue; }
            if (PlayerContextHelper.GetPlayerContextEntity(em, item.PlayerID, out var player, out var context) != FunctionResult.Success) continue;
            item.RemainingWork = math.max(0, item.RemainingWork - math.max(0, work.ValueRO.Rate) * SystemAPI.Time.DeltaTime);
            queue[0] = item;
            if (item.RemainingWork > 0) continue;
            if (item.Offer.Kind == ProductionKind.Research)
            {
                if (em.HasBuffer<PlayerPendingTech>(player))
                {
                    var pending = em.GetBuffer<PlayerPendingTech>(player);
                    for (int i = pending.Length - 1; i >= 0; i--)
                    {
                        if (pending[i].TechID == item.TechID && pending[i].Producer == entity)
                        {
                            pending.RemoveAt(i);
                            break;
                        }
                    }
                }
                if (em.HasBuffer<PlayerTechnology>(player))
                {
                    em.GetBuffer<PlayerTechnology>(player).Add(new PlayerTechnology { ID = item.TechID });
                }
            }
            else
            {
                Entity prefab = item.Offer.UnitPrefab;
                if (!em.Exists(prefab) || !em.HasComponent<UnitComponent>(prefab)) { ProductionJobs.RemoveFirst(em, entity, true); continue; }
                int cost = em.GetComponentData<UnitComponent>(prefab).PopulationCost;
                if (cost > 0 && (long)context.currentPopulation + cost > context.maxPopulation) continue; // Ready: do not restart work.
                var local = em.GetComponentData<LocalTransform>(prefab);
                float3 spawnOffset = production.ValueRO.SpawnOffset;
                float3 rallyOffset = production.ValueRO.RallyOffset;
                if (production.ValueRO.OffsetsFollowModel && em.HasComponent<PostTransformMatrix>(entity))
                {
                    var model = em.GetComponentData<PostTransformMatrix>(entity).Value;
                    spawnOffset = math.transform(model, spawnOffset);
                    rallyOffset = math.transform(model, rallyOffset);
                }
                local.Position = transform.ValueRO.TransformPoint(spawnOffset);
                if (production.ValueRO.OffsetsFollowModel)
                {
                    if (!ProductionExitPlacement.TryFind(em, entity, prefab, local.Position, reservedExits, out var exit))
                    {
                        production.ValueRW.ExitBlocked = true;
                        continue; // Keep the paid, completed job in the queue until its exit is free.
                    }
                    local.Position = exit;
                }
                Entity unit = ecb.Instantiate(prefab);
                ecb.SetComponent(unit, new EntityOwner { PlayerID = item.PlayerID });
                ecb.AddComponent(unit, new PopulationAccount { PlayerID = item.PlayerID, Used = math.max(0, cost) });
                if (cost > 0) PopulationSystem.Adjust(em, item.PlayerID, cost, 0);
                ecb.SetComponent(unit, local);
                if (em.HasComponent<Selectable>(prefab))
                {
                    var selectable = em.GetComponentData<Selectable>(prefab); selectable.playerID = item.PlayerID; ecb.SetComponent(unit, selectable);
                }
                if (em.HasComponent<MoveOverride>(prefab))
                {
                    var move = em.GetComponentData<MoveOverride>(prefab);
                    move.targetPosition = transform.ValueRO.TransformPoint(rallyOffset); move.targetApplied = false;
                    ecb.SetComponent(unit, move); ecb.SetComponentEnabled<MoveOverride>(unit, true);
                }
            }
            ProductionJobs.RemoveFirst(em, entity, false);
        }
        ecb.Playback(em); ecb.Dispose();
    }
}

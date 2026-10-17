using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PopulationSystem))]
public partial struct ProductionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<PlayerBootstrapState>(out var bootstrap) && bootstrap.Phase != PlayerBootstrapPhase.Ready) return;
        var em = state.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        List<(Entity Player, FixedString64Bytes TechID, TechEffect[] Effects)> completedResearch = null;
        foreach (var pending in SystemAPI.Query<DynamicBuffer<PlayerPendingTech>>())
        {
            for (int i = pending.Length - 1; i >= 0; i--)
            {
                if (!em.Exists(pending[i].Producer) || !em.HasComponent<ProductionData>(pending[i].Producer))
                    pending.RemoveAt(i);
            }
        }
        foreach (var (production, transform, owner, work, health, entity) in
            SystemAPI.Query<RefRO<ProductionData>, RefRO<LocalTransform>, RefRO<EntityOwner>, RefRO<EntityWork>, RefRO<EntityHealth>>().WithEntityAccess())
        {
            if (health.ValueRO.CurrentHP <= 0f) { ProductionJobs.CancelAll(em, entity, true); continue; }
            if (em.HasComponent<BuildingConstruction>(entity) && em.GetComponentData<BuildingConstruction>(entity).Phase != ConstructionPhase.Completed) continue;
            var queue = em.GetBuffer<ProductionQueueElement>(entity);
            if (queue.IsEmpty) continue;
            var item = queue[0];
            if (item.PlayerID != owner.ValueRO.PlayerID) { ProductionJobs.CancelAll(em, entity); continue; }
            if (PlayerContextHelper.GetPlayerContextEntity(em, item.PlayerID, out var player, out var context) != FunctionResult.Success) continue;
            item.RemainingWork = math.max(0, item.RemainingWork - math.max(0, work.ValueRO.Rate) * SystemAPI.Time.DeltaTime);
            queue[0] = item;
            if (item.RemainingWork > 0) continue;
            if (item.Offer.Kind == ProductionKind.Research)
            {
                if (!em.HasBuffer<PlayerTechnology>(player)) continue;
                if (!TechUnlockHelper.IsTechCompleted(em, player, item.TechID))
                {
                    if (!TryGetResearchEffects(em, item, out var effects)) continue;
                    completedResearch ??= new List<(Entity, FixedString64Bytes, TechEffect[])>();
                    completedResearch.Add((player, item.TechID, effects));
                }
            }
            else
            {
                Entity prefab = item.Offer.UnitPrefab;
                if (!em.Exists(prefab) || !em.HasComponent<UnitComponent>(prefab)) { ProductionJobs.RemoveFirst(em, entity, true); continue; }
                int cost = em.GetComponentData<UnitComponent>(prefab).PopulationCost;
                if (cost > 0 && (long)context.currentPopulation + cost > context.maxPopulation) continue; // Ready: do not restart work.
                Entity unit = ecb.Instantiate(prefab);
                ecb.SetComponent(unit, new EntityOwner { PlayerID = item.PlayerID });
                ecb.AddComponent(unit, new PopulationAccount { PlayerID = item.PlayerID, Used = math.max(0, cost) });
                if (cost > 0) PopulationSystem.Adjust(em, item.PlayerID, cost, 0);
                var local = em.GetComponentData<LocalTransform>(prefab);
                local.Position = transform.ValueRO.TransformPoint(production.ValueRO.SpawnOffset);
                ecb.SetComponent(unit, local);
                if (em.HasComponent<Selectable>(prefab))
                {
                    var selectable = em.GetComponentData<Selectable>(prefab); selectable.playerID = item.PlayerID; ecb.SetComponent(unit, selectable);
                }
                if (em.HasComponent<MoveOverride>(prefab))
                {
                    var move = em.GetComponentData<MoveOverride>(prefab);
                    move.targetPosition = transform.ValueRO.TransformPoint(production.ValueRO.RallyOffset); move.targetApplied = false;
                    ecb.SetComponent(unit, move); ecb.SetComponentEnabled<MoveOverride>(unit, true);
                }
            }
            ProductionJobs.RemoveFirst(em, entity, false);
        }
        ecb.Playback(em); ecb.Dispose();
        // Conduct may make structural changes. No query iteration, ECB playback or buffer references remain in use here.
        if (completedResearch == null) return;
        foreach (var completion in completedResearch)
        {
            var player = completion.Player;
            if (!em.Exists(player) || !em.HasBuffer<PlayerTechnology>(player)
                || TechUnlockHelper.IsTechCompleted(em, player, completion.TechID)) continue;
            try
            {
                foreach (var effect in completion.Effects) effect.Conduct(em, player);
                if (em.Exists(player) && em.HasBuffer<PlayerTechnology>(player)
                    && !TechUnlockHelper.IsTechCompleted(em, player, completion.TechID))
                    em.GetBuffer<PlayerTechnology>(player).Add(new PlayerTechnology { ID = completion.TechID });
            }
            catch (System.Exception exception)
            {
                // The queue item is consumed: do not automatically repeat effects that already ran.
                UnityEngine.Debug.LogError($"Tech '{completion.TechID}' failed while conducting effects. Earlier effects are not rolled back.");
                UnityEngine.Debug.LogException(exception);
            }
        }
    }

    private static bool TryGetResearchEffects(EntityManager em, ProductionQueueElement item, out TechEffect[] effects)
    {
        effects = null;
        if (!ProductionJobs.TryRegistry(em, out var registryEntity)) return false;
        var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
        var research = registry.GetBlobByID<ResearchBlob>(item.Offer.JobID, out var found);
        if (found != FunctionResult.Success || research.Value.Tech.ID != item.TechID) return false;
        ref var indices = ref research.Value.Tech.EffectIndices;
        if (indices.Length == 0) { effects = System.Array.Empty<TechEffect>(); return true; }
        if (!em.HasBuffer<RegistryTechEffectElement>(registryEntity)) return false;
        var catalog = em.GetBuffer<RegistryTechEffectElement>(registryEntity, true);
        effects = new TechEffect[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];
            if (index < 0 || index >= catalog.Length) return false;
            var effect = catalog[index].Effect.Value;
            if (effect == null) return false;
            effects[i] = effect;
        }
        return true;
    }
}

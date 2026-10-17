using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Call on the simulation thread after the fixed tick, then hand the frame to presentation.
public static class FogOfWarSnapshot
{
    // The optional cache parameter is retained for existing local callers. Terrain
    // is always full Explored now, regardless of receiver cache contents.
    public static bool Collect(EntityManager em, int playerId, FogOfWarFrame frame, FogTerrainMemory knownTerrain = null)
    {
        frame.Entities.Clear();
        frame.VisibleCells.Clear();
        frame.TerrainCells.Clear();
        frame.IsValid = false;
        frame.PlayerId = playerId;
        em.CompleteAllTrackedJobs();
        if (playerId < 0 || PlayerContextHelper.GetPlayerContextEntity(em, playerId, out _, out _) != FunctionResult.Success)
            return false;
        using var grids = em.CreateEntityQuery(typeof(GridComponent));
        using var fogs = em.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        using var units = em.CreateEntityQuery(typeof(UnitSpatialBucket));
        using var neutrals = em.CreateEntityQuery(typeof(NeutralSpatialBucket));
        if (grids.IsEmptyIgnoreFilter || fogs.IsEmptyIgnoreFilter || units.IsEmptyIgnoreFilter || neutrals.IsEmptyIgnoreFilter)
            return false;
        var grid = grids.GetSingleton<GridComponent>();
        if (!SpatialCellCoverage.IsGridValid(grid)) return false;
        var gridEntity = grids.GetSingletonEntity();
        if (em.HasComponent<MapBuildState>(gridEntity))
        {
            var phase = em.GetComponentData<MapBuildState>(gridEntity).Phase;
            if (phase == MapBuildPhase.Requested || phase == MapBuildPhase.Failed) return false;
        }
        var fog = fogs.GetSingleton<FogOfWarSpatialSystemData>();
        var unitMap = units.GetSingleton<UnitSpatialBucket>().Bucket;
        var neutralMap = neutrals.GetSingleton<NeutralSpatialBucket>().Bucket;
        if (!fog.Visible.IsCreated || !unitMap.IsCreated || !neutralMap.IsCreated) return false;

        frame.Width = grid.width; frame.Height = grid.height;
        frame.CellSize = grid.cellsize; frame.Origin = grid.origin;
        frame.TerrainBaseLevel = 0;
        // Explored includes history imported from teammates. It authorizes terrain, not current entities.
        if (fog.Explored.IsCreated && em.HasBuffer<GridTerrain>(gridEntity))
        {
            var terrain = em.GetBuffer<GridTerrain>(gridEntity, true);
            if (terrain.Length == (long)grid.width * grid.height)
            {
                int minimum = int.MaxValue;
                for (int i = 0; i < terrain.Length; i++) minimum = math.min(minimum, terrain[i].heightLevel);
                frame.TerrainBaseLevel = minimum;
                using var emitted = new NativeHashSet<int>(128, Allocator.Temp);
                foreach (var pair in fog.Explored)
                {
                    int cell = pair.Key;
                    if (pair.Value != playerId || cell < 0 || cell >= terrain.Length || !emitted.Add(cell)) continue;
                    frame.TerrainCells.Add(new FogTerrainCellData { CellIndex = cell, Terrain = terrain[cell] });
                }
            }
        }
        using var visibleCells = new NativeHashSet<int>(128, Allocator.Temp);
        using var candidates = new NativeHashSet<Entity>(128, Allocator.Temp);
        foreach (var pair in fog.Visible)
        {
            if (pair.Value != playerId || pair.Key < 0 || pair.Key >= (long)grid.width * grid.height
                || !visibleCells.Add(pair.Key)) continue;
            frame.VisibleCells.Add(pair.Key);
            AddCandidates(unitMap, pair.Key, candidates);
            AddCandidates(neutralMap, pair.Key, candidates);
        }

        // Projectiles neither grant vision nor occupy the unit/neutral spatial indices.
        using var projectiles = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<LocalTransform>() },
            Any = new[] { ComponentType.ReadOnly<Bullet>(), ComponentType.ReadOnly<ArtilleryBullet>() }
        });
        using var projectileEntities = projectiles.ToEntityArray(Allocator.Temp);
        foreach (var entity in projectileEntities)
        {
            var position = em.GetComponentData<LocalTransform>(entity).Position;
            if (!math.all(math.isfinite(position))) continue;
            var cell = GridHelper.WorldToGrid(position, grid);
            if (PlayerSpawnPlacement.InBounds(cell, grid) && visibleCells.Contains(GridHelper.GetNodeIndex(cell, grid)))
                candidates.Add(entity);
        }
        ProductionJobs.TryRegistry(em, out var registryEntity);
        foreach (var entity in candidates)
            if (TryRead(em, entity, grid, visibleCells, registryEntity, out var data)) frame.Entities.Add(data);
        frame.IsValid = true;
        return true;
    }

    private static void AddCandidates(NativeParallelMultiHashMap<int, Entity> map, int cell, NativeHashSet<Entity> candidates)
    {
        if (!map.TryGetFirstValue(cell, out var entity, out var iterator)) return;
        do { candidates.Add(entity); } while (map.TryGetNextValue(out entity, ref iterator));
    }

    private static bool TryRead(EntityManager em, Entity entity, in GridComponent grid,
        NativeHashSet<int> visibleCells, Entity registryEntity, out FogEntityRenderData data)
    {
        data = default;
        // Spatial entries can lag destruction/depletion until their next fixed update.
        if (!em.Exists(entity) || !em.HasComponent<LocalTransform>(entity) || em.HasComponent<Prefab>(entity)
            || em.HasComponent<ResourceNodePendingConfig>(entity)) return false;
        if (em.HasComponent<EntityHealth>(entity) && em.GetComponentData<EntityHealth>(entity).CurrentHP <= 0) return false;
        if (em.HasComponent<ResourceNodeData>(entity) && em.GetComponentData<ResourceNodeData>(entity).Amount <= 0) return false;
        data.Transform = em.GetComponentData<LocalTransform>(entity);
        if (!math.all(math.isfinite(data.Transform.Position))) return false;
        data.Entity = entity;
        data.OwnerId = em.HasComponent<EntityOwner>(entity) ? em.GetComponentData<EntityOwner>(entity).PlayerID : -1;
        data.PostTransform = em.HasComponent<PostTransformMatrix>(entity)
            ? em.GetComponentData<PostTransformMatrix>(entity).Value : float4x4.identity;
        bool projectile = em.HasComponent<Bullet>(entity) || em.HasComponent<ArtilleryBullet>(entity);
        data.IsStatic = !projectile && (em.HasComponent<BuildingComponent>(entity) || em.HasComponent<ResourceNodeData>(entity));
        data.IsVisible = true;
        data.FootprintMin = data.FootprintMax = GridHelper.WorldToGrid(data.Transform.Position, grid);
        if (em.HasComponent<BlockageData>(entity)
            && !PlayerSpawnPlacement.TryFootprint(grid, data.Transform.Position, em.GetComponentData<BlockageData>(entity).LocalRect,
                out data.FootprintMin, out data.FootprintMax)) return false;
        // Recheck current occupancy: an index from the last tick must not expose a unit that moved into fog.
        bool visible = false;
        for (int z = data.FootprintMin.y; z <= data.FootprintMax.y && !visible; z++)
        for (int x = data.FootprintMin.x; x <= data.FootprintMax.x; x++)
            if (x >= 0 && z >= 0 && x < grid.width && z < grid.height && visibleCells.Contains(z * grid.width + x))
            { visible = true; break; }
        if (!visible) return false;
        if (em.HasComponent<BuildingComponent>(entity))
        {
            var building = em.GetComponentData<BuildingComponent>(entity);
            data.Kind = FogRenderKind.Building;
            data.ConstructionPhase = ConstructionPhase.Completed;
            data.ConstructionProgress = 1;
            if (em.HasComponent<BuildingConstruction>(entity))
            {
                var construction = em.GetComponentData<BuildingConstruction>(entity);
                if (construction.Phase == ConstructionPhase.Destroyed) return false;
                data.ConstructionPhase = construction.Phase;
                data.ConstructionProgress = construction.Phase == ConstructionPhase.Completed ? 1
                    : building.WorkLoad > 0 ? math.saturate(construction.CompletedWork / building.WorkLoad) : 0;
            }
        }
        else if (em.HasComponent<UnitComponent>(entity))
        {
            data.Kind = FogRenderKind.Unit;
        }
        else
        {
            data.Kind = projectile ? FogRenderKind.Projectile
                : em.HasComponent<ResourceNodeData>(entity) ? FogRenderKind.Resource : FogRenderKind.Prop;
        }
        float? definedMax = null;
        BlobAssetReference<UnitBlob> unitDefinition = default;
        BlobAssetReference<BuildingBlob> buildingDefinition = default;
        if (registryEntity != Entity.Null)
        {
            var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
            if (em.HasComponent<BuildingComponent>(entity))
            {
                var id = em.GetComponentData<BuildingComponent>(entity).DefinitionID;
                buildingDefinition = registry.GetBlobByID<BuildingBlob>(id, out var result);
                if (result == FunctionResult.Success)
                { data.GameRegId = id; definedMax = buildingDefinition.Value.Base.MaxHP; }
            }
            else if (em.HasComponent<UnitComponent>(entity))
            {
                var id = em.GetComponentData<UnitComponent>(entity).DefinitionID;
                unitDefinition = registry.GetBlobByID<UnitBlob>(id, out var result);
                if (result == FunctionResult.Success)
                { data.GameRegId = id; definedMax = unitDefinition.Value.Base.MaxHP; }
            }
        }
        if (!data.GameRegId.HasValue) data.SourceEntity = entity;
        if (em.HasComponent<EntityHealth>(entity))
        {
            var health = em.GetComponentData<EntityHealth>(entity);
            data.Health = new FogHealthSnapshot { Current = health.CurrentHP,
                MaxOverride = definedMax.HasValue && definedMax.Value == health.MaxHP ? null : health.MaxHP };
        }
        if (em.HasComponent<ResourceNodeData>(entity))
        {
            var resource = em.GetComponentData<ResourceNodeData>(entity);
            data.Resource = new FogResourceSnapshot { Amount = resource.Amount };
        }
        // Only executing capabilities, never the list of available abilities.
        if (em.HasComponent<WorkerGatherData>(entity) && em.IsComponentEnabled<WorkerGatherData>(entity)
            && em.GetComponentData<WorkerGatherData>(entity).State != WorkerGatherState.Idle)
            data.ActiveAbility = new FogActiveAbilitySnapshot { Kind = FogAbilityKind.Gather,
                State = (byte)em.GetComponentData<WorkerGatherData>(entity).State };
        if (em.HasComponent<BuilderComponent>(entity) && em.IsComponentEnabled<BuilderComponent>(entity)
            && em.GetComponentData<BuilderComponent>(entity).State != BuilderState.Idle)
            data.ActiveAbility = new FogActiveAbilitySnapshot { Kind = FogAbilityKind.Build,
                State = (byte)em.GetComponentData<BuilderComponent>(entity).State };
        // ShootAttack being enabled only means the entity can attack. A live,
        // hostile target and no move override indicate an executing attack order.
        if (!data.ActiveAbility.HasValue && em.HasComponent<ShootAttack>(entity)
            && em.IsComponentEnabled<ShootAttack>(entity) && em.HasComponent<Target>(entity)
            && em.HasBuffer<UnitWeaponSlot>(entity) && registryEntity != Entity.Null
            && !(em.HasComponent<MoveOverride>(entity) && em.IsComponentEnabled<MoveOverride>(entity))
            && (data.Kind != FogRenderKind.Building || data.ConstructionPhase == ConstructionPhase.Completed))
        {
            var target = em.GetComponentData<Target>(entity).targetEntity;
            var attackId = em.GetComponentData<ShootAttack>(entity).IDs;
            em.GetBuffer<RegistryBlobElement>(registryEntity, true).GetBlobByID<AttackBlob>(attackId, out var attackResult);
            if (attackResult == FunctionResult.Success && em.Exists(target)
                && em.HasComponent<LocalTransform>(target) && em.HasComponent<EntityHealth>(target)
                && em.GetComponentData<EntityHealth>(target).CurrentHP > 0
                && em.HasComponent<EntityOwner>(entity))
            {
                bool allied = false;
                if (em.HasComponent<EntityOwner>(target))
                {
                    using var players = em.CreateEntityQuery(typeof(PlayerContext));
                    using var contexts = players.ToComponentDataArray<PlayerContext>(Allocator.Temp);
                    allied = PlayerContextHelper.AreAllies(contexts, data.OwnerId, em.GetComponentData<EntityOwner>(target).PlayerID);
                }
                if (!allied)
                    data.ActiveAbility = new FogActiveAbilitySnapshot { Kind = FogAbilityKind.Attack,
                        Id = attackId };
            }
        }
        if (data.ActiveAbility.HasValue && data.ActiveAbility.Value.Kind != FogAbilityKind.Attack)
        {
            var active = data.ActiveAbility.Value;
            long key = active.Kind == FogAbilityKind.Build ? RegistryTypeKey.For<BuildBlob>() : RegistryTypeKey.For<GatherBlob>();
            if (buildingDefinition.IsCreated) active.Id = FindAbility(ref buildingDefinition.Value.Base, key);
            else if (unitDefinition.IsCreated) active.Id = FindAbility(ref unitDefinition.Value.Base, key);
            data.ActiveAbility = active;
        }
        if (em.HasBuffer<ProductionQueueElement>(entity) && em.HasComponent<ProductionData>(entity)
            && (data.Kind != FogRenderKind.Building || data.ConstructionPhase == ConstructionPhase.Completed))
        {
            var queue = em.GetBuffer<ProductionQueueElement>(entity, true);
            if (queue.Length > 0 && queue[0].PlayerID == data.OwnerId)
            {
                var current = queue[0];
                data.ActiveJob = new FogActiveJobSnapshot { Id = current.Offer.JobID,
                    Kind = current.Offer.Kind, RemainingWork = current.RemainingWork };
            }
        }
        return true;
    }

    private static FixedString64Bytes FindAbility(ref BaseBlob definition, long key)
    {
        for (int i = 0; i < definition.Abilities.Length; i++)
            if (definition.Abilities[i].CatalogKey == key) return definition.Abilities[i].ID;
        return default;
    }
}

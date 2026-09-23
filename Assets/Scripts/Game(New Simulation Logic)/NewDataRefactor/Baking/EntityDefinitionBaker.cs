using System;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Shared instance state only. RegistryAuthoring owns all immutable definition blobs.
public abstract class EntityDefinitionBaker<T> : Baker<T> where T : Component
{
    protected void BakeBase(Entity entity, BasedSO definition,float initialHealth)
    {
        DependsOn(definition);
        if (definition == null)
            throw new InvalidOperationException("The entity definition requires a BasedSO.");
        if (string.IsNullOrWhiteSpace(definition.ID) ||
            Encoding.UTF8.GetByteCount(definition.ID) > new FixedString64Bytes().Capacity)
            throw new InvalidOperationException("BasedSO.ID must be non-empty and fit in FixedString64Bytes.");
        if (!math.isfinite(definition.MaxHP) || definition.MaxHP <= 0f)
            throw new InvalidOperationException("BasedSO.MaxHP must be finite and positive.");
        if (!math.isfinite(initialHealth) ||
            (initialHealth != -1f && (initialHealth < 0f || initialHealth > definition.MaxHP)))
            throw new InvalidOperationException("Initial HP must be -1 (full health) or between 0 and MaxHP.");

        AddComponent(entity, new EntityOwner { PlayerID = -1});
        AddComponent(entity, new EntityHealth
        {
            CurrentHP = initialHealth == -1f ? definition.MaxHP : initialHealth,
            MaxHP = definition.MaxHP,
            Changed = true
        });
        AddComponent(entity, new EntityWork { Rate = definition.WorkRate, JobCapacity = definition.JobCapacity });
        if (GetComponent<TargetCacheAuthoring>() == null) AddComponent(entity, new TargetCache());
        foreach (var ability in definition.Abilities)
        {
            DependsOn(ability);
            switch (ability)
            {
                case GatherAbilityDefinition gather:
                    AddComponent<WorkerTag>(entity);
                    AddComponent(entity, new WorkerGatherData
                    {
                        Capacity = gather.Capacity, GatherTime = gather.GatherTime,
                        GatherRate = gather.GatherRate, WorkRate = definition.WorkRate,
                        StopDistanceSq = gather.StopDistance * gather.StopDistance
                    });
                    SetComponentEnabled<WorkerGatherData>(entity, false);
                    break;
                case BuildAbilityDefinition build:
                    AddComponent(entity, new BuilderComponent
                    {
                        BuildWorkLoadPerSecond = definition.WorkRate, BuildRange = build.Range
                    });
                    SetComponentEnabled<BuilderComponent>(entity, false);
                    AddBuffer<BuilderQueueElement>(entity);
                    var offers = AddBuffer<BuildOffer>(entity);
                    foreach (var building in build.Buildings)
                    {
                        DependsOn(building); DependsOn(building.basedSO);
                        offers.Add(new BuildOffer { DefinitionID = building.basedSO.ID });
                    }
                    break;
                case StorageAbilityDefinition storage:
                    var resources = AddBuffer<StorageResource>(entity);
                    foreach (var resource in storage.AcceptedResourceTypes)
                        resources.Add(new StorageResource { Type = resource });
                    break;
                case AttackAbilityDefinition attack:
                    AddComponent(entity, new ShootAttack { IDs = attack.Id });
                    SetComponentEnabled<ShootAttack>(entity, true);
                    var slots = AddBuffer<UnitWeaponSlot>(entity);
                    foreach (var weapon in attack.Weapons) slots.Add(new UnitWeaponSlot());
                    if (GetComponent<TargetAuthoring>() == null) AddComponent(entity, new Target());
                    AddComponent(entity, new FindTarget { range = attack.Range, timerMax = 0.25f });
                    break;
                default: throw new InvalidOperationException($"Unsupported ability {ability}.");
            }
        }
        if (definition.Jobs.Count > 0)
        {
            var bindings = GetComponent<ProductionAuthoring>();
            var root = GetComponent<Transform>();
            var spawn = bindings != null && bindings.SpawnOffset != null ? GetComponent<Transform>(bindings.SpawnOffset.gameObject) : null;
            var rally = bindings != null && bindings.RallyOffset != null ? GetComponent<Transform>(bindings.RallyOffset.gameObject) : null;
            AddComponent(entity, new ProductionData
            {
                SpawnOffset = spawn != null ? (float3)root.InverseTransformPoint(spawn.position) : float3.zero,
                RallyOffset = rally != null ? (float3)root.InverseTransformPoint(rally.position) : new float3(3, 0, 3)
            });
            var jobs = AddBuffer<ProductionElement>(entity);
            foreach (var job in definition.Jobs)
            {
                DependsOn(job);
                if (job is Train train)
                {
                    DependsOn(train.OutputUnit); DependsOn(train.OutputUnit.basedSO);
                    jobs.Add(new ProductionElement { JobID = train.Id, Kind = ProductionKind.Train,
                        UnitPrefab = GetEntity(train.OutputUnit.basedSO.Prefab, TransformUsageFlags.Dynamic) });
                }
                else if (job is Research research)
                {
                    DependsOn(research.TechDefinition);
                    jobs.Add(new ProductionElement { JobID = research.Id, Kind = ProductionKind.Research });
                }
                else throw new InvalidOperationException($"Unsupported job {job}.");
            }
            AddBuffer<ProductionQueueElement>(entity);
            AddBuffer<ProductionPayment>(entity);
        }
    }
}

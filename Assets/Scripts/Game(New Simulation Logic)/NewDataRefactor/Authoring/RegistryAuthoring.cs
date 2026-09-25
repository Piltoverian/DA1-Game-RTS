using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.STP;

// Preserve the existing serialized mode values and membership behavior.
enum BakeMode { BakeAll, CivOnly, ParameterBake }

public class RegistryAuthoring : MonoBehaviour
{
    [SerializeField] private GameDataRegistry gameDataRegistry;
    [SerializeField] private BakeMode bakeMode;

    private static RegistryAuthoring __instance;


    private void Awake()
    {
        var regs= FindObjectsByType<RegistryAuthoring>(FindObjectsSortMode.None);
        if(regs.Count()!=1)
        {
            Debug.LogError($"There should be exactly one RegistryAuthoring in the scene, but found {regs.Count()}.");
        }
        __instance = this;
    }

    public Sprite GetIcon<T>(FixedString64Bytes ID) where T: ScriptableObject
    {
        Sprite result;
        switch(typeof(T))
        {
            case Type t when t == typeof(UnitSO):
                result = gameDataRegistry.Units.FirstOrDefault(u => u.basedSO.ID == ID)?.basedSO.Icon;
                break;
            case Type t when t == typeof(BuildingSO):
                result = gameDataRegistry.Buildings.FirstOrDefault(b => b.basedSO.ID == ID)?.basedSO.Icon;
                break;
            case Type t when t == typeof(Job):
                result = gameDataRegistry.Jobs.FirstOrDefault(j => j.Id == ID)?.Icon;
                break;
            case Type t when t == typeof(CivDef):
                result = gameDataRegistry.Civs.FirstOrDefault(c => c.Id == ID)?.Icon;
                break;
            default:
                throw new NotSupportedException($"Unknown type: {typeof(T)}");
        }
        return result;
    }   

    public string GetName<T>(FixedString64Bytes ID) where T: ScriptableObject
    {
        string result;
        switch (typeof(T))
        {
            case Type t when t == typeof(UnitSO):
                result = gameDataRegistry.Units.FirstOrDefault(u => u.basedSO.ID == ID)?.basedSO.Name;
                break;
            case Type t when t == typeof(BuildingSO):
                result = gameDataRegistry.Buildings.FirstOrDefault(b => b.basedSO.ID == ID)?.basedSO.Name;
                break;
            case Type t when t == typeof(Job):
                result = gameDataRegistry.Jobs.FirstOrDefault(j => j.Id == ID)?.name;
                break;
            case Type t when t == typeof(CivDef):
                result = gameDataRegistry.Civs.FirstOrDefault(c => c.Id == ID)?.name;
                break;
            default:
                throw new NotSupportedException($"Unknown type: {typeof(T)}");
        }
        return result;
    }

    public Sprite GetIcon(FixedString64Bytes ID)
    {
        if (gameDataRegistry == null) return null;

        var unit = gameDataRegistry.Units?.FirstOrDefault(u => u != null && u.basedSO != null && u.basedSO.ID == ID);
        if (unit != null) return unit.basedSO.Icon;

        var bld = gameDataRegistry.Buildings?.FirstOrDefault(b => b != null && b.basedSO != null && b.basedSO.ID == ID);
        if (bld != null) return bld.basedSO.Icon;

        var job = gameDataRegistry.Jobs?.FirstOrDefault(j => j != null && j.Id == ID);
        if (job != null) return job.Icon;

        var civ = gameDataRegistry.Civs?.FirstOrDefault(c => c != null && c.Id == ID);
        if (civ != null) return civ.Icon;

        return null;
    }

    public string GetName(FixedString64Bytes ID)
    {
        if (gameDataRegistry == null) return ID.ToString();

        var unit = gameDataRegistry.Units?.FirstOrDefault(u => u != null && u.basedSO != null && u.basedSO.ID == ID);
        if (unit != null) return string.IsNullOrEmpty(unit.basedSO.Name) ? unit.name : unit.basedSO.Name;

        var bld = gameDataRegistry.Buildings?.FirstOrDefault(b => b != null && b.basedSO != null && b.basedSO.ID == ID);
        if (bld != null) return string.IsNullOrEmpty(bld.basedSO.Name) ? bld.name : bld.basedSO.Name;

        var job = gameDataRegistry.Jobs?.FirstOrDefault(j => j != null && j.Id == ID);
        if (job != null) return job.name;

        var civ = gameDataRegistry.Civs?.FirstOrDefault(c => c != null && c.Id == ID);
        if (civ != null) return civ.name;

        return ID.ToString();
    }

    public BuildingSO GetBuildingSO(FixedString64Bytes ID)
    {
        if (gameDataRegistry == null || gameDataRegistry.Buildings == null) return null;
        return gameDataRegistry.Buildings.FirstOrDefault(b => b != null && b.basedSO != null && b.basedSO.ID == ID);
    }

    public GameObject GetBuildingPrefab(FixedString64Bytes ID)
    {
        return gameDataRegistry.Buildings.FirstOrDefault(b => b.basedSO.ID == ID)?.basedSO.Prefab;
    }

    public static RegistryAuthoring Instance
    {
        get
        {
            if (__instance==null)
            {
                throw new InvalidOperationException("RegistryAuthoring instance is not set. Ensure that there is exactly one RegistryAuthoring in the scene.");
            }
            return __instance;
        }
    }



    class Baker : Baker<RegistryAuthoring>
    {
        private readonly Dictionary<GameObject, int> prefabs = new();
        private readonly HashSet<TechTreeDef> trees = new();

        public override void Bake(RegistryAuthoring authoring)
        {
            prefabs.Clear();
            trees.Clear();
            try
            {
                DependsOn(authoring.gameDataRegistry);
                Entity et = GetEntity(TransformUsageFlags.None);
                AddComponent<GameDataRegistryComponent>(et);
                AddBuffer<RegistryBlobElement>(et);
                AddBuffer<RegistryPrefabElement>(et);
                if (authoring.bakeMode == BakeMode.BakeAll || authoring.bakeMode == BakeMode.ParameterBake)
                {
                    foreach (var unit in authoring.gameDataRegistry.Units) BakeUnits(unit, et);
                    foreach (var job in authoring.gameDataRegistry.Jobs) BakeJobs(job, et);
                    foreach (var ability in authoring.gameDataRegistry.Abilities) BakeAbilities(ability, et);
                    foreach (var building in authoring.gameDataRegistry.Buildings) BakeBuildings(building, et);
                }
                if (authoring.bakeMode == BakeMode.BakeAll || authoring.bakeMode == BakeMode.CivOnly)
                    foreach (var civ in authoring.gameDataRegistry.Civs) BakeCivs(civ, et);
            }
            finally { prefabs.Clear(); trees.Clear(); }
        }

        private void AddBlobToReg<T>(Entity et, ref BlobBuilder builder) where T : unmanaged, IGameBlobAsset
        {
            var blob = builder.CreateBlobAssetReference<T>(Allocator.Persistent);
            AddBlobAsset(ref blob, out _);
            AppendToBuffer(et, new RegistryBlobElement
            {
                TypeKey = RegistryTypeKey.For<T>(), Blob = UnsafeUntypedBlobAssetReference.Create(blob)
            });
        }

        private static void Copy<T>(ref BlobBuilder builder, ref BlobArray<T> destination, List<T> source)
            where T : unmanaged
        {
            var array = builder.Allocate(ref destination, source.Count);
            for (int i = 0; i < source.Count; i++) array[i] = source[i];
        }

        private int PrefabIndex(GameObject prefab, Entity et)
        {
            if (prefab == null) return -1;
            if (prefabs.TryGetValue(prefab, out int index)) return index;
            index = prefabs.Count;
            prefabs.Add(prefab, index);
            AppendToBuffer(et, new RegistryPrefabElement { Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
            return index;
        }

        private void BakeBase(BasedSO authoring, Entity et, ref BlobBuilder builder, ref BaseBlob blob)
        {
            DependsOn(authoring);
            blob.ID = authoring.ID;
            blob.MaxHP = authoring.MaxHP;
            blob.WorkRate = authoring.WorkRate;
            blob.JobCapacity = authoring.JobCapacity;
            blob.PrefabIndex = PrefabIndex(authoring.Prefab, et);
            var abilities = builder.Allocate(ref blob.Abilities, authoring.Abilities.Count);
            for (int i = 0; i < authoring.Abilities.Count; i++)
            {
                var source = authoring.Abilities[i];
                DependsOn(source);
                long key = source switch
                {
                    AttackAbilityDefinition _ => RegistryTypeKey.For<AttackBlob>(),
                    GatherAbilityDefinition _ => RegistryTypeKey.For<GatherBlob>(),
                    StorageAbilityDefinition _ => RegistryTypeKey.For<StorageBlob>(),
                    BuildAbilityDefinition _ => RegistryTypeKey.For<BuildBlob>(),
                    _ => throw new NotSupportedException($"Unknown ability type: {source}")
                };
                abilities[i] = new GameBlobReference { CatalogKey = key, ID = source.Id };
            }
            var jobs = builder.Allocate(ref blob.Jobs, authoring.Jobs.Count);
            for (int i = 0; i < authoring.Jobs.Count; i++)
            {
                var source = authoring.Jobs[i];
                DependsOn(source);
                long key = source switch
                {
                    Research _ => RegistryTypeKey.For<ResearchBlob>(),
                    Train _ => RegistryTypeKey.For<TrainBlob>(),
                    _ => throw new NotSupportedException($"Unknown job type: {source}")
                };
                jobs[i] = new GameBlobReference { CatalogKey = key, ID = source.Id };
            }
        }

        private void BakeUnits(UnitSO authoring, Entity et)
        {
            DependsOn(authoring);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var blob = ref builder.ConstructRoot<UnitBlob>();
                BakeBase(authoring.basedSO, et, ref builder, ref blob.Base);
                blob.PopulationCost = authoring.PopulationCost;
                Copy(ref builder, ref blob.ResourceCosts, authoring.ResourceCosts);
                Copy(ref builder, ref blob.UnitTypes, authoring.UnitTypes);
                AddBlobToReg<UnitBlob>(et, ref builder);
            }
            finally { builder.Dispose(); }
        }

        private void BakeBuildings(BuildingSO authoring, Entity et)
        {
            DependsOn(authoring);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var blob = ref builder.ConstructRoot<BuildingBlob>();
                BakeBase(authoring.basedSO, et, ref builder, ref blob.Base);
                blob.PopulationCapacity = authoring.PopulationCapacity;
                blob.WorkLoad = authoring.WorkLoad;
                Copy(ref builder, ref blob.Tags, authoring.Tags);
                Copy(ref builder, ref blob.Cost, authoring.Cost);
                AddBlobToReg<BuildingBlob>(et, ref builder);
            }
            finally { builder.Dispose(); }
        }

        private void BakeJobs(Job authoring, Entity et)
        {
            DependsOn(authoring);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                switch (authoring)
                {
                    case Research research:
                    {
                        DependsOn(research.TechDefinition);
                        ref var blob = ref builder.ConstructRoot<ResearchBlob>();
                        blob.Job.ID = research.Id;
                        blob.Job.WorkLoad = research.WorkLoad;
                        blob.Tech.ID = research.TechDefinition.IDs;
                        Copy(ref builder, ref blob.Tech.Cost, research.TechDefinition.Cost);
                        AddBlobToReg<ResearchBlob>(et, ref builder);
                        break;
                    }
                    case Train train:
                    {
                        DependsOn(train.OutputUnit);
                        DependsOn(train.OutputUnit.basedSO);
                        ref var blob = ref builder.ConstructRoot<TrainBlob>();
                        blob.Job.ID = train.Id;
                        blob.Job.WorkLoad = train.WorkLoad;
                        blob.OutputUnitID = train.OutputUnit.basedSO.ID;
                        AddBlobToReg<TrainBlob>(et, ref builder);
                        break;
                    }
                    default: throw new NotSupportedException($"Unknown job type: {authoring}");
                }
            }
            finally { builder.Dispose(); }
        }

        private void BakeAbilities(AbilityDefinition authoring, Entity et)
        {
            DependsOn(authoring);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                switch (authoring)
                {
                    case AttackAbilityDefinition attack:
                    {
                        ref var blob = ref builder.ConstructRoot<AttackBlob>();
                        blob.ID = attack.Id;
                        blob.Range = attack.Range;
                        blob.RotationSpeed = attack.RotationSpeed;
                        var weapons = builder.Allocate(ref blob.Weapons, attack.Weapons.Count);
                        for (int i = 0; i < attack.Weapons.Count; i++)
                        {
                            var weapon = attack.Weapons[i];
                            weapons[i] = new WeaponDefinitionBlob
                            {
                                Damage = weapon.Damage, Cooldown = weapon.Cooldown, SpawnOffset = weapon.SpawnOffset,
                                ProjectileSpeed = weapon.ProjectileSpeed,
                                ProjectilePrefabIndex = PrefabIndex(weapon.ProjectilePrefab, et),
                            };
                        }
                        AddBlobToReg<AttackBlob>(et, ref builder);
                        break;
                    }
                    case GatherAbilityDefinition gather:
                    {
                        ref var blob = ref builder.ConstructRoot<GatherBlob>();
                        blob.ID = gather.Id;
                        blob.Capacity = gather.Capacity;
                        blob.GatherRate = gather.GatherRate;
                        blob.GatherTime = gather.GatherTime;
                        blob.StopDistance = gather.StopDistance;
                        AddBlobToReg<GatherBlob>(et, ref builder);
                        break;
                    }
                    case StorageAbilityDefinition storage:
                    {
                        ref var blob = ref builder.ConstructRoot<StorageBlob>();
                        blob.ID = storage.Id;
                        Copy(ref builder, ref blob.Resources, storage.AcceptedResourceTypes);
                        AddBlobToReg<StorageBlob>(et, ref builder);
                        break;
                    }
                    case BuildAbilityDefinition build:
                    {
                        ref var blob = ref builder.ConstructRoot<BuildBlob>();
                        blob.ID = build.Id;
                        blob.Range = build.Range;
                        var buildings = builder.Allocate(ref blob.BuildingIDsInReg, build.Buildings.Count);
                        for (int i = 0; i < build.Buildings.Count; i++)
                        {
                            DependsOn(build.Buildings[i]);
                            DependsOn(build.Buildings[i].basedSO);
                            buildings[i] = build.Buildings[i].basedSO.ID;
                        }
                        AddBlobToReg<BuildBlob>(et, ref builder);
                        break;
                    }
                    default: throw new NotSupportedException($"Unknown ability type: {authoring}");
                }
            }
            finally { builder.Dispose(); }
        }

        private void BakeCivs(CivDef authoring, Entity et)
        {
            DependsOn(authoring);
            DependsOn(authoring.TechTree);
            if (trees.Add(authoring.TechTree)) BakeTechTree(authoring.TechTree, et);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var blob = ref builder.ConstructRoot<CivBlob>();
                blob.ID = authoring.Id;
                blob.TechTreeID = authoring.TechTree.Id;
                var unlocks = builder.Allocate(ref blob.UnitUnlocks, authoring.UnitUnlocks.Count);
                for (int i = 0; i < authoring.UnitUnlocks.Count; i++)
                {
                    var entry = authoring.UnitUnlocks[i];
                    DependsOn(entry.unitDefinition);
                    DependsOn(entry.unitDefinition.basedSO);
                    unlocks[i].UnitID = entry.unitDefinition.basedSO.ID;
                    BakeTechIds(ref builder, ref unlocks[i].Prerequisites, entry.Prerequisites);
                }
                AddBlobToReg<CivBlob>(et, ref builder);
            }
            finally { builder.Dispose(); }
        }

        private void BakeTechTree(TechTreeDef authoring, Entity et)
        {
            DependsOn(authoring);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var blob = ref builder.ConstructRoot<TechTreeBlob>();
                blob.ID = authoring.Id;
                var nodes = builder.Allocate(ref blob.Nodes, authoring.Nodes.Count);
                for (int i = 0; i < authoring.Nodes.Count; i++)
                {
                    var node = authoring.Nodes[i];
                    DependsOn(node.techDefinition);
                    nodes[i].TechID = node.techDefinition.IDs;
                    BakeTechIds(ref builder, ref nodes[i].Prerequisites, node.Prerequisites);
                }
                AddBlobToReg<TechTreeBlob>(et, ref builder);
            }
            finally { builder.Dispose(); }
        }

        private void BakeTechIds(ref BlobBuilder builder, ref BlobArray<FixedString64Bytes> destination,
            List<TechDefinition> source)
        {
            var ids = builder.Allocate(ref destination, source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                DependsOn(source[i]);
                ids[i] = source[i].IDs;
            }
        }
    }
}


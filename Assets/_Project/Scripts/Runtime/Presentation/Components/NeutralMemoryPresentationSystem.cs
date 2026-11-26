using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

// Graphics only: a remembered proxy has no simulation entity, collider, ownership or AI.
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(TerrainNetworkReceiveSystem))]
[UpdateBefore(typeof(EntitiesGraphicsSystem))]
public partial class NeutralMemoryPresentationSystem : SystemBase
{
    sealed class Part
    {
        public Mesh Mesh;
        public Material Material;
        public Matrix4x4 Relative;
        public int Submesh;
    }
    sealed class Visual
    {
        public int Template;
        public readonly List<Part> Parts = new();
        public void Clear() { foreach (var part in Parts) Release(part.Material); Parts.Clear(); }
    }
    readonly Dictionary<ulong, Visual> visuals = new();
    readonly HashSet<ulong> live = new();
    readonly List<ulong> removed = new();
    NeutralMemoryHeaderRpc scope;

    protected override void OnUpdate()
    {
        if (!TryMemory(out var memory, out var grid)) { Clear(); return; }
        var header = memory.Header;
        if (scope.MatchSessionId != header.MatchSessionId || scope.JoinGeneration != header.JoinGeneration
            || scope.Epoch != header.Epoch || scope.StreamGeneration != header.StreamGeneration)
        { Clear(); scope = header; }
        CaptureLive(memory, grid);
        CollectLive(); removed.Clear();
        foreach (var pair in visuals)
            if (!memory.Records.TryGetValue(pair.Key, out var record) || record.Removed) removed.Add(pair.Key);
        foreach (ulong id in removed) { visuals[id].Clear(); visuals.Remove(id); }
        foreach (var record in memory.Records.Values)
        {
            // The same stable ID suppresses the old proxy even when a neutral has moved or changed owner.
            if (record.Removed || live.Contains(record.Id)) continue;
            if (!visuals.TryGetValue(record.Id, out var visual) || visual.Template != record.TemplateGhostType)
            {
                visual?.Clear();
                if (!TryCapture(record.TemplateGhostType, out visual)) { visuals.Remove(record.Id); continue; }
                visuals[record.Id] = visual;
            }
            var data = record.Data;
            Matrix4x4 root = (Matrix4x4)float4x4.TRS(data.Transform.Position, data.Transform.Rotation, data.Transform.Scale)
                * (Matrix4x4)data.PostTransform;
            foreach (var part in visual.Parts)
            {
                if (!part.Mesh || !part.Material) continue;
                if (data.Kind == FogRenderKind.Building && part.Material.HasProperty("_RevealHeight"))
                    part.Material.SetFloat("_RevealHeight", data.Transform.Position.y + math.lerp(-10, 10, data.ConstructionProgress));
                Graphics.DrawMesh(part.Mesh, root * part.Relative, part.Material, 0, null, part.Submesh, null, ShadowCastingMode.On, true);
            }
        }
    }

    public static bool TryPick(World world, Ray ray, FogRenderKind kind, out ulong id)
    {
        id = 0;
        var presentation = world?.GetExistingSystemManaged<NeutralMemoryPresentationSystem>();
        return presentation != null && presentation.Pick(ray, kind, out id);
    }

    bool Pick(Ray ray, FogRenderKind kind, out ulong id)
    {
        id = 0;
        if (!TryMemory(out var memory, out var grid)) return false;
        CollectLive();
        float nearest = float.MaxValue;
        foreach (var record in memory.Records.Values)
        {
            if (record.Removed || live.Contains(record.Id)) continue;
            var data = record.Data;
            float2 min = grid.origin.xz + (float2)data.FootprintMin * grid.cellsize;
            float2 max = grid.origin.xz + ((float2)data.FootprintMax + 1) * grid.cellsize;
            var bounds = new Bounds(new Vector3((min.x + max.x) * .5f, data.Transform.Position.y + 1, (min.y + max.y) * .5f),
                new Vector3(max.x - min.x, 2, max.y - min.y));
            if (!bounds.IntersectRay(ray, out float distance) || distance >= nearest) continue;
            nearest = distance; id = data.Kind == kind ? record.Id : 0;
        }
        return id != 0;
    }

    bool TryMemory(out NeutralMemoryReplica memory, out GridComponent grid)
    {
        memory = null; grid = default;
        if (!PlayerNetwork.TryLocal(World, out _, out var player)) return false;
        var receiver = World.GetExistingSystemManaged<NeutralMemoryClientSystem>();
        var terrain = World.GetExistingSystemManaged<TerrainNetworkReceiveSystem>();
        if (receiver == null || !NeutralMemoryClientSystem.IsReady(World, player) || terrain == null || !terrain.View.Ready
            || terrain.Header.Session != player.MatchSessionId || terrain.Header.JoinGeneration != player.JoinGeneration
            || terrain.Header.Epoch != player.Epoch) return false;
        memory = receiver.Memory; grid = terrain.View.Grid; return true;
    }

    void CollectLive()
    {
        live.Clear();
        using var ghosts = EntityManager.CreateEntityQuery(typeof(GhostInstance));
        using var data = ghosts.ToComponentDataArray<GhostInstance>(Allocator.Temp);
        foreach (var ghost in data)
        {
            ulong id = NeutralMemoryIdentity.FromGhost(ghost);
            if (id != 0) live.Add(id);
        }
    }

    void CaptureLive(NeutralMemoryReplica memory, in GridComponent grid)
    {
        var terrain = World.GetExistingSystemManaged<TerrainNetworkReceiveSystem>().View;
        ProductionJobs.TryRegistry(EntityManager, out var registry);
        using var visible = new NativeHashSet<int>(32, Allocator.Temp);
        using var roots = EntityManager.CreateEntityQuery(typeof(GhostInstance), typeof(LocalTransform), typeof(SnapshotData));
        using var entities = roots.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var ghost = EntityManager.GetComponentData<GhostInstance>(entity);
            ulong id = NeutralMemoryIdentity.FromGhost(ghost);
            if (id == 0 || (!NeutralMemoryIdentity.IsNeutralGameplay(EntityManager, entity) && !memory.Records.ContainsKey(id))) continue;
            var tick = EntityManager.GetComponentData<SnapshotData>(entity).AppliedTick;
            if (!tick.IsValid) continue;
            float3 position = EntityManager.GetComponentData<LocalTransform>(entity).Position;
            int2 min = GridHelper.WorldToGrid(position, grid), max = min;
            if (EntityManager.HasComponent<BlockageData>(entity) && !PlayerSpawnPlacement.TryFootprint(grid, position,
                EntityManager.GetComponentData<BlockageData>(entity).LocalRect, out min, out max)) continue;
            visible.Clear();
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
                if (PlayerSpawnPlacement.InBounds(new int2(x, y), grid) && terrain.IsCellVisible(y * grid.width + x)) visible.Add(y * grid.width + x);
            if (FogOfWarSnapshot.TryReadVisible(EntityManager, entity, grid, visible, registry, out var data))
                memory.ObserveLive(new NeutralMemoryRecord { Id = id, ObservedTick = tick, TemplateGhostType = ghost.ghostType, Data = data,
                    ResourceType = EntityManager.HasComponent<ResourceNodeData>(entity) ? EntityManager.GetComponentData<ResourceNodeData>(entity).Type : default });
        }
    }

    bool TryCapture(int type, out Visual visual)
    {
        visual = null;
        using var collections = EntityManager.CreateEntityQuery(typeof(GhostCollectionPrefab));
        if (collections.CalculateEntityCount() != 1) return false;
        var prefabs = EntityManager.GetBuffer<GhostCollectionPrefab>(collections.GetSingletonEntity(), true);
        if (type < 0 || type >= prefabs.Length) return false;
        Entity prefab = prefabs[type].GhostPrefab;
        if (!EntityManager.Exists(prefab)) return false;
        visual = new Visual { Template = type };
        // Prefab geometry outlives relevance despawns. Only its mesh/materials are read.
        Matrix4x4 root = EntityManager.HasComponent<LocalToWorld>(prefab)
            ? (Matrix4x4)EntityManager.GetComponentData<LocalToWorld>(prefab).Value : Matrix4x4.identity;
        Capture(prefab, root.inverse, visual);
        if (EntityManager.HasBuffer<LinkedEntityGroup>(prefab))
            foreach (var child in EntityManager.GetBuffer<LinkedEntityGroup>(prefab, true))
                if (child.Value != prefab) Capture(child.Value, root.inverse, visual);
        if (visual.Parts.Count > 0) return true;
        visual.Clear(); visual = null; return false;
    }

    void Capture(Entity entity, Matrix4x4 inverse, Visual visual)
    {
        if (!EntityManager.HasComponent<MaterialMeshInfo>(entity) || !EntityManager.HasComponent<LocalToWorld>(entity)) return;
        var info = EntityManager.GetComponentData<MaterialMeshInfo>(entity);
        Matrix4x4 relative = inverse * (Matrix4x4)EntityManager.GetComponentData<LocalToWorld>(entity).Value;
        if (EntityManager.HasComponent<RenderMeshArray>(entity))
        {
            var array = EntityManager.GetSharedComponentManaged<RenderMeshArray>(entity);
            if (info.HasMaterialMeshIndexRange)
            {
                var range = info.MaterialMeshIndexRange;
                for (int i = range.start; i < range.end; i++)
                {
                    var item = array.MaterialMeshIndices[i];
                    Add(visual, array.MeshReferences[item.MeshIndex].Value, array.MaterialReferences[item.MaterialIndex].Value, relative, item.SubMeshIndex);
                }
            }
            else if (info.Material < 0 && info.Mesh < 0) Add(visual, array.GetMesh(info), array.GetMaterial(info), relative, info.SubMesh);
            else CaptureRuntime(visual, info, relative);
        }
        else CaptureRuntime(visual, info, relative);
    }

    void CaptureRuntime(Visual visual, MaterialMeshInfo info, Matrix4x4 relative)
    {
        var graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        if (graphics != null && info.Material >= 0 && info.Mesh >= 0)
            Add(visual, graphics.GetMesh(info.MeshID), graphics.GetMaterial(info.MaterialID), relative, info.SubMesh);
    }

    static void Add(Visual visual, Mesh mesh, Material material, Matrix4x4 relative, int submesh)
    {
        if (mesh && material) visual.Parts.Add(new Part { Mesh = mesh, Material = new Material(material) { name = "FOW neutral memory" }, Relative = relative, Submesh = submesh });
    }
    static void Release(Object value) { if (!value) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    void Clear() { foreach (var visual in visuals.Values) visual.Clear(); visuals.Clear(); live.Clear(); scope = default; }
    protected override void OnDestroy() { Clear(); }
}

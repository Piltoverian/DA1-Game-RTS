using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

// Local adapter only. It owns graphics memory, never clones simulation components.
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(MapTerrainPresentationSystem))]
[UpdateBefore(typeof(EntitiesGraphicsSystem))]
public partial class LocalFogPresentationSystem : SystemBase
{
    public FogOfWarFrame Frame { get; private set; }
    public FogOfWarRenderView View { get; } = new();
    public int ScopeVersion { get; private set; }
    public Texture2D Mask { get; private set; }
    readonly Dictionary<Entity, Entity> owners = new();
    readonly HashSet<Entity> hidden = new();
    readonly Dictionary<Entity, bool> hiddenSprites = new();
    readonly Dictionary<Entity, MemoryVisual> memories = new();
    readonly List<Entity> removed = new();
    Color32[] maskPixels;
    int viewer = -1;
    Entity gridEntity;
    Camera fogCamera;
    CameraClearFlags cameraFlags;
    Color cameraBackground;

    sealed class Part
    {
        public Mesh Mesh; public Material Material; public Matrix4x4 Matrix; public int Submesh;
    }
    sealed class MemoryVisual
    {
        public readonly List<Part> Parts = new();
        public int Count;
        public void Clear() { foreach (var part in Parts) Release(part.Material); Parts.Clear(); }
        public void Capture(Mesh mesh, Material source, Matrix4x4 matrix, int submesh, float? reveal)
        {
            if (!mesh || !source) return;
            if (Count == Parts.Count) Parts.Add(new Part { Material = new Material(source) { name = "FOW last seen" } });
            var part = Parts[Count++];
            if (part.Material.shader != source.shader) part.Material.shader = source.shader;
            part.Material.CopyPropertiesFromMaterial(source);
            if (reveal.HasValue) part.Material.SetFloat("_RevealHeight", reveal.Value);
            part.Mesh = mesh; part.Matrix = matrix; part.Submesh = submesh;
        }
        public void Draw() { for (int i = 0; i < Count; i++) { var p = Parts[i]; if (p.Mesh && p.Material) Graphics.DrawMesh(p.Mesh, p.Matrix, p.Material, 0, null, p.Submesh, null, ShadowCastingMode.On, true); } }
    }
    static void Release(Object value) { if (!value) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    protected override void OnCreate() { Frame = new FogOfWarFrame(); }

    protected override void OnUpdate()
    {
        EntityManager.CompleteAllTrackedJobs();
        int player = -1;
        using (var local = EntityManager.CreateEntityQuery(typeof(LocalTestPlayer)))
            if (local.CalculateEntityCount() == 1) player = local.GetSingleton<LocalTestPlayer>().PlayerId;
        if (player < 0 && GameManager.Instance != null) SelectHelper.TryGetLocalPlayerId(out player);
        using var grids = EntityManager.CreateEntityQuery(typeof(GridComponent));
        var currentGrid = grids.CalculateEntityCount() == 1 ? grids.GetSingletonEntity() : Entity.Null;
        if (viewer != player || gridEntity != currentGrid)
        { ResetMemory(); viewer = player; gridEntity = currentGrid; ScopeVersion++; }
        FogOfWarSnapshot.Collect(EntityManager, player, Frame);
        View.Apply(Frame);
        UpdateMask();
        owners.Clear();
        using var roots = EntityManager.CreateEntityQuery(new EntityQueryDesc { Any = new[] {
            ComponentType.ReadOnly<EntityOwner>(), ComponentType.ReadOnly<ResourceNodeData>(),
            ComponentType.ReadOnly<Bullet>(), ComponentType.ReadOnly<ArtilleryBullet>() } });
        using var entities = roots.ToEntityArray(Allocator.Temp);
        foreach (var root in entities)
        {
            owners[root] = root;
            if (EntityManager.HasBuffer<LinkedEntityGroup>(root))
                foreach (var member in EntityManager.GetBuffer<LinkedEntityGroup>(root, true)) owners[member.Value] = root;
        }
        foreach (var pair in memories) pair.Value.Count = View.Entities.TryGetValue(pair.Key, out var data) && data.IsVisible ? 0 : pair.Value.Count;
        using var renderers = EntityManager.CreateEntityQuery(typeof(MaterialMeshInfo), typeof(LocalToWorld));
        using var renderEntities = renderers.ToEntityArray(Allocator.Temp);
        foreach (var renderer in renderEntities)
        {
            var root = FindOwner(renderer);
            if (root == Entity.Null) continue;
            View.Entities.TryGetValue(root, out var data);
            bool visible = Frame.IsValid && data.IsVisible;
            SetHidden(renderer, !visible);
            if (visible && data.IsStatic) Capture(root, renderer);
        }
        // Hybrid selection/minimap sprites are not controlled by DisableRendering.
        using var sprites = EntityManager.CreateEntityQuery(typeof(SpriteRenderer));
        using var spriteEntities = sprites.ToEntityArray(Allocator.Temp);
        foreach (var sprite in spriteEntities)
        {
            var root = FindOwner(sprite);
            if (root == Entity.Null) continue;
            var renderer = EntityManager.GetComponentObject<SpriteRenderer>(sprite);
            bool visible = View.Entities.TryGetValue(root, out var data) && data.IsVisible;
            if (!visible)
            {
                if (!hiddenSprites.ContainsKey(sprite)) hiddenSprites.Add(sprite, renderer.enabled);
                renderer.enabled = false;
            }
            else if (hiddenSprites.TryGetValue(sprite, out bool enabled))
            {
                renderer.enabled = enabled;
                hiddenSprites.Remove(sprite);
            }
            if (visible && EntityManager.HasComponent<Selected>(root)
                && EntityManager.GetComponentData<Selected>(root).visualEntity == sprite)
                renderer.enabled = EntityManager.IsComponentEnabled<Selected>(root);
        }
        using var icons = EntityManager.CreateEntityQuery(typeof(MinimapRenderer));
        using var iconEntities = icons.ToEntityArray(Allocator.Temp);
        foreach (var entity in iconEntities)
        {
            var root = FindOwner(entity);
            if (root == Entity.Null || !View.Entities.TryGetValue(root, out var data) || !data.IsVisible) continue;
            var icon = EntityManager.GetComponentData<MinimapRenderer>(entity);
            bool selected = EntityManager.HasComponent<Selected>(root) && EntityManager.IsComponentEnabled<Selected>(root);
            if (EntityManager.Exists(icon.selectedIcon) && EntityManager.HasComponent<SpriteRenderer>(icon.selectedIcon)) EntityManager.GetComponentObject<SpriteRenderer>(icon.selectedIcon).enabled = selected;
            if (EntityManager.Exists(icon.nonSelectIcon) && EntityManager.HasComponent<SpriteRenderer>(icon.nonSelectIcon)) EntityManager.GetComponentObject<SpriteRenderer>(icon.nonSelectIcon).enabled = !selected;
        }
        removed.Clear();
        foreach (var pair in memories)
        {
            if (!View.Entities.TryGetValue(pair.Key, out var data)) removed.Add(pair.Key);
            else if (!data.IsVisible) pair.Value.Draw();
        }
        foreach (var key in removed) { memories[key].Clear(); memories.Remove(key); }
        hidden.RemoveWhere(e => !EntityManager.Exists(e));
        removed.Clear();
        foreach (var key in hiddenSprites.Keys) if (!EntityManager.Exists(key)) removed.Add(key);
        foreach (var key in removed) hiddenSprites.Remove(key);
    }

    Entity FindOwner(Entity entity)
    {
        for (int depth = 0; depth < 64 && EntityManager.Exists(entity); depth++)
        {
            if (owners.TryGetValue(entity, out var root)) return root;
            if (!EntityManager.HasComponent<Parent>(entity)) break;
            entity = EntityManager.GetComponentData<Parent>(entity).Value;
        }
        return Entity.Null;
    }
    void SetHidden(Entity entity, bool hide)
    {
        if (hide)
        { if (!EntityManager.HasComponent<DisableRendering>(entity)) { EntityManager.AddComponent<DisableRendering>(entity); hidden.Add(entity); } }
        else if (hidden.Remove(entity) && EntityManager.HasComponent<DisableRendering>(entity)) EntityManager.RemoveComponent<DisableRendering>(entity);
    }
    void Capture(Entity root, Entity renderer)
    {
        if (EntityManager.HasComponent<DisableRendering>(renderer)) return;
        if (!memories.TryGetValue(root, out var memory)) memories.Add(root, memory = new MemoryVisual());
        var info = EntityManager.GetComponentData<MaterialMeshInfo>(renderer);
        var matrix = (Matrix4x4)EntityManager.GetComponentData<LocalToWorld>(renderer).Value;
        float? reveal = EntityManager.HasComponent<RevealHeightProperty>(renderer) ? EntityManager.GetComponentData<RevealHeightProperty>(renderer).Value : null;
        if (EntityManager.HasComponent<RenderMeshArray>(renderer))
        {
            var array = EntityManager.GetSharedComponentManaged<RenderMeshArray>(renderer);
            if (info.HasMaterialMeshIndexRange)
            {
                var range = info.MaterialMeshIndexRange;
                for (int i = range.start; i < range.end; i++)
                { var item = array.MaterialMeshIndices[i]; memory.Capture(array.MeshReferences[item.MeshIndex].Value, array.MaterialReferences[item.MaterialIndex].Value, matrix, item.SubMeshIndex, reveal); }
            }
            else if (info.Material < 0 && info.Mesh < 0) memory.Capture(array.GetMesh(info), array.GetMaterial(info), matrix, info.SubMesh, reveal);
            else CaptureRuntime(memory, info, matrix, reveal);
        }
        else CaptureRuntime(memory, info, matrix, reveal);
    }
    void CaptureRuntime(MemoryVisual memory, MaterialMeshInfo info, Matrix4x4 matrix, float? reveal)
    {
        var graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        if (graphics != null && info.Material >= 0 && info.Mesh >= 0)
            memory.Capture(graphics.GetMesh(info.MeshID), graphics.GetMaterial(info.MaterialID), matrix, info.SubMesh, reveal);
    }
    void UpdateMask()
    {
        if (!Frame.IsValid) { if (maskPixels != null) { System.Array.Clear(maskPixels, 0, maskPixels.Length); Mask.SetPixels32(maskPixels); Mask.Apply(false); } return; }
        if (!Mask || Mask.width != Frame.Width || Mask.height != Frame.Height)
        { Release(Mask); Mask = new Texture2D(Frame.Width, Frame.Height, TextureFormat.RGBA32, false, true) { name = "Local FOW", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp }; maskPixels = new Color32[Frame.Width * Frame.Height]; }
        System.Array.Clear(maskPixels, 0, maskPixels.Length);
        foreach (var cell in View.Terrain.Cells.Keys) maskPixels[cell] = new Color32(80, 80, 80, 255);
        foreach (var cell in Frame.VisibleCells) maskPixels[cell] = new Color32(255, 255, 255, 255);
        Mask.SetPixels32(maskPixels); Mask.Apply(false);
        var camera = Camera.main;
        if (fogCamera != camera)
        {
            RestoreCamera(); fogCamera = camera;
            if (camera) { cameraFlags = camera.clearFlags; cameraBackground = camera.backgroundColor; }
        }
        if (camera) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; }
    }
    void RestoreCamera() { if (fogCamera) { fogCamera.clearFlags = cameraFlags; fogCamera.backgroundColor = cameraBackground; } fogCamera = null; }
    void ResetMemory()
    {
        View.Clear(); foreach (var memory in memories.Values) memory.Clear(); memories.Clear();
    }
    protected override void OnDestroy()
    {
        foreach (var entity in hidden) if (EntityManager.Exists(entity) && EntityManager.HasComponent<DisableRendering>(entity)) EntityManager.RemoveComponent<DisableRendering>(entity);
        foreach (var pair in hiddenSprites) if (EntityManager.Exists(pair.Key) && EntityManager.HasComponent<SpriteRenderer>(pair.Key)) EntityManager.GetComponentObject<SpriteRenderer>(pair.Key).enabled = pair.Value;
        ResetMemory(); Frame?.Dispose(); Release(Mask); RestoreCamera();
    }
}

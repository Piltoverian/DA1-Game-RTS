using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

// Only renderer matrices are changed. LocalTransform, physics, flow fields and ORCA remain flat.
[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst=true)]
[UpdateBefore(typeof(EntitiesGraphicsSystem))]
public partial class TerrainVisualOffsetSystem : SystemBase
{
    EntityQuery renderQuery;
    EntityQuery matrixQuery;
    Dictionary<Entity,float4x4> baseline;
    Dictionary<Entity,float4x4> displayed;
    readonly HashSet<Entity> live=new HashSet<Entity>();
    readonly List<Entity> stale=new List<Entity>();
    void EnsureCaches(){baseline??=new Dictionary<Entity,float4x4>();displayed??=new Dictionary<Entity,float4x4>();}
    bool wasActive;
    // Restore before physics/simulation reads LocalToWorld on the next frame.
    // Static collider entities can also carry render data, so presentation matrices
    // must not leak into PhysicsWorld or blockage calculations.
    public void RestoreLogicalMatrices() {
        if(!wasActive || World==null || !World.IsCreated)return;
        EnsureCaches();Dependency.Complete();
        EntityManager.CompleteDependencyBeforeRW<LocalToWorld>();
        // Enumerate the current world, never probe cached entity handles after a
        // structural change (death, resource depletion or subscene unloading).
        using var entities=matrixQuery.ToEntityArray(Allocator.Temp);
        live.Clear();
        foreach(var entity in entities){
            if(!displayed.TryGetValue(entity,out var previous))continue;
            live.Add(entity);
            var current=EntityManager.GetComponentData<LocalToWorld>(entity).Value;
            if(current.Equals(previous)&&baseline.TryGetValue(entity,out var original))
                EntityManager.SetComponentData(entity,new LocalToWorld{Value=original});
        }
        stale.Clear();
        foreach(var entity in displayed.Keys)if(!live.Contains(entity))stale.Add(entity);
        foreach(var entity in stale){baseline.Remove(entity);displayed.Remove(entity);}
    }
    protected override void OnCreate(){EnsureCaches();renderQuery=GetEntityQuery(ComponentType.ReadWrite<LocalToWorld>(),ComponentType.ReadOnly<MaterialMeshInfo>(),ComponentType.Exclude<DisableRendering>());matrixQuery=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadWrite<LocalToWorld>()},Options=EntityQueryOptions.IncludeDisabledEntities|EntityQueryOptions.IncludePrefab});}
    protected override void OnUpdate() {
        if(World!=World.DefaultGameObjectInjectionWorld)return;
        EnsureCaches();
        var surface=TerrainVisualSurface.Active;
        if(surface==null&&!wasActive)return;
        Dependency.Complete();
        // SystemBase.GetComponentLookup registers the component dependency and
        // completes existing jobs when a new reader/writer type is introduced.
        var local=GetComponentLookup<LocalTransform>(true);var parents=GetComponentLookup<Parent>(true);var post=GetComponentLookup<PostTransformMatrix>(true);
        using var entities=renderQuery.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities){
            float4x4 current=EntityManager.GetComponentData<LocalToWorld>(entity).Value,original=current;
            if(TryLogicalMatrix(entity,ref local,ref parents,ref post,out var logical))original=logical;
            else if(displayed.TryGetValue(entity,out var previous)&&current.Equals(previous)&&baseline.TryGetValue(entity,out var cached))original=cached;
            baseline[entity]=original;
            if(surface!=null)original.c3.y+=surface.Sample(original.c3.x,original.c3.z);
            EntityManager.SetComponentData(entity,new LocalToWorld{Value=original});displayed[entity]=original;
        }
        // Restore ordinary matrices when the terrain presentation is disabled.
        if(surface==null){baseline.Clear();displayed.Clear();}
        wasActive=surface!=null;
    }
    // Compose from leaf to root without an unsafe stack buffer. Broken or cyclic
    // hierarchies fall back to the last logical matrix instead of traversing forever.
    static bool TryLogicalMatrix(Entity entity,ref ComponentLookup<LocalTransform> local,
        ref ComponentLookup<Parent> parents,ref ComponentLookup<PostTransformMatrix> post,out float4x4 matrix) {
        matrix=float4x4.identity;
        for(int depth=0;depth<64;depth++) {
            if(!local.TryGetComponent(entity,out var transform))return false;
            var part=transform.ToMatrix();
            if(post.TryGetComponent(entity,out var scale))part=math.mul(part,scale.Value);
            matrix=math.mul(part,matrix);
            if(!parents.TryGetComponent(entity,out var parent))return true;
            entity=parent.Value;
        }
        return false;
    }
}

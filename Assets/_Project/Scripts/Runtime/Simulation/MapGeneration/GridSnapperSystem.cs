using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(MapTerrainGenerationSystem))]
public partial class GridSnapperSystem : SystemBase
{
    // World-owned immutable collider cache. Instances share these blobs safely;
    // no per-entity cleanup handles get duplicated when a prefab is instantiated.
    readonly Dictionary<(float3 size,float3 center,byte shape,CollisionFilter filter,Unity.Physics.Material material),BlobAssetReference<Unity.Physics.Collider>> colliders=new();
    protected override void OnCreate(){RequireForUpdate<GridComponent>();}
    protected override void OnUpdate()
    {
        using var grids=EntityManager.CreateEntityQuery(typeof(GridComponent));
        if(grids.CalculateEntityCount()!=1)return;
        var grid=grids.GetSingleton<GridComponent>();
        using var query=EntityManager.CreateEntityQuery(new EntityQueryDesc{
            All=new[]{ComponentType.ReadOnly<Snapping>(),ComponentType.ReadOnly<ModelSnapSource>(),ComponentType.ReadWrite<LocalTransform>()},
            Options=EntityQueryOptions.IncludePrefab|EntityQueryOptions.IncludeDisabledEntities});
        using var entities=query.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities)Apply(entity,grid);
    }
    public void Apply(Entity entity,GridComponent grid)
    {
        var em=EntityManager;
        var source=em.GetComponentData<ModelSnapSource>(entity);
        bool agent=em.HasComponent<MovementAgentAvoidanceComponent>(entity);
        float side=agent?source.DiameterCells*grid.cellsize:source.Cells*grid.cellsize;
        if(!(side>0)||!math.isfinite(side)||math.any(source.Size<=0))throw new InvalidOperationException("Invalid snapping radius/size");
        float3 resize=new float3(side/source.Size.x,math.min(side/source.Size.x,side/source.Size.z),side/source.Size.z);
        float3 center=new float3(0,source.Center.y*resize.y,0);
        float3 size=new float3(side,source.Size.y*resize.y,side);
        BlobAssetReference<Unity.Physics.Collider> collider=default;
        if(em.HasComponent<PhysicsCollider>(entity)){
            var old=em.GetComponentData<PhysicsCollider>(entity).Value;
            if(!old.IsCreated)throw new InvalidOperationException("Snapping requires a baked physics collider");
            if(old.Value.Type==ColliderType.Compound)throw new InvalidOperationException("Compound collider snapping requires an explicit root collider; do not approximate its children.");
            var filter=old.Value.GetCollisionFilter();
            var material=Unity.Physics.Material.Default;
            material.Friction=old.Value.GetFriction();material.Restitution=old.Value.GetRestitution();material.CollisionResponse=old.Value.GetCollisionResponse();
            material.FrictionCombinePolicy=source.FrictionCombine;material.RestitutionCombinePolicy=source.RestitutionCombine;
            var key=(size,center,source.Shape,filter,material);
            if(!colliders.TryGetValue(key,out collider)){
                if(source.Shape==1)collider=Unity.Physics.SphereCollider.Create(new SphereGeometry{Center=center,Radius=side*.5f},filter,material);
                else if(source.Shape==2){float half=math.max(0,size.y*.5f-side*.5f);collider=Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry{Vertex0=center-new float3(0,half,0),Vertex1=center+new float3(0,half,0),Radius=side*.5f},filter,material);}
                else collider=Unity.Physics.BoxCollider.Create(new BoxGeometry{Center=center,Size=size,Orientation=quaternion.identity,BevelRadius=0},filter,material);
                colliders.Add(key,collider);
            }
        }
        // Absolute scale from immutable source, so re-enabling the tag is idempotent.
        var transform=em.GetComponentData<LocalTransform>(entity);transform.Scale=1;
        if(!agent&&!em.HasComponent<Prefab>(entity))transform.Position=GridSnapperMath.Snap(transform.Position,grid,source.Cells);
        var post=new PostTransformMatrix{Value=float4x4.TRS(new float3(-source.Center.x*resize.x,0,-source.Center.z*resize.z),quaternion.identity,source.RootScale*resize)};
        if(em.HasComponent<PostTransformMatrix>(entity))em.SetComponentData(entity,post);else em.AddComponentData(entity,post);
        em.SetComponentData(entity,transform);
        if(em.HasComponent<PendingHeight>(entity))em.SetComponentEnabled<PendingHeight>(entity,true);
        if(collider.IsCreated){
            em.SetComponentData(entity,new PhysicsCollider{Value=collider});
            if(em.HasComponent<PhysicsMass>(entity)){
                var mass=em.GetComponentData<PhysicsMass>(entity);
                var resized=mass.InverseMass>0?PhysicsMass.CreateDynamic(collider.Value.MassProperties,1/mass.InverseMass):PhysicsMass.CreateKinematic(collider.Value.MassProperties);
                resized.InverseInertia=math.select(resized.InverseInertia,float3.zero,mass.InverseInertia==float3.zero);
                em.SetComponentData(entity,resized);
            }
        }
        if(agent){var avoidance=em.GetComponentData<MovementAgentAvoidanceComponent>(entity);float factor=side*.5f/math.max(.0001f,avoidance.radius);avoidance.radius=side*.5f;em.SetComponentData(entity,avoidance);
            if(em.HasComponent<MovementSteeringComponent>(entity)){var steering=em.GetComponentData<MovementSteeringComponent>(entity);steering.arrivalRadius*=factor;steering.stoppingDistance*=factor;em.SetComponentData(entity,steering);}}
        else if(em.HasComponent<GridFootprint>(entity)){var footprint=em.GetComponentData<GridFootprint>(entity);footprint.WorldSize=side;em.SetComponentData(entity,footprint);}
        if(!agent&&em.HasComponent<BlockageData>(entity)){
            var rect=new StartEndRect(new float2(-side*.5f));rect.ExpandTo(new float2(side*.5f));
            var blockage=em.GetComponentData<BlockageData>(entity);blockage.LocalRect=rect;em.SetComponentData(entity,blockage);
            if(em.HasComponent<BlockageNeedBakeTag>(entity))em.SetComponentEnabled<BlockageNeedBakeTag>(entity,true);
        }
        em.SetComponentEnabled<Snapping>(entity,false);
    }
    protected override void OnDestroy(){foreach(var collider in colliders.Values)if(collider.IsCreated)collider.Dispose();colliders.Clear();}
}

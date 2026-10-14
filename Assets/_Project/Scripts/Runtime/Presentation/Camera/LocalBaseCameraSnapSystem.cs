using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct CameraSnapPending : IComponentData,IEnableableComponent {}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation|WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(MapTerrainPresentationSystem))]
public partial class LocalBaseCameraSnapSystem : SystemBase
{
    protected override void OnUpdate()
    {
        using var pending=EntityManager.CreateEntityQuery(typeof(MapPresentationBinding),typeof(MapPresentationState),typeof(CameraSnapPending));
        using var entities=pending.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities){
            if(EntityManager.GetComponentData<MapPresentationState>(entity).Phase!=MapPresentationPhase.Ready)continue;
            var binding=EntityManager.GetComponentObject<MapPresentationBinding>(entity);
            var camera=Camera.main;if(!camera||!binding.Snapper||!binding.Snapper.isActiveAndEnabled)continue;
            if(!binding.Snapper.snapOnReady){
                if(binding.Owner.frameRampOnLoad)IsometricTerrainCamera.FrameNearestRamp(binding.Map,camera);
                EntityManager.SetComponentEnabled<CameraSnapPending>(entity,false);continue;
            }
            using var bootstrap=EntityManager.CreateEntityQuery(typeof(PlayerBootstrapState));
            if(bootstrap.CalculateEntityCount()!=1||bootstrap.GetSingleton<PlayerBootstrapState>().Phase!=PlayerBootstrapPhase.Ready)continue;
            using var local=EntityManager.CreateEntityQuery(typeof(LocalTestPlayer));
            int playerId;
            if(local.CalculateEntityCount()==1)playerId=local.GetSingleton<LocalTestPlayer>().PlayerId;
            else if(local.CalculateEntityCount()!=0||GameManager.Instance==null||!SelectHelper.TryGetLocalPlayerId(out playerId))continue;
            using var bases=EntityManager.CreateEntityQuery(typeof(MainBaseTag),typeof(EntityOwner),typeof(LocalTransform));
            using var halls=bases.ToEntityArray(Allocator.Temp);
            foreach(var hall in halls){
                if(EntityManager.GetComponentData<EntityOwner>(hall).PlayerID!=playerId)continue;
                binding.Snapper.Snap(camera,EntityManager.GetComponentData<LocalTransform>(hall).Position);
                EntityManager.SetComponentEnabled<CameraSnapPending>(entity,false);break;
            }
        }
    }
}

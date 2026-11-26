using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.Scenes;
using UnityEngine;

// Scene configuration/lifetime bridge only. ECS systems own render orchestration.
public class TerrainMapRenderer : MonoBehaviour
{
    public TerrainTheme theme;
    public bool frameRampOnLoad = true;
    [HideInInspector] public bool snapToLocalBaseOnLoad = true; // Migration for existing scenes.
    World registeredWorld;
    Entity bindingEntity;
    readonly HashSet<World> sceneWorlds = new();
    public Entity BindingEntity => bindingEntity;
    public Camera ResolveCamera()
    {
        // Additive connection scenes can also contain a tagged MainCamera.
        // Terrain and camera snapping must use the same camera from this scene.
        foreach (var camera in Camera.allCameras)
            if (camera.isActiveAndEnabled && camera.gameObject.scene == gameObject.scene && camera.CompareTag("MainCamera"))
                return camera;
        return null;
    }
    void OnEnable(){if(Application.isPlaying)StartCoroutine(Register());}
    IEnumerator Register()
    {
        while(isActiveAndEnabled){
            EnsureSubScenesForNewNetworkWorlds();
            var world=FindPresentationWorld();
            if(world!=registeredWorld||(registeredWorld!=null&&!registeredWorld.IsCreated))ReleaseBinding();
            if(registeredWorld==null&&world!=null&&world.IsCreated){
                var snapper=GetComponent<LocalBaseCameraSnapper>();
                if(!snapper){snapper=gameObject.AddComponent<LocalBaseCameraSnapper>();snapper.snapOnReady=snapToLocalBaseOnLoad;}
                registeredWorld=world;
                bindingEntity=world.EntityManager.CreateEntity(typeof(MapPresentationState),typeof(CameraSnapPending));
                world.EntityManager.AddComponentObject(bindingEntity,new MapPresentationBinding{Owner=this,Snapper=snapper});
            }
            yield return null;
        }
    }
    void Clear()
    {
        StopAllCoroutines();
        ReleaseBinding();
        sceneWorlds.Clear();
    }
    void EnsureSubScenesForNewNetworkWorlds()
    {
        // Main can already be open when the connection UI creates Netcode Worlds.
        // SubScene.OnEnable only registers with Worlds that exist at that moment.
        foreach(var world in World.All){
            if(!world.IsCreated||(!world.IsClient()&&!world.IsServer())||sceneWorlds.Contains(world)
                ||world.GetExistingSystem<SceneSystem>().Equals(default(SystemHandle)))continue;
            bool found=false;
            foreach(var subScene in SubScene.AllSubScenes){
                if(!subScene||!subScene.isActiveAndEnabled||!subScene.AutoLoadScene||subScene.gameObject.scene!=gameObject.scene)continue;
                found=true;
                if(SceneSystem.GetSceneEntity(world.Unmanaged,subScene.SceneGUID)!=Entity.Null)continue;
                var scene=SceneSystem.LoadSceneAsync(world.Unmanaged,subScene.SceneGUID);
                world.EntityManager.AddComponentObject(scene,subScene);
            }
            if(found)sceneWorlds.Add(world);
        }
    }
    static World FindPresentationWorld()
    {
        var active = World.DefaultGameObjectInjectionWorld;
        if(active!=null&&active.IsCreated&&active.IsClient()&&!active.IsThinClient())return active;
        foreach(var world in World.All)if(world.IsCreated&&world.IsClient()&&!world.IsThinClient())return world;
        // Never render the server's complete authoritative map on a host.
        foreach(var world in World.All)if(world.IsCreated&&!world.IsServer()&&!world.IsClient()&&!world.IsThinClient()
            &&(world.Flags&WorldFlags.Game)==WorldFlags.Game)return world;
        return null;
    }
    void ReleaseBinding()
    {
        if(registeredWorld!=null&&registeredWorld.IsCreated&&registeredWorld.EntityManager.Exists(bindingEntity)){
            var binding=registeredWorld.EntityManager.GetComponentObject<MapPresentationBinding>(bindingEntity);
            binding.Dispose();registeredWorld.EntityManager.DestroyEntity(bindingEntity);
        }
        registeredWorld=null;bindingEntity=Entity.Null;
    }
    void OnDisable(){Clear();}
    void OnDestroy(){Clear();}
}

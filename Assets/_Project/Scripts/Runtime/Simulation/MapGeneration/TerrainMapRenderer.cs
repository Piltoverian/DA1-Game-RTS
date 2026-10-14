using System.Collections;
using Unity.Entities;
using UnityEngine;

// Scene configuration/lifetime bridge only. ECS systems own render orchestration.
public class TerrainMapRenderer : MonoBehaviour
{
    public TerrainTheme theme;
    public bool frameRampOnLoad = true;
    [HideInInspector] public bool snapToLocalBaseOnLoad = true; // Migration for existing scenes.
    World registeredWorld;
    Entity bindingEntity;
    public Entity BindingEntity => bindingEntity;
    void OnEnable(){if(Application.isPlaying)StartCoroutine(Register());}
    IEnumerator Register()
    {
        while(isActiveAndEnabled){
            var world=World.DefaultGameObjectInjectionWorld;
            if(world!=null&&world.IsCreated){
                var snapper=GetComponent<LocalBaseCameraSnapper>();
                if(!snapper){snapper=gameObject.AddComponent<LocalBaseCameraSnapper>();snapper.snapOnReady=snapToLocalBaseOnLoad;}
                registeredWorld=world;
                bindingEntity=world.EntityManager.CreateEntity(typeof(MapPresentationState),typeof(CameraSnapPending));
                world.EntityManager.AddComponentObject(bindingEntity,new MapPresentationBinding{Owner=this,Snapper=snapper});
                yield break;
            }
            yield return null;
        }
    }
    void Clear()
    {
        StopAllCoroutines();
        if(registeredWorld!=null&&registeredWorld.IsCreated&&registeredWorld.EntityManager.Exists(bindingEntity)){
            var binding=registeredWorld.EntityManager.GetComponentObject<MapPresentationBinding>(bindingEntity);
            binding.Release();registeredWorld.EntityManager.DestroyEntity(bindingEntity);
        }
        registeredWorld=null;bindingEntity=Entity.Null;
    }
    void OnDisable(){Clear();}
    void OnDestroy(){Clear();}
}

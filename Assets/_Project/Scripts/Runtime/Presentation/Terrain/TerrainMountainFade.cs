using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

// One bounded update per terrain, no per-cell GameObjects or materials.
// Cursor + selected bounds are client-only interaction anchors, not gameplay visibility.
public sealed class TerrainMountainFade : MonoBehaviour
{
    Material material;
    Camera cameraView;
    TerrainVisualSurface surface;
    float radius;
    World selectionWorld;
    World contextWorld;
    EntityQuery selection;
    readonly Vector4[] anchors=new Vector4[16];
    readonly Vector4[] targets=new Vector4[16];
    readonly Vector3[] minimum=new Vector3[8];
    readonly Vector3[] maximum=new Vector3[8];
    readonly bool[] used=new bool[8];
    float nextSelection;
    int count;
    public void Initialize(Material value,Camera camera,TerrainVisualSurface data,float cells,World world=null)
    {
        material=value;cameraView=camera;surface=data;contextWorld=world;radius=Mathf.Max(.1f,cells)*data.map.grid.cellsize;
        material.SetVectorArray("_FadeAnchors",anchors);
    }
    void Update()
    {
        if(!material||!cameraView||surface==null)return;
        targets[0]=Vector4.zero;
        if(Mouse.current!=null&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject()))
        {
            var ray=cameraView.ScreenPointToRay(Mouse.current.position.ReadValue());
            if(surface.Raycast(ray,out _,out var point))targets[0]=new Vector4(point.x,point.y,point.z,radius);
        }
        // Query selected roots only, at 10Hz; no 2000 x terrain scan. Eight region bounds cover all selected units.
        if(Time.unscaledTime>=nextSelection)
        {
            nextSelection=Time.unscaledTime+.1f;for(int i=1;i<16;i++)targets[i]=Vector4.zero;count=0;
            var world=contextWorld;
            if(world!=null&&world.IsCreated)
            {
                if(selectionWorld!=world){if(selectionWorld!=null&&selectionWorld.IsCreated)selection.Dispose();selectionWorld=world;selection=world.EntityManager.CreateEntityQuery(typeof(Selected),typeof(LocalTransform));}
                using var positions=selection.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                System.Array.Clear(used,0,used.Length);
                foreach(var transform in positions)
                {
                    Vector3 p=transform.Position;int x=Mathf.Clamp((int)((p.x-surface.map.grid.origin.x)/surface.map.grid.cellsize*4/surface.map.grid.width),0,3);
                    int z=Mathf.Clamp((int)((p.z-surface.map.grid.origin.z)/surface.map.grid.cellsize*2/surface.map.grid.height),0,1),index=z*4+x;
                    if(!used[index]){minimum[index]=maximum[index]=p;used[index]=true;}else{minimum[index]=Vector3.Min(minimum[index],p);maximum[index]=Vector3.Max(maximum[index],p);}
                }
                for(int i=0;i<8;i++)if(used[i]){var center=(minimum[i]+maximum[i])*.5f;targets[++count]=new Vector4(center.x,center.y,center.z,radius+Vector3.Distance(minimum[i],maximum[i])*.5f);}
            }
        }
        float t=1-Mathf.Exp(-Time.unscaledDeltaTime*10);
        for(int i=0;i<16;i++)
        {
            if(targets[i].w>0&&anchors[i].w<=.001f){anchors[i]=targets[i];anchors[i].w=0;}
            if(targets[i].w<=0)anchors[i].w=Mathf.Lerp(anchors[i].w,0,t);
            else anchors[i]=Vector4.Lerp(anchors[i],targets[i],t);
        }
        material.SetVectorArray("_FadeAnchors",anchors);
    }
    void OnDestroy(){if(selectionWorld!=null&&selectionWorld.IsCreated)selection.Dispose();}
}

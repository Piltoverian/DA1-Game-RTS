using UnityEngine;
using UnityEngine.InputSystem;

// Keep the saved camera orthographic in Edit and Play modes; never reset it on Stop.
[ExecuteAlways, RequireComponent(typeof(Camera)), DefaultExecutionOrder(-1000)]
public sealed class IsometricTerrainCamera : MonoBehaviour
{


    [Range(8,60)] public float size=18;
    Camera view;
    public void Apply()
    {
        if(!view)view=GetComponent<Camera>();
        var rotation=Quaternion.Euler(TerrainTheme.CameraPitch,TerrainTheme.CameraYaw,0);
        if(Quaternion.Angle(transform.rotation,rotation)>.001f){
            Vector3 focus=transform.position;
            var ray=view.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))focus=ray.GetPoint(distance);
            float cameraHeight=Mathf.Max(15,transform.position.y);
            transform.rotation=rotation;transform.position=focus-transform.forward*(cameraHeight/-transform.forward.y);
        }
        view.orthographic=true;view.orthographicSize=size;view.ResetProjectionMatrix();
    }
    void OnEnable(){if(gameObject.scene.IsValid())Apply();}
    void OnValidate(){if(gameObject.scene.IsValid())Apply();}
    void Update(){if(!gameObject.scene.IsValid())return;if(Application.isPlaying&&Mouse.current!=null){float scroll=Mouse.current.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f)size=Mathf.Clamp(size-scroll/120f*2,8,60);}Apply();}
    public static void Configure(Camera camera){if(!camera)return;var setup=camera.GetComponent<IsometricTerrainCamera>();if(!setup)setup=camera.gameObject.AddComponent<IsometricTerrainCamera>();setup.Apply();}
    public static void FrameNearestRamp(GeneratedTerrain map,Camera camera)
    {
        if(!camera)return;
        if(FrameTerrainShowcase(map,camera))return;
        var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
        Vector3 focus=camera.transform.position;
        if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))focus=ray.GetPoint(distance);
        int selected=-1;float nearest=float.MaxValue;
        foreach(var draw in map.draws)if(draw.tile>=3&&draw.tile<=6){
            var p=new Vector3(map.grid.origin.x+(draw.cell%map.grid.width+.5f)*map.grid.cellsize,0,map.grid.origin.z+(draw.cell/map.grid.width+.5f)*map.grid.cellsize);
            float d=(p-new Vector3(focus.x,0,focus.z)).sqrMagnitude;if(d<nearest){nearest=d;selected=draw.cell;}
        }
        if(selected<0)return;
        var target=new Vector3(map.grid.origin.x+(selected%map.grid.width+.5f)*map.grid.cellsize,0,map.grid.origin.z+(selected/map.grid.width+.5f)*map.grid.cellsize);
        if(TerrainVisualSurface.Active!=null)target.y=TerrainVisualSurface.Active.Sample(target.x,target.z);
        camera.transform.position=target-camera.transform.forward*(Mathf.Max(15,camera.transform.position.y-target.y)/-camera.transform.forward.y);
    }

    // Frame real generated features together; never add mountains or change navigation.
    static bool FrameTerrainShowcase(GeneratedTerrain map,Camera camera)
    {
        var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
        Vector3 focus=camera.transform.position;
        if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))focus=ray.GetPoint(distance);
        int ramp=-1,mountain=-1,cliff=-1;float best=float.MaxValue;
        var legacyRamps=new System.Collections.Generic.HashSet<int>();
        foreach(var draw in map.draws)if(draw.tile>=3&&draw.tile<=6)legacyRamps.Add(draw.cell);
        for(int rampCell=0;rampCell<map.cells.Length;rampCell++){
            var cell=map.cells[rampCell];
            if(cell.isMountain||!cell.walkable)continue;
            if(cell.UsesVertexCorners?(cell.CornerMask==0||cell.CornerMask==15):!legacyRamps.Contains(rampCell))continue;
            int x=rampCell%map.grid.width,z=rampCell/map.grid.width;
            int rock=-1,wall=-1;float closest=float.MaxValue,closestWall=float.MaxValue;
            for(int dz=-12;dz<=12;dz++)for(int dx=-12;dx<=12;dx++){
                int nx=x+dx,nz=z+dz;
                if(nx<0||nz<0||nx>=map.grid.width||nz>=map.grid.height)continue;
                int i=nz*map.grid.width+nx;float d=dx*dx+dz*dz;
                if(map.cells[i].isMountain&&d<closest){rock=i;closest=d;}
                // Camera-facing cliff: higher cell facing a lower -X/-Z neighbour.
                if(!map.cells[i].isMountain&&((nx>0&&map.cells[i].heightLevel>map.cells[i-1].heightLevel)||
                    (nz>0&&map.cells[i].heightLevel>map.cells[i-map.grid.width].heightLevel)))if(d<closestWall){wall=i;closestWall=d;}
            }
            if(rock<0||wall<0)continue;
            var p=Position(rampCell);float score=(p-new Vector3(focus.x,0,focus.z)).sqrMagnitude+closest*map.grid.cellsize*map.grid.cellsize;
            if(score<best){best=score;ramp=rampCell;mountain=rock;cliff=wall;}
        }
        if(ramp<0){Debug.LogWarning("Terrain showcase: no ramp/mountain/cliff cluster within 12 cells; framing nearest ramp.");return false;}
        Vector3 Position(int i)=>new Vector3(map.grid.origin.x+(i%map.grid.width+.5f)*map.grid.cellsize,0,map.grid.origin.z+(i/map.grid.width+.5f)*map.grid.cellsize);
        Vector3 right=camera.transform.right,up=camera.transform.up;
        float left=float.MaxValue,rightEdge=float.MinValue,bottom=float.MaxValue,top=float.MinValue;
        foreach(int i in new[]{ramp,mountain,cliff}){
            var p=Position(i);if(TerrainVisualSurface.Active!=null)p.y=TerrainVisualSurface.Active.Sample(p.x,p.z);
            float px=Vector3.Dot(p,right),py=Vector3.Dot(p,up);
            float margin=map.grid.cellsize*2;
            left=Mathf.Min(left,px-margin);rightEdge=Mathf.Max(rightEdge,px+margin);
            bottom=Mathf.Min(bottom,py-margin);top=Mathf.Max(top,py+margin*2);
        }
        var target=Position(ramp);
        target+=right*((left+rightEdge)*.5f-Vector3.Dot(target,right))+up*((bottom+top)*.5f-Vector3.Dot(target,up));
        var setup=camera.GetComponent<IsometricTerrainCamera>();
        if(setup){setup.size=Mathf.Clamp(Mathf.Max((top-bottom)*.65f,(rightEdge-left)*.65f/Mathf.Max(.1f,camera.aspect)),8,60);setup.Apply();}
        camera.transform.position=target-camera.transform.forward*80;
        Debug.Log($"Terrain showcase: ramp={ramp}, mountain={mountain}, cliff={cliff}; orthographic size={camera.orthographicSize}.");
        return true;
    }
}

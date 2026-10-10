using System;
using UnityEngine;
using Unity.Mathematics;

// Presentation-only height field. Navigation and physics still use the flat legacy grid.
public sealed class TerrainVisualSurface
{
    public static TerrainVisualSurface Active { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession(){Active=null;}
    public readonly GeneratedTerrain map;
    public readonly float rise;
    readonly int minimum;
    readonly int[] rampDirections;
    public TerrainVisualSurface(GeneratedTerrain source,float tierRise) {
        map=source;rise=Mathf.Max(0,tierRise);minimum=int.MaxValue;
        foreach(var cell in map.cells) minimum=Math.Min(minimum,cell.heightLevel);
        rampDirections=new int[map.cells.Length];Array.Fill(rampDirections,-1);
        foreach(var draw in map.draws)if(draw.tile>=3&&draw.tile<=6)rampDirections[draw.cell]=draw.rotation;
    }
    public void Activate(){Active=this;}
    public void Deactivate(){if(Active==this)Active=null;}
    public bool IsRamp(int index) => map.cells[index].UsesVertexCorners ? map.cells[index].CornerMask!=0 : rampDirections[index] >= 0;
    public float CellHeight(int index,float fx,float fy) {
        var cell=map.cells[index];float high=(cell.heightLevel-minimum)*rise;
        if(cell.UsesVertexCorners) {
            int mask=cell.CornerMask;
            float nw=(mask&1)!=0?1:0,ne=(mask&2)!=0?1:0,se=(mask&4)!=0?1:0,sw=(mask&8)!=0?1:0;
            return high+rise*(fy<=fx ? nw+(ne-nw)*fx+(se-ne)*fy : nw+(se-sw)*fx+(sw-nw)*fy);
        }
        int d=rampDirections[index];if(d<0)return high;
        int x=index%map.grid.width,y=index/map.grid.width;
        int2 low=new int2(x,y)-GridTerrain.RampDirections[d];
        if(low.x<0||low.y<0||low.x>=map.grid.width||low.y>=map.grid.height)return high;
        float lowHeight=(map.cells[low.y*map.grid.width+low.x].heightLevel-minimum)*rise;
        float t=d==0?1-fy:d==1?fx:d==2?fy:1-fx;
        return Mathf.LerpUnclamped(lowHeight,high,t);
    }
    public float Sample(float worldX,float worldZ) {
        float x=(worldX-map.grid.origin.x)/map.grid.cellsize,z=(worldZ-map.grid.origin.z)/map.grid.cellsize;
        int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(z);
        if(ix<0||iy<0||ix>=map.grid.width||iy>=map.grid.height)return 0;
        return CellHeight(iy*map.grid.width+ix,x-ix,z-iy);
    }
    // Ray traverses only touched cells, testing exact two top triangles. No collider added.
    public bool Raycast(Ray ray,out Vector3 logical,out Vector3 visible,float maxDistance=1000) {
        logical=visible=default;var g=map.grid;
        float enter=0,exit=maxDistance;
        bool Slab(float origin,float direction,float min,float max) {
            if(Mathf.Abs(direction)<1e-7f)return origin>=min&&origin<=max;
            float a=(min-origin)/direction,b=(max-origin)/direction;if(a>b){float t=a;a=b;b=t;}
            enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);return enter<=exit;
        }
        if(!Slab(ray.origin.x,ray.direction.x,g.origin.x,g.origin.x+g.width*g.cellsize)||!Slab(ray.origin.z,ray.direction.z,g.origin.z,g.origin.z+g.height*g.cellsize))return false;
        var start=ray.GetPoint(enter+1e-4f);int x=Mathf.Clamp(Mathf.FloorToInt((start.x-g.origin.x)/g.cellsize),0,g.width-1),y=Mathf.Clamp(Mathf.FloorToInt((start.z-g.origin.z)/g.cellsize),0,g.height-1);
        int sx=ray.direction.x>=0?1:-1,sy=ray.direction.z>=0?1:-1;
        float dx=Mathf.Abs(ray.direction.x)<1e-7f?float.PositiveInfinity:g.cellsize/Mathf.Abs(ray.direction.x),dy=Mathf.Abs(ray.direction.z)<1e-7f?float.PositiveInfinity:g.cellsize/Mathf.Abs(ray.direction.z);
        float tx=float.IsInfinity(dx)?dx:(g.origin.x+(x+(sx>0?1:0))*g.cellsize-ray.origin.x)/ray.direction.x;
        float ty=float.IsInfinity(dy)?dy:(g.origin.z+(y+(sy>0?1:0))*g.cellsize-ray.origin.z)/ray.direction.z;
        for(int step=0;step<g.width+g.height+4;step++) {
            int i=y*g.width+x;float wx=g.origin.x+x*g.cellsize,wz=g.origin.z+y*g.cellsize,c=g.cellsize;
            Vector3 a=new Vector3(wx,CellHeight(i,0,0),wz),b=new Vector3(wx+c,CellHeight(i,1,0),wz),d=new Vector3(wx,CellHeight(i,0,1),wz+c),e=new Vector3(wx+c,CellHeight(i,1,1),wz+c);
            float best=float.PositiveInfinity;
            if(Triangle(ray,a,e,b,out float t1))best=t1;if(Triangle(ray,a,d,e,out float t2))best=Mathf.Min(best,t2);
            if(best>=enter-1e-4f&&best<=exit&&best<=Mathf.Min(tx,ty)+1e-4f){visible=ray.GetPoint(best);logical=new Vector3(visible.x,0,visible.z);return true;}
            if(tx<ty){enter=tx;tx+=dx;x+=sx;}else{enter=ty;ty+=dy;y+=sy;}
            if(enter>exit||x<0||y<0||x>=g.width||y>=g.height)return false;
        }return false;
    }
    static bool Triangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,out float distance) {
        distance=0;var e1=b-a;var e2=c-a;var p=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,p);if(Mathf.Abs(det)<1e-7f)return false;float inv=1/det;var t=ray.origin-a;float u=Vector3.Dot(t,p)*inv;if(u<0||u>1)return false;var q=Vector3.Cross(t,e1);float v=Vector3.Dot(ray.direction,q)*inv;if(v<0||u+v>1)return false;distance=Vector3.Dot(e2,q)*inv;return distance>=0;
    }
    public static Ray LogicalRay(Ray ray) {
        if(Active!=null&&Active.Raycast(ray,out _,out var visual))ray.origin-=Vector3.up*visual.y;
        return ray;
    }
}

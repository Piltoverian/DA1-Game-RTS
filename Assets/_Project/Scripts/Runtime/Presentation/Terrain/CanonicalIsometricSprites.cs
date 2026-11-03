using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// PNGs carry the shape. Runtime emits only flat, unwarped image rectangles.
public static class CanonicalIsometricSprites
{
    struct Face { public Vector3 anchor; public Texture2D texture; public float depth; public Color metadata; public long sequence; public int kind; public float tierRise; public Vector2 cell; }
    public static GameObject Build(GridComponent grid,TerrainTheme theme,TerrainVisualSurface surface,Vector3 forward,Material material,Dictionary<Texture2D,int> ids,Rect[] atlasRects,List<Mesh> ownedMeshes,IReadOnlyDictionary<int,TerrainCellVisualData> cells){
        GameObject result = null;
        var steps = BuildInSteps(grid, theme, surface, forward, material, ids, atlasRects, ownedMeshes, cells, value => result = value);
        while (steps.MoveNext()) { }
        return result;
    }
    // Runtime callers advance once per frame. Offline/export callers can drain the same routine synchronously.
    public static IEnumerator BuildInSteps(GridComponent grid,TerrainTheme theme,TerrainVisualSurface surface,Vector3 forward,Material material,Dictionary<Texture2D,int> ids,Rect[] atlasRects,List<Mesh> ownedMeshes,IReadOnlyDictionary<int,TerrainCellVisualData> cells,Action<GameObject> created){
        if(Mathf.Abs(forward.y+.5f)>.001f)throw new InvalidOperationException("Canonical 2:1 sprites require orthographic pitch30/yaw45");
        var root=new GameObject("Terrain presentation chunks (no colliders)");
        root.SetActive(false); created(root);
        var faces=new List<Face>();float size=grid.cellsize,rise=surface.rise;
        long cellSequence = 0; int collected = 0;
        float capRise=rise*Mathf.Max(1,(theme.mountainCaps[0].height-96)/32f);
        if(rise<=0)throw new InvalidOperationException("Canonical sprites require positive tier height");
        Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
        Vector3 screenUp=new Vector3(forward.x,0,forward.z).normalized/Mathf.Abs(forward.y);
        void Add(int x,int z,float height,Texture2D texture,Color metadata,Vector3? sortPosition=null,int kind=-1,float? tierRise=null){
            var anchor=new Vector3(grid.origin.x+(x+.5f)*size,height,grid.origin.z+(z+.5f)*size);
            faces.Add(new Face{anchor=anchor,texture=texture,depth=Vector3.Dot(sortPosition??anchor,forward),metadata=metadata,sequence=cellSequence++,kind=kind<0?(int)metadata.a:kind,tierRise=tierRise??rise,cell=new Vector2(x+.5f,z+.5f)});
        }
        foreach (var entry in cells) {
            int i=entry.Key, x=i%grid.width, z=i/grid.width;
            var cell=entry.Value;
            cellSequence=(long)i<<32;
            var h=cell.CornerHeights;
            int mask=cell.TopMask;
            Add(x,z,cell.TopHeight,theme.GetTopSprite(mask),new Color(1,1,1,mask==0?1:2),new Vector3(grid.origin.x+(x+.5f)*size,(h[0]+h[1]+h[2]+h[3])*.25f,grid.origin.z+(z+.5f)*size),cell.IsMountain?5:-1);
            foreach(var wall in cell.Walls){
                int side=wall.Side;
                Add(x,z,wall.Height,theme.GetWallSprite(side,wall.Mask),new Color(1,1,1,0),new Vector3(grid.origin.x+(x+(side==0?.5f:0))*size,wall.SortHeight,grid.origin.z+(z+(side==0?0:.5f))*size),cell.IsMountain?6:-1);
            }
            if(cell.IsMountain)
                Add(x,z,cell.MountainHeight,theme.mountainCaps[cell.MountainMask],Color.white,null,3,capRise);
            if (++collected % 512 == 0) yield return null;
        }

        // Global order survives batch boundaries. No per-chunk local sorting.
        // Stable global order, with merge work spread across frames rather than one unbounded List.Sort.
        var ordered=faces.ToArray(); var scratch=new Face[ordered.Length]; faces.Clear();
        int comparisons=0;
        for(int width=1;width<ordered.Length;width*=2){
            for(int left=0;left<ordered.Length;left+=width*2){
                int middle=Math.Min(left+width,ordered.Length),end=Math.Min(left+width*2,ordered.Length);
                int a=left,b=middle;
                for(int dst=left;dst<end;dst++){
                    bool takeA=b>=end || a<middle && Compare(ordered[a],ordered[b])<=0;
                    scratch[dst]=takeA?ordered[a++]:ordered[b++];
                    if(++comparisons%8192==0)yield return null;
                }
            }
            var swap=ordered;ordered=scratch;scratch=swap;
        }
        const int batchSize=2048;
        for(int first=0;first<ordered.Length;first+=batchSize){
            int count=Math.Min(batchSize,ordered.Length-first);
            var vertices=new List<Vector3>(count*4);var uv=new List<Vector2>(count*4);var fogCells=new List<Vector2>(count*4);var colors=new List<Color>(count*4);var faceData=new List<Vector4>(count*4);var triangles=new List<int>(count*6);
            for(int f=first;f<Math.Min(first+batchSize,ordered.Length);f++){
                var face=ordered[f];var anchor=face.anchor-forward*(face.anchor.y/forward.y);anchor.y=0;
                // Canonical artwork is anchored to one logical cell, including mountains.
                float width=size*Mathf.Sqrt(2),height=width*face.texture.height/face.texture.width;
                Vector2 pivot=new Vector2(.5f,48f/face.texture.height);
                var rect=atlasRects[ids[face.texture]];int start=vertices.Count;
                for(int k=0;k<4;k++){
                    float u=k==1||k==2?1:0,v=k>=2?1:0;
                    var p=anchor+right*((u-pivot.x)*width)+screenUp*((v-pivot.y)*height);p.y=0;vertices.Add(p);
                    fogCells.Add(face.cell);
                    uv.Add(new Vector2(Mathf.Lerp(rect.xMin,rect.xMax,u),Mathf.Lerp(rect.yMin,rect.yMax,v)));colors.Add(face.metadata);faceData.Add(new Vector4(face.anchor.y,face.tierRise,face.kind,theme.UsesManualColor(face.texture)?1:0));
                }
                triangles.Add(start);triangles.Add(start+2);triangles.Add(start+1);
                triangles.Add(start);triangles.Add(start+3);triangles.Add(start+2);
            }
            var mesh=new Mesh{name="Canonical isometric sprites "+first,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,faceData);mesh.SetUVs(2,fogCells);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            var go=new GameObject(mesh.name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortingOrder=first/batchSize-ordered.Length/batchSize-1;
            yield return null;
        }
        root.SetActive(true);
    }
    static int Compare(Face a, Face b){int depth=b.depth.CompareTo(a.depth);return depth!=0?depth:a.sequence.CompareTo(b.sequence);}
}

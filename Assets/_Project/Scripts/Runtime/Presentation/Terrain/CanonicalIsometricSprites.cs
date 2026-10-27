using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// PNGs carry the shape. Runtime emits only flat, unwarped image rectangles.
public static class CanonicalIsometricSprites
{
    struct Face { public Vector3 anchor; public Texture2D texture; public float depth; public Color metadata; public int sequence; public int kind; public float tierRise; public Vector2 cell; }
    public static GameObject Build(GeneratedTerrain map,TerrainTheme theme,TerrainVisualSurface surface,Vector3 forward,Material material,Dictionary<Texture2D,int> ids,Rect[] atlasRects,List<Mesh> ownedMeshes,IReadOnlyDictionary<int,TerrainCellVisualData> cells){
        if(Mathf.Abs(forward.y+.5f)>.001f)throw new InvalidOperationException("Canonical 2:1 sprites require orthographic pitch30/yaw45");
        var root=new GameObject("Terrain presentation chunks (no colliders)");
        var faces=new List<Face>();float size=map.grid.cellsize,rise=surface.rise;
        float capRise=rise*Mathf.Max(1,(theme.mountainCaps[0].height-96)/32f);
        if(rise<=0)throw new InvalidOperationException("Canonical sprites require positive tier height");
        Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
        Vector3 screenUp=new Vector3(forward.x,0,forward.z).normalized/Mathf.Abs(forward.y);
        void Add(int x,int z,float height,Texture2D texture,Color metadata,Vector3? sortPosition=null,int kind=-1,float? tierRise=null){
            var anchor=new Vector3(map.grid.origin.x+(x+.5f)*size,height,map.grid.origin.z+(z+.5f)*size);
            faces.Add(new Face{anchor=anchor,texture=texture,depth=Vector3.Dot(sortPosition??anchor,forward),metadata=metadata,sequence=faces.Count,kind=kind<0?(int)metadata.a:kind,tierRise=tierRise??rise,cell=new Vector2(x+.5f,z+.5f)});
        }
        for(int z=0;z<map.grid.height;z++)for(int x=0;x<map.grid.width;x++){
            int i=z*map.grid.width+x;
            if(!cells.TryGetValue(i,out var cell))continue;
            var h=cell.CornerHeights;
            int mask=cell.TopMask;
            Add(x,z,cell.TopHeight,theme.GetTopSprite(mask),new Color(1,1,1,mask==0?1:2),new Vector3(map.grid.origin.x+(x+.5f)*size,(h[0]+h[1]+h[2]+h[3])*.25f,map.grid.origin.z+(z+.5f)*size),cell.IsMountain?5:-1);
            foreach(var wall in cell.Walls){
                int side=wall.Side;
                Add(x,z,wall.Height,theme.GetWallSprite(side,wall.Mask),new Color(1,1,1,0),new Vector3(map.grid.origin.x+(x+(side==0?.5f:0))*size,wall.SortHeight,map.grid.origin.z+(z+(side==0?0:.5f))*size),cell.IsMountain?6:-1);
            }
            if(cell.IsMountain)
                Add(x,z,cell.MountainHeight,theme.mountainCaps[cell.MountainMask],Color.white,null,3,capRise);
        }

        // Global order survives batch boundaries. No per-chunk local sorting.
        faces.Sort((a,b)=>{int d=b.depth.CompareTo(a.depth);return d!=0?d:a.sequence.CompareTo(b.sequence);});
        const int batchSize=2048;
        for(int first=0;first<faces.Count;first+=batchSize){
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var fogCells=new List<Vector2>();var colors=new List<Color>();var faceData=new List<Vector4>();var triangles=new List<int>();
            for(int f=first;f<Math.Min(first+batchSize,faces.Count);f++){
                var face=faces[f];var anchor=face.anchor-forward*(face.anchor.y/forward.y);anchor.y=0;
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
                triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            var mesh=new Mesh{name="Canonical isometric sprites "+first,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,faceData);mesh.SetUVs(2,fogCells);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            var go=new GameObject(mesh.name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortingOrder=first/batchSize-faces.Count/batchSize-1;
        }
        Debug.Log($"CANONICAL_ISOMETRIC: {faces.Count} fixed-aspect sprites; global painter order; {ownedMeshes.Count} batches; rise={rise}; all vertex local/world Y=0.");
        return root;
    }
}

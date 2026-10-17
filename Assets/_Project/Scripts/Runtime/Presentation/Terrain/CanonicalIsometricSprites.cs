using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// PNGs carry the shape. Runtime emits only flat, unwarped image rectangles.
public static class CanonicalIsometricSprites
{
    struct Face { public Vector3 anchor; public Texture2D texture; public float depth; public Color metadata; public int sequence; public int kind; public float tierRise; public Vector2 cell; }
    public static GameObject Build(GeneratedTerrain map,TerrainTheme theme,TerrainVisualSurface surface,Vector3 forward,Material material,Dictionary<Texture2D,int> ids,Rect[] atlasRects,List<Mesh> ownedMeshes,IReadOnlyDictionary<int,GridTerrain> known=null){
        if(Mathf.Abs(forward.y+.5f)>.001f)throw new InvalidOperationException("Canonical 2:1 sprites require orthographic pitch30/yaw45");
        var root=new GameObject("Terrain presentation chunks (no colliders)");
        var faces=new List<Face>();float size=map.grid.cellsize,rise=surface.rise;
        var mountainField=MountainVisualField.BuildNatural(map,theme.mountainVisualTiers,theme.groundVisualSeed);
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
            if(known!=null&&!known.ContainsKey(i))continue;
            float[] h={surface.CellHeight(i,0,0),surface.CellHeight(i,1,0),surface.CellHeight(i,1,1),surface.CellHeight(i,0,1)};
            float bottom=Mathf.Min(Mathf.Min(h[0],h[1]),Mathf.Min(h[2],h[3]));int mask=0;
            for(int k=0;k<4;k++){float tier=(h[k]-bottom)/rise;if(tier>1.001f)throw new InvalidOperationException("Top cell spans more than one sprite tier");if(tier>.5f)mask|=1<<k;}
            Add(x,z,bottom,mask==0?theme.GetTopSprite(0):theme.GetTopSprite(mask),new Color(1,1,1,mask==0?1:2),new Vector3(map.grid.origin.x+(x+.5f)*size,(h[0]+h[1]+h[2]+h[3])*.25f,map.grid.origin.z+(z+.5f)*size),map.cells[i].isMountain?5:-1);
            // Only camera-facing -Z and -X walls. Each segment is exactly one tier.
            for(int side=0;side<2;side++){
                int j=side==0?(z>0?i-map.grid.width:-1):(x>0?i-1:-1);
                float a=h[0],b=h[side==0?1:3],oa=0,ob=0;
                if(j>=0){oa=surface.CellHeight(j,side==0?0:1,side==0?1:0);ob=surface.CellHeight(j,1,1);}
                // A higher foreground neighbour hides this edge; its own top covers it.
                float loA=Mathf.Min(a,oa),loB=Mathf.Min(b,ob),hiA=a,hiB=b;
                if(hiA<=loA+.001f&&hiB<=loB+.001f)continue;
                int first=Mathf.FloorToInt(Mathf.Min(loA,loB)/rise+.0001f),last=Mathf.CeilToInt(Mathf.Max(hiA,hiB)/rise-.0001f);
                for(int tier=first;tier<last;tier++){
                    float baseH=tier*rise;
                    float[] e={Mathf.Clamp01((loA-baseH)/rise),Mathf.Clamp01((loB-baseH)/rise),Mathf.Clamp01((hiB-baseH)/rise),Mathf.Clamp01((hiA-baseH)/rise)};
                    if(e[3]<=e[0]+.001f&&e[2]<=e[1]+.001f)continue;
                    int edgeMask=0;for(int k=0;k<4;k++){if(e[k]>.5f)edgeMask|=1<<k;}
                    if(!TerrainTheme.IsVisibleWallMask(edgeMask))continue;
                    Add(x,z,baseH,theme.GetWallSprite(side,edgeMask),new Color(1,1,1,0),new Vector3(map.grid.origin.x+(x+(side==0?.5f:0))*size,baseH+(e[0]+e[1]+e[2]+e[3])*.25f*rise,map.grid.origin.z+(z+(side==0?0:.5f))*size),map.cells[i].isMountain?6:-1);
                }
            }
            if(map.cells[i].isMountain){
                {
                    int stride=map.grid.width+1,a=z*stride+x;
                    int[] levels={mountainField[a],mountainField[a+1],mountainField[a+stride+1],mountainField[a+stride]};
                    int low=Math.Min(Math.Min(levels[0],levels[1]),Math.Min(levels[2],levels[3])),rockMask=0;
                    for(int k=0;k<4;k++){if(levels[k]-low>1)throw new InvalidOperationException("Mountain cap exceeds one tier");if(levels[k]>low)rockMask|=1<<k;}
                    Add(x,z,bottom+low*capRise,theme.mountainCaps[rockMask],Color.white,null,3,capRise);
                }
            }
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

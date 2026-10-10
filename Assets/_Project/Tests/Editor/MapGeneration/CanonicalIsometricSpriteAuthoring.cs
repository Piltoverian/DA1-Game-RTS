using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Offline template rasterizer. The exported PNGs are designer-editable assets.
public static class CanonicalIsometricSpriteAuthoring
{
    const string Folder="Assets/_Project/Data/Terrain/Sprites/ReferenceIsometricSprites/Canonical";
    const int Canvas=TerrainTheme.CanvasPixels;
    [MenuItem("RTS/Terrain/Build Canonical Isometric Sprite Library")]
    public static void Build(){
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before authoring sprites");
        var theme=StockIsometricSpriteValidation.LoadTheme();
        foreach(var source in new[]{theme.groundArtwork,theme.rampArtwork,theme.cliffArtwork})
            if(!source||!source.isReadable)throw new Exception("Offline bake requires readable ground/ramp/cliff sources");
        foreach(var corners in new[]{theme.groundSpriteCorners,theme.rampSpriteCorners,theme.cliffSpriteCorners})
            if(corners==null||corners.Length!=4)throw new Exception("Offline bake requires four UV corners per source");
        Directory.CreateDirectory(Folder);
        Vector2 Point(int corner,int high){var p=new[]{new Vector2(64,16),new Vector2(128,48),new Vector2(64,80),new Vector2(0,48)}[corner];return p+Vector2.up*(high*TerrainTheme.TierPixels);}
        Texture2D Raster(Vector2[] points,Texture2D source,Vector2[] sourceCorners,float shade){
            var result=new Texture2D(Canvas,Canvas,TextureFormat.RGBA32,false);var pixels=new Color32[Canvas*Canvas];
            int[] indices={0,1,2,0,2,3};
            for(int triangle=0;triangle<2;triangle++){
                int a=indices[triangle*3],b=indices[triangle*3+1],c=indices[triangle*3+2];var pa=points[a];var pb=points[b];var pc=points[c];
                float cross=(pb.x-pa.x)*(pc.y-pa.y)-(pb.y-pa.y)*(pc.x-pa.x);if(Mathf.Abs(cross)<.001f)continue;
                for(int y=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(pa.y,Mathf.Min(pb.y,pc.y))));y<Mathf.Min(Canvas,Mathf.CeilToInt(Mathf.Max(pa.y,Mathf.Max(pb.y,pc.y))));y++)
                for(int x=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(pa.x,Mathf.Min(pb.x,pc.x))));x<Mathf.Min(Canvas,Mathf.CeilToInt(Mathf.Max(pa.x,Mathf.Max(pb.x,pc.x))));x++){
                    var p=new Vector2(x+.5f,y+.5f);float wb=((p.x-pa.x)*(pc.y-pa.y)-(p.y-pa.y)*(pc.x-pa.x))/cross;
                    float wc=((pb.x-pa.x)*(p.y-pa.y)-(pb.y-pa.y)*(p.x-pa.x))/cross;float wa=1-wb-wc;
                    if(wa<-.00001f||wb<-.00001f||wc<-.00001f)continue;
                    var uv=sourceCorners[a]*wa+sourceCorners[b]*wb+sourceCorners[c]*wc;
                    // Slight inset is sampling-only; destination joins stay exact.
                    var center=(sourceCorners[0]+sourceCorners[1]+sourceCorners[2]+sourceCorners[3])*.25f;uv=Vector2.Lerp(uv,center,.06f);
                    Color color=source.GetPixelBilinear(uv.x,1-uv.y);
                    if(color.a<.5f)color=source.GetPixelBilinear(center.x,1-center.y);
                    color.r*=shade;color.g*=shade;color.b*=shade;color.a=1;pixels[y*Canvas+x]=color;
                }
            }
            result.SetPixels32(pixels);result.Apply();return result;
        }
        void Save(string name,Vector2[] points,Texture2D source,Vector2[] uv,float shade){
            string path=Folder+"/"+name+".png";
            // Rebuild does not overwrite PNGs subsequently painted by a designer.
            if(File.Exists(path))return;
            var texture=Raster(points,source,uv,shade);try{File.WriteAllBytes(path,texture.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        for(int mask=0;mask<16;mask++){
            var points=new Vector2[4];for(int k=0;k<4;k++)points[k]=Point(k,(mask>>k)&1);
            Save("top-"+mask.ToString("X1"),points,mask==0||mask==15?theme.groundArtwork:theme.rampArtwork,mask==0||mask==15?theme.groundSpriteCorners:theme.rampSpriteCorners,1);
        }
        for(int side=0;side<2;side++)for(int mask=0;mask<16;mask++){
            if(!TerrainTheme.IsVisibleWallMask(mask))continue;
            int a=0,b=side==0?1:3;var points=new[]{Point(a,mask&1),Point(b,(mask>>1)&1),Point(b,(mask>>2)&1),Point(a,(mask>>3)&1)};
            Save("cliff-"+side+"-"+mask.ToString("X1"),points,theme.cliffArtwork,theme.cliffSpriteCorners,side==0?1:.85f);
        }
        AssetDatabase.Refresh();
        Texture2D Load(string name){
            string path=Folder+"/"+name+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.isReadable=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        for(int mask=0;mask<16;mask++)theme.SetTopSprite(mask,Load("top-"+mask.ToString("X1")));
        for(int side=0;side<2;side++)for(int mask=0;mask<16;mask++)if(TerrainTheme.IsVisibleWallMask(mask))theme.SetWallSprite(side,mask,Load("cliff-"+side+"-"+mask.ToString("X1")));
        EditorUtility.SetDirty(theme);AssetDatabase.SaveAssetIfDirty(theme);
        Debug.Log("CANONICAL_SPRITE_LIBRARY_READY: 16 tops + 10 visible walls; 128x128 canvas; diamond128x64; tier32; existing designer PNGs preserved.");
    }
}

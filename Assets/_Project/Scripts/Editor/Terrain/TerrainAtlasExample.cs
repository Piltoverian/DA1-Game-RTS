using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Repackages existing project material art; generates no new AI artwork and never changes Main.
public static class TerrainAtlasExample
{
    public const string Folder="Assets/_Project/Data/Terrain/Generated/JadeSlateAtlas";
    [MenuItem("RTS/Terrain/Create JadeSlate Atlas Example")]
    public static void Create() => Selection.activeObject=Build();
    public static TerrainTheme Build()
    {
        var old=new Texture2D(2,2,TextureFormat.RGBA32,false);
        var atlas=new Texture2D(1536,1024,TextureFormat.RGBA32,false);
        try
        {
            if(!old.LoadImage(File.ReadAllBytes("ArtSource/Terrain/JadeSlate/materials.png")))throw new InvalidOperationException("JadeSlate source PNG unavailable");
            var pixels=new Color[1536*1024];
            for(int slot=0;slot<6;slot++)for(int y=0;y<512;y++)for(int x=0;x<512;x++)
            {
                float u=(x+.5f)/512,v=(y+.5f)/512;
                int quadrant=slot==0||slot==5?0:slot==1||slot==2?2:slot==3?1:3;
                Color color=old.GetPixelBilinear((quadrant%2+u)*.5f,(1-quadrant/2+v)*.5f);
                if(slot==2){float detail=Mathf.Clamp((color.r*.2126f+color.g*.7152f+color.b*.0722f)/.45f,.7f,1.3f);color=new Color(185/255f,112/255f,66/255f)*detail;color.a=1;}
                pixels[((1-slot/3)*512+y)*1536+slot%3*512+x]=color;
            }
            atlas.SetPixels(pixels);atlas.Apply();var theme=TerrainAtlasAuthoring.Import(atlas,Folder);
            return theme;
        }
        finally{UnityEngine.Object.DestroyImmediate(old);UnityEngine.Object.DestroyImmediate(atlas);}
    }
}

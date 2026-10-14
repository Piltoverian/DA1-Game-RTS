using System;
using UnityEngine;

// Only authoring. Uses exact canonical triangles; stores tier height in a paired linear texture.
public static class TerrainCanonicalBaker
{
    public sealed class Face
    {
        public Texture2D Color, Geometry;
    }
    public static Face Top(Texture2D source, int mask, int tierPixels=32)
    {
        var points = new[] { new Vector2(64,16), new Vector2(128,48), new Vector2(64,80), new Vector2(0,48) };
        var uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        var height = new float[4];
        for (int i=0;i<4;i++) { height[i] = (mask & (1<<i)) != 0 ? 1 : 0; points[i].y += tierPixels*height[i]; }
        return Raster(source, points, uv, height, false,0,Math.Max(128,80+tierPixels+16));
    }
    public static Face Wall(Texture2D source, int side, int mask)
    {
        Vector2 a = new Vector2(64,16), b = side == 0 ? new Vector2(128,48) : new Vector2(0,48);
        var h = new float[4]; for(int i=0;i<4;i++) h[i] = (mask & (1<<i)) != 0 ? 1 : 0;
        return Raster(source, new[] { a+Vector2.up*32*h[0], b+Vector2.up*32*h[1], b+Vector2.up*32*h[2], a+Vector2.up*32*h[3] },
            new[] { new Vector2(0,h[0]), new Vector2(1,h[1]), new Vector2(1,h[2]), new Vector2(0,h[3]) }, h, true, side);
    }
    static Face Raster(Texture2D source, Vector2[] p, Vector2[] uv, float[] heights, bool wall, int side=0,int canvasHeight=128)
    {
        if (!source || !source.isReadable) throw new ArgumentException("Readable source material required");
        var result = new Face { Color = new Texture2D(128,canvasHeight,TextureFormat.RGBA32,false), Geometry = new Texture2D(128,canvasHeight,TextureFormat.RGBA32,false,true) };
        var color = new Color[128*canvasHeight]; var geometry = new Color[color.Length];
        int[] triangles = {0,1,2,0,2,3};
        for(int t=0;t<6;t+=3)
        {
            int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
            float determinant = Cross(p[b]-p[a],p[c]-p[a]); if(Mathf.Abs(determinant)<1e-5f)continue;
            float shade = wall ? (side==0?.91f:.74f) : Shade(uv[a],uv[b],uv[c],heights[a],heights[b],heights[c]);
            for(int y=0;y<canvasHeight;y++)for(int x=0;x<128;x++)
            {
                var point=new Vector2(x+.5f,y+.5f)-p[a];
                float v=Cross(point,p[c]-p[a])/determinant,w=Cross(p[b]-p[a],point)/determinant,u=1-v-w;
                if(u<0||v<0||w<0)continue;
                var sample=uv[a]*u+uv[b]*v+uv[c]*w; float h=heights[a]*u+heights[b]*v+heights[c]*w;
                Color col=RepeatSample(source,sample.x,sample.y); col.r*=shade;col.g*=shade;col.b*=shade;col.a=1;
                int i=y*128+x;color[i]=col;geometry[i]=new Color(h,sample.x,sample.y,1);
            }
        }
        result.Color.SetPixels(color); result.Color.Apply(); result.Geometry.SetPixels(geometry);result.Geometry.Apply();return result;
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static float Shade(Vector2 a,Vector2 b,Vector2 c,float ha,float hb,float hc)
    {
        var p=new Vector3(a.x,ha*.4082483f,a.y);var q=new Vector3(b.x,hb*.4082483f,b.y);var r=new Vector3(c.x,hc*.4082483f,c.y);
        var normal=Vector3.Cross(r-p,q-p).normalized;if(normal.y<0)normal=-normal;
        return .72f+.28f*Mathf.Max(0,Vector3.Dot(normal,new Vector3(-.35f,1,-.45f).normalized));
    }
    public static Color RepeatSample(Texture2D texture,float u,float v)
    {
        // Wrap actual texels, not atlas coordinates: opposite panel borders never bleed to next material.
        float x=(u-Mathf.Floor(u))*texture.width-.5f,y=(v-Mathf.Floor(v))*texture.height-.5f;
        int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
        int Mod(int n,int size)=>(n%size+size)%size;
        Color a=Color.Lerp(texture.GetPixel(Mod(ix,texture.width),Mod(iy,texture.height)),texture.GetPixel(Mod(ix+1,texture.width),Mod(iy,texture.height)),x-ix);
        Color b=Color.Lerp(texture.GetPixel(Mod(ix,texture.width),Mod(iy+1,texture.height)),texture.GetPixel(Mod(ix+1,texture.width),Mod(iy+1,texture.height)),x-ix);
        return Color.Lerp(a,b,y-iy);
    }
}

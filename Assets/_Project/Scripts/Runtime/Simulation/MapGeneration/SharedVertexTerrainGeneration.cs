using System;
using System.Collections.Generic;
using Unity.Mathematics;

public enum TerrainGenerationAlgorithm { Legacy, SharedVertexSlopes }

// Reconstructs Lab 7's shared-corner rules; it does not reproduce its removed JS RNG.
public static class SharedVertexTerrainGeneration
{
    public static GeneratedTerrain Generate(GridComponent grid,TerrainGenerationSettings settings)
    {
        int stride=grid.width+1;
        var permutation=MapGenRNG.GetPermatureList(settings.seed);
        float Noise(float2 p,float wavelength)=>MapGenerator.fbm2D(p,3,2,1/wavelength,.5f,permutation)*2-1;
        string failure="Spawn core changed during relaxation";
        for(int attempt=0;attempt<16;attempt++) {
            var map=new GeneratedTerrain{grid=grid,attempt=attempt,cells=new GridTerrain[grid.width*grid.height]};
            map.spawns=TerrainGeneration.GenerateSpawnCells(grid,settings.seed+(uint)attempt*7919,settings.players,settings.protectedRadius);
            var levels=new int[stride*(grid.height+1)];
            float scale=Math.Min(grid.width,grid.height);
            float Height(float2 p)=>settings.landscapeHeight*(.8f+Noise(p+new float2(137+attempt*23,61),scale*.35f))
                +settings.detailAmplitude*Noise(p+new float2(47,-38),settings.wavelength);
            var spawnLevels=new int[map.spawns.Length];
            for(int b=0;b<spawnLevels.Length;b++)spawnLevels[b]=(int)math.round(Height((float2)map.spawns[b]+.5f)/settings.levelStep);
            for(int y=0;y<=grid.height;y++)for(int x=0;x<=grid.width;x++) {
                float2 p=new float2(x,y);float h=Height(p);
                for(int b=0;b<map.spawns.Length;b++) {
                    float r=math.distance(p,(float2)map.spawns[b]+.5f);
                    float blend=math.saturate((r-settings.protectedRadius-3)/8);
                    blend=blend*blend*(3-2*blend);
                    h=math.lerp(spawnLevels[b]*settings.levelStep,h,blend);
                }
                levels[y*stride+x]=(int)math.round(h/settings.levelStep);
            }
            // Monotone relaxation: every cell range <=1, with no alternating 5/10 saddle.
            var queue=new Queue<int>();var queued=new bool[map.cells.Length];
            for(int i=0;i<queued.Length;i++){queue.Enqueue(i);queued[i]=true;}
            while(queue.Count>0) {
                int cell=queue.Dequeue();queued[cell]=false;int x=cell%grid.width,y=cell/grid.width;
                int a=y*stride+x,b=a+1,c=a+stride+1,d=a+stride;
                int low=Math.Min(Math.Min(levels[a],levels[b]),Math.Min(levels[c],levels[d]));
                int[] corners={a,b,c,d};
                bool changed=false;
                foreach(int v in corners)if(levels[v]>low+1){levels[v]=low+1;changed=true;}
                int mask=0;for(int k=0;k<4;k++)if(levels[corners[k]]>low)mask|=1<<k;
                if(mask==5||mask==10){for(int k=0;k<4;k++)if((mask&(1<<k))!=0)levels[corners[k]]=low;changed=true;}
                if(!changed)continue;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++) {
                    int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=grid.width||ny>=grid.height)continue;
                    int i=ny*grid.width+nx;if(!queued[i]){queued[i]=true;queue.Enqueue(i);}
                }
            }
            bool protectedSpawns=true;
            for(int y=0;y<grid.height;y++)for(int x=0;x<grid.width;x++) {
                int a=y*stride+x;int[] h={levels[a],levels[a+1],levels[a+stride+1],levels[a+stride]};
                int low=Math.Min(Math.Min(h[0],h[1]),Math.Min(h[2],h[3]));byte mask=0;
                for(int k=0;k<4;k++)if(h[k]>low)mask|=(byte)(1<<k);
                if(mask==15)throw new InvalidOperationException("Unnormalised shared corner mask");
                int i=y*grid.width+x;
                // Majority corner tier becomes a horizontal terrace, NOT an automatic slope.
                int highCount=0;for(int k=0;k<4;k++)if((mask&(1<<k))!=0)highCount++;
                map.cells[i]=new GridTerrain{heightLevel=low+(highCount>=2?1:0),walkable=true,RampId=-1,RampDirection=-1};
                for(int b=0;b<map.spawns.Length;b++)if(math.distance(new float2(x+.5f,y+.5f),(float2)map.spawns[b]+.5f)<=settings.protectedRadius+2 && (mask!=0||low!=levels[map.spawns[b].y*stride+map.spawns[b].x]))protectedSpawns=false;
            }
            if(!protectedSpawns)continue;
            map.sourceVertexLevels=levels;
            // Curved shared-vertex contours can have no straight portal; late retries
            // use the same bounded block-median fallback as the legacy source.
            if(attempt>=8) {
                int side=Math.Max(6,settings.rampWidth+3);
                for(int by=0;by<grid.height;by+=side)for(int bx=0;bx<grid.width;bx+=side) {
                    var block=new List<int>();
                    for(int y=by;y<Math.Min(by+side,grid.height);y++)for(int x=bx;x<Math.Min(bx+side,grid.width);x++)block.Add(map.cells[y*grid.width+x].heightLevel);
                    block.Sort();int level=block[block.Count/2];
                    for(int y=by;y<Math.Min(by+side,grid.height);y++)for(int x=bx;x<Math.Min(bx+side,grid.width);x++)map.cells[y*grid.width+x].heightLevel=level;
                }
                for(int y=0;y<grid.height;y++)for(int x=0;x<grid.width;x++)for(int b=0;b<map.spawns.Length;b++)if(math.distance(new float2(x+.5f,y+.5f),(float2)map.spawns[b]+.5f)<=settings.protectedRadius+4)map.cells[y*grid.width+x].heightLevel=levels[map.spawns[b].y*stride+map.spawns[b].x];
            }
            TerrainGeneration.BuildMountainClusters(map,settings,settings.seed+(uint)attempt*7919);
            if(TerrainGeneration.TryFinishTerraces(map,settings,out failure))return map;
        }
        throw new InvalidOperationException("Shared vertex terrace terrain rejected 16 candidates: "+failure);
    }
}

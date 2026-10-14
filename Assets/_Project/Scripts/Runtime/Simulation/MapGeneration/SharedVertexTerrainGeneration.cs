using System;
using System.Collections.Generic;
using Unity.Mathematics;

// Reconstructs Lab 7's shared-corner rules; it does not reproduce its removed JS RNG.
public static class SharedVertexTerrainGeneration
{
    public static GeneratedTerrain Generate(GridComponent grid,TerrainGenerationSettings settings,Action<int,string> reportAttempt = null)
    {
        int stride=grid.width+1;
        var permutation=MapGenRNG.GetPermatureList(settings.seed);
        float Noise(float2 p,float wavelength)=>MapGenerator.fbm2D(p,3,2,1/wavelength,.5f,permutation)*2-1;
        string failure="No candidate accepted";
        // One workspace per invocation; reused across fallback candidates, safe for concurrent calls.
        int size=grid.width*grid.height;
        var walkLabels=new int[size];var cliffLabels=new int[size];var floodQueue=new int[size];
        var profile=settings.measureGeneration?new TerrainGenerationProfile():null;
        for(int attempt=0;attempt<16;attempt++) {
            reportAttempt?.Invoke(attempt,"Started");
            var map=new GeneratedTerrain{grid=grid,attempt=attempt,cells=new GridTerrain[size],walkLabels=walkLabels,cliffLabels=cliffLabels,floodQueue=floodQueue,profile=profile};
            long stamp=TerrainGenerationProfile.Start(map);

            var levels=new int[stride*(grid.height+1)];
            float scale=Math.Min(grid.width,grid.height);
            float Height(float2 p)=>settings.landscapeHeight*(.8f+Noise(p+new float2(137+attempt*23,61),scale*.35f))
                +settings.detailAmplitude*Noise(p+new float2(47,-38),settings.wavelength);
            for(int y=0;y<=grid.height;y++)for(int x=0;x<=grid.width;x++)
                levels[y*stride+x]=(int)math.round(Height(new float2(x,y))/settings.levelStep);
            TerrainGenerationProfile.End(map,"heightNoise",ref stamp);
            // Monotone relaxation: every cell range <=1, with no alternating 5/10 saddle.
            var queue=new Queue<int>();var queued=new bool[map.cells.Length];
            for(int i=0;i<queued.Length;i++){queue.Enqueue(i);queued[i]=true;}
            while(queue.Count>0) {
                int cell=queue.Dequeue();queued[cell]=false;int x=cell%grid.width,y=cell/grid.width;
                int a=y*stride+x,b=a+1,c=a+stride+1,d=a+stride;
                int low=Math.Min(Math.Min(levels[a],levels[b]),Math.Min(levels[c],levels[d]));

                bool changed=false;
                for(int k=0;k<4;k++){int v=k==0?a:k==1?b:k==2?c:d;if(levels[v]>low+1){levels[v]=low+1;changed=true;}}
                int mask=0;for(int k=0;k<4;k++)if(levels[k==0?a:k==1?b:k==2?c:d]>low)mask|=1<<k;
                if(mask==5||mask==10){for(int k=0;k<4;k++)if((mask&(1<<k))!=0)levels[k==0?a:k==1?b:k==2?c:d]=low;changed=true;}
                if(!changed)continue;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++) {
                    int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=grid.width||ny>=grid.height)continue;
                    int i=ny*grid.width+nx;if(!queued[i]){queued[i]=true;queue.Enqueue(i);}
                }
            }

            TerrainGenerationProfile.End(map,"vertexRelaxation",ref stamp);
            for(int y=0;y<grid.height;y++)for(int x=0;x<grid.width;x++) {
                int a=y*stride+x;int h0=levels[a],h1=levels[a+1],h2=levels[a+stride+1],h3=levels[a+stride];
                int low=Math.Min(Math.Min(h0,h1),Math.Min(h2,h3));byte mask=0;
                for(int k=0;k<4;k++)if((k==0?h0:k==1?h1:k==2?h2:h3)>low)mask|=(byte)(1<<k);
                if(mask==15)throw new InvalidOperationException("Unnormalised shared corner mask");
                int i=y*grid.width+x;
                // Majority corner tier becomes a horizontal terrace, NOT an automatic slope.
                int highCount=0;for(int k=0;k<4;k++)if((mask&(1<<k))!=0)highCount++;
                map.cells[i]=new GridTerrain{heightLevel=low+(highCount>=2?1:0),walkable=true,RampId=-1,RampDirection=-1};
            }
            map.sourceVertexLevels=levels;
            TerrainGenerationProfile.End(map,"terraceConversion",ref stamp);
            // Curved shared-vertex contours can have no straight portal; late retries
            // use a bounded block-median fallback.
            if(attempt>=8) {
                int side=Math.Max(6,settings.rampWidth+3);
                for(int by=0;by<grid.height;by+=side)for(int bx=0;bx<grid.width;bx+=side) {
                    var block=new List<int>();
                    for(int y=by;y<Math.Min(by+side,grid.height);y++)for(int x=bx;x<Math.Min(bx+side,grid.width);x++)block.Add(map.cells[y*grid.width+x].heightLevel);
                    block.Sort();int level=block[block.Count/2];
                    for(int y=by;y<Math.Min(by+side,grid.height);y++)for(int x=bx;x<Math.Min(bx+side,grid.width);x++)map.cells[y*grid.width+x].heightLevel=level;
                }
            }
            TerrainGenerationProfile.End(map,"blockFallback",ref stamp);
            try { TerrainTopologyOptimizer.PrepareBases(map,settings,settings.seed+(uint)attempt*7919); }
            catch(InvalidOperationException error){failure=error.Message;reportAttempt?.Invoke(attempt,failure);continue;}
            TerrainGenerationProfile.End(map,"base",ref stamp);
            TerrainGeneration.BuildMountainClusters(map,settings,settings.seed+(uint)attempt*7919);
            TerrainGenerationProfile.End(map,"mountain",ref stamp);
            if(TerrainGeneration.TryFinishTerraces(map,settings,out failure)){reportAttempt?.Invoke(attempt,"Accepted");return map;}
            reportAttempt?.Invoke(attempt,failure);
        }
        throw new InvalidOperationException("Shared vertex terrace terrain rejected 16 candidates: "+failure);
    }
}

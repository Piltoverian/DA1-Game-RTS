using System;
using System.Collections.Generic;

// Multi-source distance on shared vertices. Every neighbouring corner differs by <=1.
// The footprint is exclusively existing mountain cells; no simulation data is written.
public static class MountainVisualField
{
    public static int[] BuildNatural(GeneratedTerrain map,int maxTiers,int seed)
    {
        maxTiers=Math.Max(1,Math.Min(6,maxTiers));
        // Full distance is needed to locate the medial spine; early tier clamp would flatten its crest.
        var boundary=BoundaryDistance(map);var levels=new int[boundary.Length];
        int stride=map.grid.width+1,height=map.grid.height+1;
        var queue=new Queue<int>();
        for(int i=0;i<levels.Length;i++)
        {
            levels[i]=Math.Min(1,boundary[i]);
            int x=i%stride,z=i/stride,d=boundary[i];if(d<2||x==0||z==0||x==stride-1||z==height-1)continue;
            // Axis-local maxima of distance trace long interior ridges; strict tie-break avoids broad plateaus.
            bool spine=(d>=boundary[i-1]&&d>boundary[i+1])||(d>=boundary[i-stride]&&d>boundary[i+stride]);
            if(!spine)continue;
            uint hash;unchecked{hash=(uint)(x/12)*0x8da6b343u^(uint)(z/12)*0xd8163841u^(uint)seed;hash^=hash>>16;hash*=0x7feb352du;hash^=hash>>15;}
            int peak=Math.Min(maxTiers,Math.Max(2,maxTiers-(int)(hash%3)));
            levels[i]=Math.Min(d,peak);queue.Enqueue(i);
        }
        // Propagate from the whole spine, yielding connected slopes, secondary peaks and shallow saddles.
        while(queue.Count>0)
        {
            int i=queue.Dequeue(),x=i%stride,z=i/stride;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                int nx=x+dx,nz=z+dz;if(nx<0||nz<0||nx>=stride||nz>=height)continue;
                int j=nz*stride+nx,value=Math.Min(boundary[j],levels[i]-1);
                if(value<=levels[j])continue;levels[j]=value;queue.Enqueue(j);
            }
        }
        return levels;
    }
    static int[] BoundaryDistance(GeneratedTerrain map)
    {
        int stride=map.grid.width+1,height=map.grid.height+1;
        var distance=new int[stride*height];Array.Fill(distance,int.MaxValue);
        var queue=new Queue<int>();
        bool Mountain(int x,int z)=>x>=0&&z>=0&&x<map.grid.width&&z<map.grid.height&&map.cells[z*map.grid.width+x].isMountain;
        for(int z=0;z<height;z++)for(int x=0;x<stride;x++)
            if(!Mountain(x,z)||!Mountain(x-1,z)||!Mountain(x,z-1)||!Mountain(x-1,z-1)){int i=z*stride+x;distance[i]=0;queue.Enqueue(i);}
        while(queue.Count>0){int i=queue.Dequeue(),x=i%stride,z=i/stride;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++){
                int nx=x+dx,nz=z+dz;if(nx<0||nz<0||nx>=stride||nz>=height)continue;
                int j=nz*stride+nx;if(distance[j]<=distance[i]+1)continue;distance[j]=distance[i]+1;queue.Enqueue(j);
            }
        }
        return distance;
    }
}

using System;
using System.Collections.Generic;

// Multi-source distance on shared vertices. Every neighbouring corner differs by <=1.
// The footprint is exclusively existing mountain cells; no simulation data is written.
public static class MountainVisualField
{
    public static int[] Build(GeneratedTerrain map,int maxTiers)
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
        for(int i=0;i<distance.Length;i++)distance[i]=Math.Min(maxTiers,distance[i]);
        return distance;
    }
}

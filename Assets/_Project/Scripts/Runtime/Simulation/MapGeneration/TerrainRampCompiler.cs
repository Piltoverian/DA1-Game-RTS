using System;
using System.Collections.Generic;
using Unity.Mathematics;

// Compile exact low/high ramp crossings for shared height sampling.
// Canonical top/wall geometry reads corners; no retired sprite tile draw list remains.
public static class TerrainRampCompiler
{
    public static void Compile(GeneratedTerrain map, IReadOnlyDictionary<int,GridTerrain> known=null)
    {
        map.rampFaceDirections=Array.Empty<int>();
        if(map.cells.Length==0)return;
        var directions=new int[map.cells.Length];Array.Fill(directions,-1);
        var groups=new Dictionary<(int id,int level),int>();
        for(int high=0;high<map.cells.Length;high++)
        {
            var cell=map.cells[high];
            if((known!=null&&!known.ContainsKey(high))||cell.UsesVertexCorners
                ||!cell.walkable||cell.RampId<0||cell.RampDirection<0)continue;
            int direction=cell.RampDirection;
            int2 low=GridHelper.GetGridPosFromIndex(high,map.grid)+GridTerrain.RampDirections[(direction+2)%4];
            if(math.any(low<0)||low.x>=map.grid.width||low.y>=map.grid.height)continue;
            int index=GridHelper.GetNodeIndex(low,map.grid);
            if(known!=null&&!known.ContainsKey(index))continue;
            if(!map.cells[index].walkable||cell.heightLevel-map.cells[index].heightLevel!=1)continue;
            // Unknown placeholders are a height datum, not observed cliff topology.
            int lowerMask=0;
            if(known==null)lowerMask=TerrainGeneration.LowerMask(map,high);
            else for(int d=0;d<4;d++){
                int2 neighbor=GridHelper.GetGridPosFromIndex(high,map.grid)+GridTerrain.RampDirections[d];
                if(math.any(neighbor<0)||neighbor.x>=map.grid.width||neighbor.y>=map.grid.height)continue;
                int j=GridHelper.GetNodeIndex(neighbor,map.grid);
                if(known.ContainsKey(j)&&!map.cells[j].isMountain&&map.cells[j].heightLevel<cell.heightLevel)lowerMask|=1<<d;
            }
            if(lowerMask!=(1<<((direction+2)%4)))throw new InvalidOperationException("Ramp occupies a cliff corner");
            var key=(cell.RampId,cell.heightLevel);
            if(groups.TryGetValue(key,out int previous)&&previous!=direction)throw new InvalidOperationException("Ramp group has inconsistent directions");
            groups[key]=direction;directions[high]=direction;
        }
        map.rampFaceDirections=directions;
    }
}

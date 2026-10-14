using System;
using System.Collections.Generic;
using Unity.Mathematics;

// Compile exact low/high ramp crossings for shared height sampling.
// Canonical top/wall geometry reads corners; no retired sprite tile draw list remains.
public static class TerrainRampCompiler
{
    public static void Compile(GeneratedTerrain map)
    {
        map.rampFaceDirections=Array.Empty<int>();
        if(map.cells.Length==0||map.cells[0].UsesVertexCorners)return;
        var directions=new int[map.cells.Length];Array.Fill(directions,-1);
        var groups=new Dictionary<(int id,int level),int>();
        for(int high=0;high<map.cells.Length;high++)
        {
            var cell=map.cells[high];
            if(!cell.walkable||cell.RampId<0||cell.RampDirection<0)continue;
            int direction=cell.RampDirection;
            int2 low=GridHelper.GetGridPosFromIndex(high,map.grid)+GridTerrain.RampDirections[(direction+2)%4];
            if(math.any(low<0)||low.x>=map.grid.width||low.y>=map.grid.height)continue;
            int index=GridHelper.GetNodeIndex(low,map.grid);
            if(!map.cells[index].walkable||cell.heightLevel-map.cells[index].heightLevel!=1)continue;
            if(TerrainGeneration.LowerMask(map,high)!=(1<<((direction+2)%4)))throw new InvalidOperationException("Ramp occupies a cliff corner");
            var key=(cell.RampId,cell.heightLevel);
            if(groups.TryGetValue(key,out int previous)&&previous!=direction)throw new InvalidOperationException("Ramp group has inconsistent directions");
            groups[key]=direction;directions[high]=direction;
        }
        map.rampFaceDirections=directions;
    }
}

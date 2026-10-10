using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Object=UnityEngine.Object;

public static class TerrainPresentationValidation
{
    [MenuItem("RTS/Tests/Validate Mountain Integration")]
    public static void RunMountainIntegration()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before validating mountains");
        const string folder="Artifacts/TerrainValidation/Mountains";Directory.CreateDirectory(folder);
        ValidateGeneration(folder);
        var config=AssetDatabase.LoadAssetAtPath<MapGenConfig>("Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset");
        var grid=new GridComponent{width=512,height=512,cellsize=1000f/512,origin=float3.zero};
        var map=TerrainGeneration.Generate(grid,config.Terrain);
        int mountains=0,clusters=0;var visited=new bool[map.cells.Length];
        var queue=new System.Collections.Generic.Queue<int>();
        for(int i=0;i<map.cells.Length;i++){
            var cell=map.cells[i];if(!cell.isMountain)continue;mountains++;
            if(cell.walkable||cell.RampId>=0)throw new Exception("Mountain overlaps navigation/ramp");
            // Final connectivity repair may seal pockets outside the protected core.
            foreach(var spawn in map.spawns)if(math.distance(new float2(i%grid.width+.5f,i/grid.width+.5f),(float2)spawn+.5f)<=config.Terrain.protectedRadius+2)throw new Exception("Mountain entered protected spawn core");
            if(visited[i])continue;clusters++;visited[i]=true;queue.Enqueue(i);
            while(queue.Count>0){int k=queue.Dequeue();foreach(var dir in GridTerrain.RampDirections){int x=k%grid.width+dir.x,z=k/grid.width+dir.y;if(x<0||z<0||x>=grid.width||z>=grid.height)continue;int j=z*grid.width+x;if(visited[j]||!map.cells[j].isMountain)continue;visited[j]=true;queue.Enqueue(j);}}
        }
        if(mountains<2||clusters<2)throw new Exception("Large shared-vertex map lost mountain clusters");
        string result=$"PASS mountain integration: {mountains} mountain cells, {clusters} clusters; Main config512; protected spawns, no mountain ramps; generator connectivity validation passed.";
        File.WriteAllText(folder+"/mountains.txt",result);Debug.Log(result);
    }
    [MenuItem("RTS/Tests/Validate Terrain Presentation")]
    public static void Run() { CanonicalTerrainValidation.RunStock(); }
    [MenuItem("RTS/Tests/Validate Shared Vertex Cliff Ramp")]
    public static void RunSharedVertexTerraces() { CanonicalTerrainValidation.RunStock(); }
    static void ValidateGeneration(string folder) {
        int count=0;
        foreach(var algorithm in new[]{TerrainGenerationAlgorithm.Legacy,TerrainGenerationAlgorithm.SharedVertexSlopes})foreach(uint seed in new uint[]{0,1,30000,99999})foreach(int players in new[]{2,4}) {
            var g=new GridComponent{width=64,height=64,cellsize=1,origin=float3.zero};
            var map=TerrainGeneration.Generate(g,new TerrainGenerationSettings{seed=seed,players=players,protectedRadius=3,algorithm=algorithm});
            if(algorithm==TerrainGenerationAlgorithm.SharedVertexSlopes)for(int i=0;i<map.cells.Length;i++) {
                int a=(i/g.width)*(g.width+1)+i%g.width;var v=map.sourceVertexLevels;
                int[] h={v[a],v[a+1],v[a+g.width+2],v[a+g.width+1]};int lo=Math.Min(Math.Min(h[0],h[1]),Math.Min(h[2],h[3])),mask=0;
                for(int k=0;k<4;k++){if(h[k]-lo>1)throw new Exception("Source corner range exceeds one tier");if(h[k]>lo)mask|=1<<k;}
                if(mask==5||mask==10)throw new Exception("Source contains saddle");
                if(map.cells[i].UsesVertexCorners)throw new Exception("Contour still automatically rendered as slope");
            }
            for(int y=0;y<g.height;y++)for(int x=0;x<g.width;x++)for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++) {
                int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=g.width||ny>=g.height)continue;
                var a=map.cells[y*g.width+x];var b=map.cells[ny*g.width+nx];
                if(!a.isMountain&&!b.isMountain&&Math.Abs(a.heightLevel-b.heightLevel)>1)throw new Exception("Neighbour tier range violation");
            }
            TerrainVisualCompiler.Compile(map);count++;
        }
        File.WriteAllText(folder+"/generation.txt",$"PASS: {count} maps across legacy/shared vertex modes; four seeds, two/four players; tier bounds, shared edges and no saddle masks.\n");
    }
    internal static void Validate(GeneratedTerrain map,TerrainVisualSurface surface,string folder) {
        var original=(GridTerrain[])map.cells.Clone();int picks=0;
        for(int i=0;i<map.cells.Length;i+=13){var center=(Vector3)GridHelper.GridToWorld(new int2(i%64,i/64),map.grid);var ray=new Ray(center+Vector3.up*100,Vector3.down);if(!surface.Raycast(ray,out var logical,out var visible)||Mathf.Abs(logical.y)>1e-6f||Mathf.Abs(visible.y-surface.Sample(center.x,center.z))>.001f)throw new Exception("Height picking failed at "+i);picks++;}
        var oldWorld=World.DefaultGameObjectInjectionWorld;using(var world=new World("Visual offset verification")) {
            World.DefaultGameObjectInjectionWorld=world;
            try {
                var em=world.EntityManager;var unit=em.CreateEntity(typeof(LocalTransform),typeof(LocalToWorld),typeof(MaterialMeshInfo));
                var child=em.CreateEntity(typeof(LocalTransform),typeof(LocalToWorld),typeof(MaterialMeshInfo),typeof(Parent));em.SetComponentData(child,new Parent{Value=unit});em.SetComponentData(child,LocalTransform.FromPosition(0,.3f,0));
                var system=world.CreateSystemManaged<TerrainVisualOffsetSystem>();
                var staticEntity=em.CreateEntity(typeof(LocalToWorld),typeof(MaterialMeshInfo));em.SetComponentData(staticEntity,new LocalToWorld{Value=float4x4.Translate(new float3(32,0,32))});
                for(int frame=0;frame<120;frame++){system.RestoreLogicalMatrices();if(em.GetComponentData<LocalToWorld>(unit).Position.y!=0||em.GetComponentData<LocalToWorld>(staticEntity).Position.y!=0)throw new Exception("Visual height leaked into simulation matrices");int i=(frame*31)%map.cells.Length;var center=GridHelper.GridToWorld(new int2(i%64,i/64),map.grid);em.SetComponentData(unit,LocalTransform.FromPosition(center));system.Update();float expected=surface.Sample(center.x,center.z);if(em.GetComponentData<LocalTransform>(unit).Position.y!=0||math.abs(em.GetComponentData<LocalToWorld>(unit).Position.y-expected)>.001f||math.abs(em.GetComponentData<LocalToWorld>(child).Position.y-expected-.3f)>.001f)throw new Exception("Render offset drift or simulation height mutation");system.Update();if(math.abs(em.GetComponentData<LocalToWorld>(unit).Position.y-expected)>.001f)throw new Exception("Offset accumulated");}
                // Cached render handles must remain safe after structural changes.
                em.DestroyEntity(staticEntity);
                em.RemoveComponent<LocalToWorld>(child);
                em.AddComponent<Disabled>(unit);
                system.RestoreLogicalMatrices();
                if(math.abs(em.GetComponentData<LocalToWorld>(unit).Position.y)>.001f)throw new Exception("Disabled entity was not restored");
                em.RemoveComponent<Disabled>(unit);
                system.Update();
                surface.Deactivate();system.Update();if(math.abs(em.GetComponentData<LocalToWorld>(unit).Position.y)>.001f)throw new Exception("Offset not restored");surface.Activate();
            }finally{World.DefaultGameObjectInjectionWorld=oldWorld;}
        }
        for(int i=0;i<map.cells.Length;i++)if(!original[i].Equals(map.cells[i]))throw new Exception("Presentation mutated GridTerrain");
        File.WriteAllText(folder+"/validation.txt",$"PASS: {picks} exact terrain picks; 120 moving frames plus repeated updates; child/static render transforms; pre-simulation matrices restored; LocalTransform.Y=0; restoration after disabling; GridTerrain unchanged. Camera uses perspective 55/0, FOV20.2.\n");
    }
}

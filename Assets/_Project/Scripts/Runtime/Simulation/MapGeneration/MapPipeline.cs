using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum MapBuildPhase : byte { Requested, TerrainReady, NavigationReady, GameplayReady, Failed }
public struct MapBuildState : IComponentData
{
    public MapBuildPhase Phase;
    public uint Revision;
    public ulong TerrainHash;
    public FixedString512Bytes Error;
}

// Value-only match recipe; no ScriptableObject, camera or local-player dependency.
public struct MapGenerationRecipe : IComponentData
{
    public const int CurrentAlgorithmVersion=1;
    public int AlgorithmVersion;
    public uint Seed;
    public int Players, CliffCells, RampWidth, MaximumRampCells, MountainMinimumClusterCells, BattlefieldRadius;
    public float ProtectedRadius, LandscapeHeight, DetailAmplitude, Wavelength, LevelStep, SmallIslandFillBudget;
    public float MountainWavelength, MountainThreshold, MountainDensity, MountainSpawnBuffer;
    public bool LayoutClearings;
    public static MapGenerationRecipe From(TerrainGenerationSettings s)=>new MapGenerationRecipe{
        AlgorithmVersion=CurrentAlgorithmVersion,Seed=s.seed,Players=s.players,CliffCells=s.cliffCells,RampWidth=s.rampWidth,
        MaximumRampCells=s.maximumRampCells,MountainMinimumClusterCells=s.mountainMinimumClusterCells,BattlefieldRadius=s.battlefieldRadius,
        ProtectedRadius=s.protectedRadius,LandscapeHeight=s.landscapeHeight,DetailAmplitude=s.detailAmplitude,Wavelength=s.wavelength,
        LevelStep=s.levelStep,SmallIslandFillBudget=s.smallIslandFillBudget,MountainWavelength=s.mountainWavelength,
        MountainThreshold=s.mountainThreshold,MountainDensity=s.mountainDensity,MountainSpawnBuffer=s.mountainSpawnBuffer,LayoutClearings=s.layoutClearings};
    public TerrainGenerationSettings Settings()=>new TerrainGenerationSettings{
        seed=Seed,players=Players,cliffCells=CliffCells,rampWidth=RampWidth,maximumRampCells=MaximumRampCells,
        mountainMinimumClusterCells=MountainMinimumClusterCells,battlefieldRadius=BattlefieldRadius,protectedRadius=ProtectedRadius,
        landscapeHeight=LandscapeHeight,detailAmplitude=DetailAmplitude,wavelength=Wavelength,levelStep=LevelStep,
        smallIslandFillBudget=SmallIslandFillBudget,mountainWavelength=MountainWavelength,mountainThreshold=MountainThreshold,
        mountainDensity=MountainDensity,mountainSpawnBuffer=MountainSpawnBuffer,layoutClearings=LayoutClearings};
}

// Future transport writes this same payload on clients. Hash validates the terrain
// payload, not cross-platform determinism of Unity's floating-point noise.
public static class MapTerrainSnapshot
{
    public static ulong Hash(GridComponent grid,GridTerrain[] cells,GridSpawnCell[] spawns)
    {
        ulong h=14695981039346656037UL;
        void Add(uint v){for(int b=0;b<4;b++){h^=(byte)(v>>(b*8));h=unchecked(h*1099511628211UL);}}
        Add((uint)grid.width);Add((uint)grid.height);Add(math.asuint(grid.cellsize));
        Add(math.asuint(grid.origin.x));Add(math.asuint(grid.origin.y));Add(math.asuint(grid.origin.z));
        foreach(var c in cells){Add((uint)c.heightLevel);Add((uint)c.RampId);Add((uint)c.RampDirection);
            Add((uint)c.CornerMask);Add(c.UsesVertexCorners?1u:0);Add(c.walkable?1u:0);Add(c.isMountain?1u:0);Add(c.isCliff?1u:0);Add(c.RampInfluence?1u:0);}
        foreach(var s in spawns){Add((uint)s.playerId);Add((uint)s.cell.x);Add((uint)s.cell.y);}
        return h;
    }
    public static void Apply(EntityManager em,Entity entity,GridTerrain[] cells,GridSpawnCell[] spawns,uint revision,ulong expectedHash)
    {
        var grid=em.GetComponentData<GridComponent>(entity);
        if(cells==null||spawns==null||cells.Length!=checked(grid.width*grid.height))throw new ArgumentException("Terrain snapshot size mismatch");
        foreach(var spawn in spawns)if(spawn.cell.x<0||spawn.cell.y<0||spawn.cell.x>=grid.width||spawn.cell.y>=grid.height)throw new ArgumentException("Invalid spawn in terrain snapshot");
        var status=em.GetComponentData<MapBuildState>(entity);
        if(revision<status.Revision)throw new InvalidOperationException("Stale map revision");
        ulong hash=Hash(grid,cells,spawns);
        if(hash!=expectedHash)throw new InvalidOperationException("Terrain snapshot hash mismatch");
        if(status.Phase!=MapBuildPhase.Requested&&status.Phase!=MapBuildPhase.Failed){
            if(revision==status.Revision){
                if(hash!=status.TerrainHash)throw new InvalidOperationException("Conflicting terrain for the same revision");
                return; // Retransmission must not reset navigation or live blockers.
            }
            if(status.Phase==MapBuildPhase.NavigationReady||status.Phase==MapBuildPhase.GameplayReady)
                throw new InvalidOperationException("Reset the match before replacing an active map");
        }
        // Validate everything before publishing the buffers/phase to downstream systems.
        var terrain=em.GetBuffer<GridTerrain>(entity);terrain.ResizeUninitialized(cells.Length);
        for(int i=0;i<cells.Length;i++)terrain[i]=cells[i];
        var anchors=em.GetBuffer<GridSpawnCell>(entity);anchors.Clear();foreach(var spawn in spawns)anchors.Add(spawn);
        grid.initialized=false;grid.islandGeneration=uint.MaxValue;grid.isDirty=false;
        em.SetComponentData(entity,grid);
        em.SetComponentData(entity,new MapBuildState{Phase=MapBuildPhase.TerrainReady,Revision=revision,TerrainHash=hash});
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation|WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class MapTerrainGenerationSystem : SystemBase
{
    protected override void OnCreate(){RequireForUpdate<MapGenerationRecipe>();}
    protected override void OnUpdate()
    {
        using var query=EntityManager.CreateEntityQuery(typeof(MapGenerationRecipe),typeof(MapBuildState),typeof(GridComponent));
        using var entities=query.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities){
            var state=EntityManager.GetComponentData<MapBuildState>(entity);if(state.Phase!=MapBuildPhase.Requested)continue;
            try{
                var recipe=EntityManager.GetComponentData<MapGenerationRecipe>(entity);
                if(recipe.AlgorithmVersion!=MapGenerationRecipe.CurrentAlgorithmVersion)throw new InvalidOperationException("Unsupported map algorithm version");
                using var probe=RuntimePerformanceProbe.Measure("terrainGenerate");
                var grid=EntityManager.GetComponentData<GridComponent>(entity);
                var map=TerrainGeneration.Generate(grid,recipe.Settings());
                var spawns=new GridSpawnCell[map.spawns.Length];for(int i=0;i<spawns.Length;i++)spawns[i]=new GridSpawnCell{playerId=i,cell=map.spawns[i]};
                MapTerrainSnapshot.Apply(EntityManager,entity,map.cells,spawns,state.Revision,MapTerrainSnapshot.Hash(grid,map.cells,spawns));
            }catch(Exception error){state.Phase=MapBuildPhase.Failed;state.Error=new FixedString512Bytes(error.Message.Length>400?error.Message.Substring(0,400):error.Message);EntityManager.SetComponentData(entity,state);Debug.LogException(error);}
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation|WorldSystemFilterFlags.ServerSimulation|WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PlayerBootstrapSystem))]
public partial class MapBuildProgressSystem : SystemBase
{
    protected override void OnUpdate()
    {
        using var query=EntityManager.CreateEntityQuery(typeof(MapBuildState),typeof(GridComponent));
        using var entities=query.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities){
            var state=EntityManager.GetComponentData<MapBuildState>(entity);var grid=EntityManager.GetComponentData<GridComponent>(entity);
            if(state.Phase==MapBuildPhase.TerrainReady&&grid.initialized&&!grid.isDirty&&grid.islandGeneration==grid.generation)state.Phase=MapBuildPhase.NavigationReady;
            using var bootstrap=EntityManager.CreateEntityQuery(typeof(PlayerBootstrapState));
            if(state.Phase==MapBuildPhase.NavigationReady&&bootstrap.CalculateEntityCount()==1&&bootstrap.GetSingleton<PlayerBootstrapState>().Phase==PlayerBootstrapPhase.Ready)state.Phase=MapBuildPhase.GameplayReady;
            if(state.Phase!=EntityManager.GetComponentData<MapBuildState>(entity).Phase)EntityManager.SetComponentData(entity,state);
        }
    }
}

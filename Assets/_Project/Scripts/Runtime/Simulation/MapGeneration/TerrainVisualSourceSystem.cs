using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct TerrainSyncSettings : IComponentData
{
    public Entity Prefab;
    public int MountainTiers, VisualSeed, CapPixels;
    public float GroundRegionCells;
}

// One compiled authoritative map per World. Player views select records from this cache.
public sealed class TerrainVisualSource : IComponentData
{
    public Entity GridEntity;
    public GridComponent Grid;
    public uint Revision, Epoch;
    public ulong Hash;
    public TerrainSyncSettings Settings;
    public TerrainCellVisualData[] Cells;
    public float Rise, MountainRise;
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
public partial class TerrainVisualSourceSystem : SystemBase
{
    uint epoch;
    protected override void OnCreate() { RequireForUpdate<TerrainSyncSettings>(); }
    protected override void OnUpdate()
    {
        using var query=EntityManager.CreateEntityQuery(typeof(GridComponent),typeof(GridTerrain),typeof(MapBuildState),typeof(TerrainSyncSettings));
        if(query.CalculateEntityCount()!=1)return;
        EntityManager.CompleteAllTrackedJobs();
        var e=query.GetSingletonEntity();
        var status=EntityManager.GetComponentData<MapBuildState>(e);
        if(status.Phase==MapBuildPhase.Requested||status.Phase==MapBuildPhase.Failed)return;
        var grid=EntityManager.GetComponentData<GridComponent>(e);
        var settings=EntityManager.GetComponentData<TerrainSyncSettings>(e);
        if(EntityManager.HasComponent<TerrainVisualSource>(e)){
            var previous=EntityManager.GetComponentObject<TerrainVisualSource>(e);
            if(previous.Cells!=null&&previous.Revision==status.Revision&&previous.Hash==status.TerrainHash)return;
        }
        var buffer=EntityManager.GetBuffer<GridTerrain>(e,true);
        if(buffer.Length!=checked(grid.width*grid.height))return;
        var map=new GeneratedTerrain{grid=grid,cells=new GridTerrain[buffer.Length],spawns=Array.Empty<int2>()};
        for(int i=0;i<buffer.Length;i++)map.cells[i]=buffer[i];
        TerrainRampCompiler.Compile(map);
        float rise=TerrainSurfaceData.CanonicalRise(grid.cellsize);
        float mountainRise=rise*math.max(1,(settings.CapPixels-96)/32f);
        var source=new TerrainVisualSource{GridEntity=e,Grid=grid,Revision=status.Revision,Hash=status.TerrainHash,
            Epoch=++epoch,Settings=settings,Rise=rise,MountainRise=mountainRise,
            Cells=TerrainCellVisualCompiler.Compile(map,rise,mountainRise,settings.MountainTiers,settings.VisualSeed)};
        if(EntityManager.HasComponent<TerrainVisualSource>(e))EntityManager.SetComponentData(e,source);
        else EntityManager.AddComponentObject(e,source);
    }
}

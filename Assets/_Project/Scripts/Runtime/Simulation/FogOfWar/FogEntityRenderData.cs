using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public enum FogRenderKind : byte { Unit, Building, Resource, Prop, Projectile }

public enum FogAbilityKind : byte { Gather, Build, Attack }

public struct FogHealthSnapshot
{
    public float Current;
    // Null when the registered Based already provides the effective maximum.
    public float? MaxOverride;
}

public struct FogResourceSnapshot
{
    public int Amount;
}

public struct FogActiveAbilitySnapshot
{
    public FogAbilityKind Kind;
    public FixedString64Bytes Id;
    // WorkerGatherState or BuilderState according to Kind. Attack has state 0.
    public byte State;
}

public struct FogActiveJobSnapshot
{
    public FixedString64Bytes Id;
    public ProductionKind Kind;
    public float RemainingWork;
}

// Local Entity plus last-observed render values. Networking is deferred.
public struct FogEntityRenderData
{
    public Entity Entity;
    // Existing registry IDs are strings. A missing registry source falls back to
    // the live local entity; presentation must capture its visuals while visible.
    public FixedString64Bytes? GameRegId;
    public Entity? SourceEntity;
    public FogRenderKind Kind;
    public LocalTransform Transform;
    public float4x4 PostTransform;
    public int OwnerId;
    public bool IsStatic;
    public bool IsVisible;
    public FogHealthSnapshot? Health;
    public ConstructionPhase ConstructionPhase;
    public float ConstructionProgress;
    public FogResourceSnapshot? Resource;
    public FogActiveAbilitySnapshot? ActiveAbility;
    public FogActiveJobSnapshot? ActiveJob;
    public int2 FootprintMin, FootprintMax;
}

// One reusable output per consumer. Collect replaces its contents; the consumer owns disposal.
public sealed class FogOfWarFrame : IDisposable
{
    public NativeList<int> VisibleCells;
    public NativeList<FogEntityRenderData> Entities;
    public NativeList<FogTerrainCellData> TerrainCells;
    public bool IsValid;
    public int PlayerId;
    public int Width, Height;
    public float CellSize;
    public float3 Origin;
    // Immutable map geometry datum, shared with TerrainSurfaceData's full-map
    // normalization. Unknown cells must not introduce a different height origin.
    public int TerrainBaseLevel;

    public FogOfWarFrame(Allocator allocator = Allocator.Persistent)
    {
        VisibleCells = new NativeList<int>(allocator);
        Entities = new NativeList<FogEntityRenderData>(allocator);
        TerrainCells = new NativeList<FogTerrainCellData>(allocator);
    }

    public void Dispose()
    {
        if (VisibleCells.IsCreated) VisibleCells.Dispose();
        if (Entities.IsCreated) Entities.Dispose();
        if (TerrainCells.IsCreated) TerrainCells.Dispose();
        IsValid = false;
    }
}

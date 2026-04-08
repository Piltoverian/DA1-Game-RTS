using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(IntegrationFieldSystem))]
public partial struct MovementAgentPathRequestSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridComponent>();
        state.RequireForUpdate<FlowFieldCache>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<GridComponent>();
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        var job = new IdentifyPathRequestJob
        {
            Grid = grid,
            Ecb = ecb.AsParallelWriter(),
            FlowFieldLookup = SystemAPI.GetComponentLookup<FlowField>(true)
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
        state.Dependency.Complete();

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    public partial struct IdentifyPathRequestJob : IJobEntity
    {
        [ReadOnly] public GridComponent Grid;
        public EntityCommandBuffer.ParallelWriter Ecb;
        [ReadOnly] public ComponentLookup<FlowField> FlowFieldLookup;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref MovementAgentComponent move)
        {
            if (!move.hastarget) return;

            int2 targetCell = GridHelper.WorldToGrid(move.currentworldtarget, Grid);

            if (move.FieldEntity == Entity.Null)
            {
                Ecb.AddComponent(chunkIndex, entity, new TargetChangeRequest { newWorldTarget = move.currentworldtarget });
                return;
            }

            if (FlowFieldLookup.TryGetComponent(move.FieldEntity, out var currentField))
            {
                if (math.any(targetCell != currentField.targetcell))
                {
                    Ecb.AddComponent(chunkIndex, entity, new TargetChangeRequest { newWorldTarget = move.currentworldtarget });
                }
            }
            else
            {
                Ecb.AddComponent(chunkIndex, entity, new TargetChangeRequest { newWorldTarget = move.currentworldtarget });
            }
        }
    }
}

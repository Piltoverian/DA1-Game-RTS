using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct MatchSession : IComponentData
{
    [GhostField] public ulong Id;
}

public struct MatchPlayerRoster : IBufferElementData
{
    [GhostField] public int PlayerId;
    [GhostField] public int TeamId;
    [GhostField] public FixedString64Bytes CivId;
}

public class MatchGhostAuthoring : MonoBehaviour
{
    class Baker : Baker<MatchGhostAuthoring>
    {
        public override void Bake(MatchGhostAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<MatchSession>(entity);
            AddComponent(entity, new MatchState { Phase = MatchPhase.Preparing });
            AddComponent<MatchTimeComponent>(entity);
            AddBuffer<MatchPlayerRoster>(entity);
        }
    }
}

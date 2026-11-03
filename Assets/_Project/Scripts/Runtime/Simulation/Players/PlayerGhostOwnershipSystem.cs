using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
[UpdateBefore(typeof(GameplayGhostRelevancySystem))]
public partial class PlayerGhostOwnershipSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var owners = new Dictionary<int, (int NetworkId, PlayerConnection Player)>();
        using var connectionsQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<NetworkId>(), ComponentType.ReadOnly<PlayerConnection>() },
            None = new[] { ComponentType.ReadOnly<NetworkStreamRequestDisconnect>() }
        });
        using var connections = connectionsQuery.ToEntityArray(Allocator.Temp);
        foreach (var connection in connections)
        {
            var player = EntityManager.GetComponentData<PlayerConnection>(connection);
            if (PlayerSessionSlots.TryGetSlot(EntityManager, player.PlayerId, out _)
                && !PlayerSessionSlots.IsCurrent(EntityManager, connection, player)) continue;
            owners[player.PlayerId] = (EntityManager.GetComponentData<NetworkId>(connection).Value, player);
        }
        using var query = EntityManager.CreateEntityQuery(typeof(GhostInstance), typeof(GhostOwner));
        using var ghosts = query.ToEntityArray(Allocator.Temp);
        foreach (var ghost in ghosts)
        {
            bool context = EntityManager.HasComponent<PlayerContext>(ghost);
            if (!context && !EntityManager.HasComponent<EntityOwner>(ghost)) continue;
            int playerId = context ? EntityManager.GetComponentData<PlayerContext>(ghost).PlayerId
                : EntityManager.GetComponentData<EntityOwner>(ghost).PlayerID;
            bool assigned = owners.TryGetValue(playerId, out var owner);
            int network = assigned ? owner.NetworkId : 0;
            if (EntityManager.GetComponentData<GhostOwner>(ghost).NetworkId != network)
                EntityManager.SetComponentData(ghost, new GhostOwner { NetworkId = network });
            if (context && EntityManager.HasComponent<PlayerContextSession>(ghost))
            {
                bool persistent = PlayerSessionSlots.TryGetSlot(EntityManager, playerId, out var slot);
                EntityManager.SetComponentData(ghost, new PlayerContextSession
                { MatchSessionId = persistent ? slot.MatchSessionId : assigned ? owner.Player.MatchSessionId : 0,
                    JoinGeneration = persistent ? slot.JoinGeneration : assigned ? owner.Player.JoinGeneration : 0 });
            }
        }
    }
}

using Unity.Entities;
using Unity.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "ShareMapEffect", menuName = "ScriptableObjects/Tech Effects/Share Map")]
public sealed class ShareMapEffect : TechEffect
{
    public override void Conduct(EntityManager entityManager, Entity player)
    {
        var context = entityManager.GetComponentData<PlayerContext>(player);
        context.FogMode = FogMode.Team;
        entityManager.SetComponentData(player, context);
        using var fogQuery = entityManager.CreateEntityQuery(typeof(FogOfWarSpatialSystemData));
        if (fogQuery.IsEmptyIgnoreFilter) return;
        using var playersQuery = entityManager.CreateEntityQuery(typeof(PlayerContext));
        using var players = playersQuery.ToComponentDataArray<PlayerContext>(Allocator.Temp);
        var fog = fogQuery.GetSingleton<FogOfWarSpatialSystemData>();
        fog.ImportTeammateMap(context, players);
        fogQuery.SetSingleton(fog);
    }
}

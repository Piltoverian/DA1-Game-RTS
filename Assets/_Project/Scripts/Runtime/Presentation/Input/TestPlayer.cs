using Unity.Entities;
using UnityEngine;

// Local identity for offline testing. Match players are created by map bootstrap.
[DisallowMultipleComponent]
public class TestPlayer : MonoBehaviour
{
    [Min(0)] public int playerId = 1;

    class Baker : Baker<TestPlayer>
    {
        public override void Bake(TestPlayer authoring)
        {
            var context = GetComponent<PlayerContextAuthoring>();
            if (context != null) DependsOn(context);
            AddComponent(GetEntity(TransformUsageFlags.None), new LocalTestPlayer
            {
                PlayerId = context != null ? context.playerId : authoring.playerId
            });
        }
    }

    public bool TryGetPlayerId(out int id)
    {
        id = -1;
        var world = World.DefaultGameObjectInjectionWorld;
        if (!isActiveAndEnabled || playerId < 0 || world == null || !world.IsCreated)
            return false;
        using var bootstrap = world.EntityManager.CreateEntityQuery(typeof(PlayerBootstrapState));
        if (!bootstrap.IsEmptyIgnoreFilter &&
            (bootstrap.CalculateEntityCount() != 1 || bootstrap.GetSingleton<PlayerBootstrapState>().Phase != PlayerBootstrapPhase.Ready))
            return false;
        if (PlayerContextHelper.GetContextData(world.EntityManager, playerId, out _) != FunctionResult.Success)
            return false;
        id = playerId;
        return true;
    }
}

public struct LocalTestPlayer : IComponentData
{
    public int PlayerId;
}

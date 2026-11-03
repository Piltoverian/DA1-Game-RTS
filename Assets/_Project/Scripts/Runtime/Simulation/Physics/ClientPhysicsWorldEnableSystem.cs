using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[CreateAfter(typeof(PredictedPhysicsConfigSystem))]
[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateBefore(typeof(PredictedPhysicsConfigSystem))]
public partial class ClientPhysicsWorldEnableSystem : SystemBase
{
    protected override void OnCreate()
    {
        var predictedPhysicsConfig = World.GetExistingSystemManaged<PredictedPhysicsConfigSystem>();
        if (predictedPhysicsConfig != null)
        {
            predictedPhysicsConfig.Enabled = false;
        }
    }

    protected override void OnUpdate()
    {
        var initGroup = World.GetExistingSystemManaged<InitializationSystemGroup>();
        var predictedPhysicsConfig = World.GetExistingSystemManaged<PredictedPhysicsConfigSystem>();
        if (predictedPhysicsConfig != null)
        {
            predictedPhysicsConfig.Enabled = false;
            initGroup?.RemoveSystemFromUpdateList(predictedPhysicsConfig);
        }

        Enabled = false;
        initGroup?.RemoveSystemFromUpdateList(this);
    }
}

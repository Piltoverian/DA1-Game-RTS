using Unity.Entities;
using UnityEngine;

public class MoveOnlyCommandReceiver : CommandReceiver
{
    public override void ReceiveCommand(ICommand command)
    {
        OnTargetCommandReceive(command);
    }

    private void OnTargetCommandReceive(ICommand command)
    {
        if (command is TargetCommand targetCommand)
        {
            Debug.Log("CommandExecuted");
            Vector2 targetpos = targetCommand.GetTargetPos();

            Vector3 world = Utility.ConvertCamToWorld(targetpos);

            var worldECS = World.DefaultGameObjectInjectionWorld;
            var em = worldECS.EntityManager;

            Entity gridEntity =
                em.CreateEntityQuery(typeof(GridComponent))
                .GetSingletonEntity();

            if (!em.HasComponent<Target>(gridEntity))
            {
                em.AddComponentData(gridEntity, new Target
                {
                    worldpos = world
                });
            }
            else
            {
                em.SetComponentData(gridEntity, new Target
                {
                    worldpos = world
                });
            }

    

            var handle = worldECS.GetExistingSystem<IntergrationFieldSystem>();
            worldECS.Unmanaged.ResolveSystemStateRef(handle).Enabled = true;
        }
    }
}

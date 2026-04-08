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
            
        }
    }
}

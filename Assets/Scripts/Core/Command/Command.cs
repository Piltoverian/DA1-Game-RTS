using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Unity.VisualScripting;
using UnityEngine.AI;

public interface ICommand
{
    public void Execute();
}

public class TargetCommand : ICommand
{
    private readonly Vector2 targetpos;
    private readonly SelectableObject unit;
    public void Execute()
    {
        if(unit==null)
        {
            Debug.LogWarning("Command has no unit to execute on.");
            return;
        }
        unit.TryGetComponent(out CommandReceiver receiver);
        if (receiver != null)
        {
            receiver.ReceiveCommand(this);
        }
    }

    public TargetCommand(SelectableObject unit,Vector2 targetpos)
    {
        this.unit = unit;
        this.targetpos = targetpos;
    }

    public SelectableObject GetUnit()
    {
        return unit;
    }

    public Vector2 GetTargetPos()
        {
        return targetpos;
    }
}


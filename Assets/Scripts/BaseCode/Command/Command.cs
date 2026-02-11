using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Unity.VisualScripting;

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
        if (unit is DragSelectableObject) { 
            (unit as DragSelectableObject).OnMoveTo(targetpos);
        }
    }

    public TargetCommand(SelectableObject unit,Vector2 targetpos)
    {
        this.unit = unit;
        this.targetpos = targetpos;
    }
}

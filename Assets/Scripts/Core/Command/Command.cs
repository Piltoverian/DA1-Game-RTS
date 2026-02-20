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
        bool executed = false;
        Ray targetCast=Camera.main.ScreenPointToRay(targetpos);
        GameObject targetobj = null;

        if (Physics.Raycast(targetCast, out RaycastHit hit,Mathf.Infinity,LayerMask.GetMask("Selectable")))
        {
            targetobj = hit.collider.gameObject;
        }

        if (targetobj != null)
        {
            if (targetobj.GetComponent<ResourceNode>() != null)
            {

                if (unit.GetComponent<ResourceGatherable>() != null)
                {
                    unit.GetComponent<ResourceGatherable>().ChangeNode(targetobj.GetComponent<ResourceNode>());
                    Debug.Log("Current Node Is: " + unit.GetComponent<ResourceGatherable>().GetCurrentResource());
                    Debug.Log("Current Type Is: " + unit.GetComponent<ResourceGatherable>().GetCurrentResourceNode());
                    Debug.Log("Target to movePos: " + unit.GetComponent<NavMeshAgent>().destination);
                    unit.GetComponent<ResourceGatherable>().MovingToNode();
                    executed = true;
                }
            }
        }
        if (targetobj == null||executed==false)
        {
            MoveAbilityComponent moveAbility = unit.GetComponent<MoveAbilityComponent>();
            if (moveAbility != null)
            {
                moveAbility.OnMoveToScreen(targetpos);
            }
        }
    }

    public TargetCommand(SelectableObject unit,Vector2 targetpos)
    {
        this.unit = unit;
        this.targetpos = targetpos;
    }
}

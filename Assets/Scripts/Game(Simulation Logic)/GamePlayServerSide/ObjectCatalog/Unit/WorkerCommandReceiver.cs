//using UnityEngine;

//public class WorkerCommandReceiver : CommandReceiver
//{
//    public void OnTargetCommandReceive(ICommand command)
//    {
//        if (command is TargetCommand targetCommand)
//        {
//            Vector2 targetpos = targetCommand.GetTargetPos();
//            SelectableObject unit = targetCommand.GetUnit();
//            bool executed = false;
//            Ray targetCast = Camera.main.ScreenPointToRay(targetpos);
//            GameObject targetobj = null;

//            if (Physics.Raycast(targetCast, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Selectable")))
//            {
//                targetobj = hit.collider.gameObject;
//            }

//            if (targetobj != null)
//            {
//                if (targetobj.GetComponent<ResourceNode>() != null)
//                {

//                    if (unit.GetComponent<ResourceGatherable>() != null)
//                    {
//                        var gather = unit.GetComponent<ResourceGatherable>();
//                        SetAbility(gather);
//                        gather.ChangeNode(targetobj.GetComponent<ResourceNode>());
//                        gather.MovingToNode();
//                        executed=true;
//                    }
//                }

//                if (targetobj.GetComponent<Storage>() != null)
//                {
//                    if (unit.GetComponent<ResourceGatherable>() != null)
//                    {
//                        var gather = unit.GetComponent<ResourceGatherable>();
//                        SetAbility(gather);
//                        gather.ChangeTargetStorage(targetobj.GetComponent<Storage>());
//                        gather.ReturnToBase();
//                        executed = true;
//                    }
//                }
//            }
//            if (targetobj == null || executed == false)
//            {
//                //forceMoveCommand -> force all other abilitiesAI to return to idle state and execute move command
//                MoveAbilityComponent moveAbility = unit.GetComponent<MoveAbilityComponent>();
//                if (moveAbility != null)
//                {
//                    ClearAbility();
//                    moveAbility.OnMoveToScreen(targetpos);
//                }
//            }
//        }
//        else
//        {
//            Debug.LogError("Received command is not of type TargetCommand.");
//        }
//    }

//    public override void ReceiveCommand(ICommand command)
//    {
//            if (command != null)
//            {
//                switch (command.GetType().Name)
//                {
//                    case "TargetCommand":
//                        OnTargetCommandReceive(command);
//                        break;
//                    default:
//                        Debug.LogWarning("Unknown Command Type: " + command.GetType().Name);
//                        break;
//                }
//            }
//    }

//    void Start()
//    {
        
//    }
//    void Update()
//    {
        
//    }
//}

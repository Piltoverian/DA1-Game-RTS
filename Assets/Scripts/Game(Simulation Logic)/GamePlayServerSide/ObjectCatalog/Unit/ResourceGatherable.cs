//using NUnit.Framework;
//using System;
//using UnityEngine;
//using UnityEngine.AI;

//public class ResourceGatherable : MonoBehaviour, Ability
//{
//    #region ====== State Enum ======

//    enum GatheringState
//    {
//        Idle,
//        MovingToNode,
//        Gathering,
//        ReturningToBase
//    }

//    #endregion


//    #region ====== Serialized Fields ======

//    [SerializeField] private float carryingCapacity;
//    [SerializeField] private float GatheringSpeed;
//    [SerializeField] private float gatheringRange;

//    [SerializeField] private float currentlyCarryingAmount;
//    [SerializeField] private ResourceNode currentResourceNode;
//    [SerializeField] private ResourceType resourceType = ResourceType.None;
//    [SerializeField] private GatheringState gatheringState = GatheringState.Idle;
//    [SerializeField] private Storage targetStorage;

//    #endregion


//    #region ====== Public API ======

//    public void SetCurrentResourceNode(ResourceNode resourceNode)
//    {
//        currentResourceNode = resourceNode;
//    }

//    public ResourceType GetCurrentResource()
//    {
//        return resourceType;
//    }

//    public ResourceNode GetCurrentResourceNode()
//    {
//        return currentResourceNode;
//    }

//    public void ChangeTargetStorage(Storage newStorage)
//    {
//        targetStorage = newStorage;

//        if (gatheringState == GatheringState.ReturningToBase)
//        {
//            ReturnToBase();
//        }
//    }

//    #endregion


//    #region ====== Core State Logic ======

//    public void GatherResource()
//    {
//        if (currentlyCarryingAmount >= carryingCapacity)
//        {
//            currentlyCarryingAmount = carryingCapacity;
//            ReturnToBase();
//            return;
//        }
//        if (currentResourceNode != null && !currentResourceNode.IsDepleted())
//        {
//            gatheringState = GatheringState.Gathering;

//            float expectedGatherAmount =
//                Mathf.Min(carryingCapacity - currentlyCarryingAmount,
//                          GatheringSpeed * Time.fixedDeltaTime);

//            float gatheredAmount = currentResourceNode.Extract(expectedGatherAmount);

//            if (gatheredAmount <= 0)
//            {
//                ReturnToBase();
//                return;
//            }

//            currentlyCarryingAmount += gatheredAmount;

//            if (Mathf.Abs(carryingCapacity - currentlyCarryingAmount) < 0.01f)
//            {
//                currentlyCarryingAmount = carryingCapacity;
//            }

//            Debug.Log("Gathered " + gatheredAmount + " from " + currentResourceNode.GetNodeType());
//        }
//        else
//        {
//            ReturnToBase();
//        }
//    }

//    public void ChangeNode(ResourceNode newNode)
//    {
//        if (currentResourceNode == null)
//        {
//            currentResourceNode = newNode;
//            resourceType = newNode.GetNodeType();
//        }
//        else if (newNode.GetNodeType() != currentResourceNode.GetNodeType())
//        {
//            currentResourceNode = newNode;
//            currentlyCarryingAmount = 0;
//            resourceType = newNode.GetNodeType();
//            targetStorage = null;
//        }
//        else
//        {
//            currentResourceNode = newNode;
//        }

//        Debug.Log("NewResourceNodeIs: " + currentResourceNode);
//    }

//    public void ReturnToBase()
//    {
//        gatheringState = GatheringState.ReturningToBase;

//        if (targetStorage == null)
//        {
//            FindNearestStorage();

//            if (targetStorage == null)
//            {
//                ReturnToIdle();
//                return;
//            }
//        }

//        var move = GetComponent<MoveAbilityComponent>();
//        if (move != null)
//            move.OnMoveToWorld(targetStorage.transform.position);
//        else
//            Debug.LogError("MoveAbilityComponent is missing from the worker");
//    }

//    public void ReturnToIdle()
//    {
//        gatheringState = GatheringState.Idle;
//    }

//    #endregion


//    #region ====== Movement ======

//    public void MovingToNode()
//    {
//        if (currentResourceNode != null)
//        {
//            var move = GetComponent<MoveAbilityComponent>();
//            if (move != null)
//                move.OnMoveToWorld(currentResourceNode.transform.position);
//            else
//                Debug.LogError("MoveAbilityComponent is missing from the worker");

//            gatheringState = GatheringState.MovingToNode;
//        }
//        else
//        {
//            FindNearestSuitableNode();

//            if (currentResourceNode != null)
//            {
//                MovingToNode();
//                return;
//            }

//            ReturnToBase();
//        }
//    }

//    #endregion


//    #region ====== Searching ======

//    private void FindNearestStorage()
//    {
//        Storage[] storages = FindObjectsByType<Storage>(FindObjectsSortMode.None);

//        float minDistance = Mathf.Infinity;
//        Storage nearestStorage = null;

//        foreach (Storage storage in storages)
//        {
//            float distance = Vector3.Distance(transform.position, storage.transform.position);

//            if (distance < minDistance &&
//                storage.GetStorageTypes().Contains(resourceType))
//            {
//                minDistance = distance;
//                nearestStorage = storage;
//            }
//        }

//        targetStorage = nearestStorage;
//    }

//    public void FindNearestSuitableNode()
//    {
//        ResourceNode[] nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);

//        float minDistance = Mathf.Infinity;
//        ResourceNode nearestNode = null;

//        foreach (ResourceNode node in nodes)
//        {
//            if (node.GetNodeType() == resourceType)
//            {
//                float distance = Vector3.Distance(transform.position, node.transform.position);

//                if (distance < minDistance)
//                {
//                    minDistance = distance;
//                    nearestNode = node;
//                }
//            }
//        }

//        if (nearestNode != null)
//        {
//            ChangeNode(nearestNode);
//        }
//    }

//    #endregion


//    #region ====== Unity Events ======


//    NavMeshAgent agent;
//    MoveAbilityComponent move;

//    void Awake()
//    {
//        agent = GetComponent<NavMeshAgent>();
//        move = GetComponent<MoveAbilityComponent>();
//    }

//    private void FixedUpdate()
//    {
//        if (gatheringState == GatheringState.Gathering)
//        {
//            GatherResource();
//        }
//        if (gatheringState == GatheringState.MovingToNode)
//        {
//            if(currentResourceNode==null)
//            {
//                ReturnToBase();
//                return;
//            }
//            Collider nodecollider = currentResourceNode.GetComponent<Collider>();

//            float distance = Vector3.Distance(transform.position, nodecollider.ClosestPoint(transform.position));

//            if (!agent.pathPending &&
//                distance <= gatheringRange)
//            {
//                move.Stop();
//                gatheringState = GatheringState.Gathering;
//            }
//        }

//        if (gatheringState == GatheringState.ReturningToBase)
//        {
//            if (targetStorage == null)
//            {
//                ReturnToIdle();
//                return;
//            }
//            Collider storageCollider = targetStorage.GetComponent<Collider>();

//            float distance = Vector3.Distance(transform.position, storageCollider.ClosestPoint(transform.position));
       
//            if (!agent.pathPending &&
//                distance <= gatheringRange)
//            {
//                Debug.Log("Reached storage, depositing resource");
//                DepositResource();
//            }
//        }
//    }

//    private void DepositResource()
//    {
//        if (targetStorage == null) return;
//        if (resourceType == ResourceType.None)
//        {
//            ReturnToIdle();
//            return;
//        }
//        targetStorage.ReceiveResource(currentlyCarryingAmount, resourceType);

//        currentlyCarryingAmount = 0;
//        if (move != null)
//            move.Stop();
//        if (currentResourceNode != null)
//            MovingToNode();
//        else
//            ReturnToIdle();
//    }
//    public void OnCollisionStay(Collision other)
//    {
//        if (gatheringState == GatheringState.MovingToNode &&
//                 other.gameObject.GetComponent<ResourceNode>() == currentResourceNode)
//        {
//            gatheringState = GatheringState.Gathering;

//            var move = GetComponent<MoveAbilityComponent>();
//            if (move != null)
//                move.Stop();
//        }
//    }
//    #endregion
//}
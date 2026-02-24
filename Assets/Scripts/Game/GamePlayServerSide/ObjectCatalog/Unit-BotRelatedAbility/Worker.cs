using NUnit.Framework;
using UnityEngine;

public class ResourceGatherable : MonoBehaviour, Ability
{
    #region ====== State Enum ======

    enum GatheringState
    {
        Idle,
        MovingToNode,
        Gathering,
        ReturningToBase
    }

    #endregion


    #region ====== Serialized Fields ======

    [SerializeField] private float carryingCapacity;
    [SerializeField] private float GatheringSpeed;
    [SerializeField] private float gatheringRange;

    [SerializeField] private float currentlyCarryingAmount;
    [SerializeField] private ResourceNode currentResourceNode;
    [SerializeField] private ResourceType resourceType = ResourceType.None;
    [SerializeField] private GatheringState gatheringState = GatheringState.Idle;
    [SerializeField] private Storage targetStorage;

    #endregion


    #region ====== Public API ======

    public void SetCurrentResourceNode(ResourceNode resourceNode)
    {
        currentResourceNode = resourceNode;
    }

    public ResourceType GetCurrentResource()
    {
        return resourceType;
    }

    public ResourceNode GetCurrentResourceNode()
    {
        return currentResourceNode;
    }

    public void ChangeTargetStorage(Storage newStorage)
    {
        targetStorage = newStorage;

        if (gatheringState == GatheringState.ReturningToBase)
        {
            ReturnToBase();
        }
    }

    #endregion


    #region ====== Core State Logic ======

    public void GatherResource()
    {
        if (currentlyCarryingAmount >= carryingCapacity)
        {
            currentlyCarryingAmount = carryingCapacity;
            ReturnToBase();
            return;
        }
        if (currentResourceNode != null && !currentResourceNode.IsDepleted())
        {
            gatheringState = GatheringState.Gathering;

            float expectedGatherAmount =
                Mathf.Min(carryingCapacity - currentlyCarryingAmount,
                          GatheringSpeed * Time.fixedDeltaTime);

            float gatheredAmount = currentResourceNode.Extract(expectedGatherAmount);

            if (gatheredAmount <= 0)
            {
                ReturnToBase();
                return;
            }

            currentlyCarryingAmount += gatheredAmount;

            if (Mathf.Abs(carryingCapacity - currentlyCarryingAmount) < 0.01f)
            {
                currentlyCarryingAmount = carryingCapacity;
            }

            Debug.Log("Gathered " + gatheredAmount + " from " + currentResourceNode.GetNodeType());
        }
        else
        {
            ReturnToBase();
        }
    }

    public void ChangeNode(ResourceNode newNode)
    {
        if (currentResourceNode == null)
        {
            currentResourceNode = newNode;
            resourceType = newNode.GetNodeType();
        }
        else if (newNode.GetNodeType() != currentResourceNode.GetNodeType())
        {
            currentResourceNode = newNode;
            currentlyCarryingAmount = 0;
            resourceType = newNode.GetNodeType();
            targetStorage = null;
        }
        else
        {
            currentResourceNode = newNode;
        }

        Debug.Log("NewResourceNodeIs: " + currentResourceNode);
    }

    public void ReturnToBase()
    {
        gatheringState = GatheringState.ReturningToBase;

        if (targetStorage == null)
        {
            FindNearestStorage();

            if (targetStorage == null)
            {
                ReturnToIdle();
                return;
            }
        }

        var move = GetComponent<MoveAbilityComponent>();
        if (move != null)
            move.OnMoveToWorld(targetStorage.transform.position);
        else
            Debug.LogError("MoveAbilityComponent is missing from the worker");

        targetStorage = null;
    }

    public void ReturnToIdle()
    {
        gatheringState = GatheringState.Idle;
    }

    #endregion


    #region ====== Movement ======

    public void MovingToNode()
    {
        if (currentResourceNode != null)
        {
            var move = GetComponent<MoveAbilityComponent>();
            if (move != null)
                move.OnMoveToWorld(currentResourceNode.transform.position);
            else
                Debug.LogError("MoveAbilityComponent is missing from the worker");

            gatheringState = GatheringState.MovingToNode;
        }
        else
        {
            FindNearestSuitableNode();

            if (currentResourceNode != null)
            {
                MovingToNode();
                return;
            }

            ReturnToBase();
        }
    }

    #endregion


    #region ====== Searching ======

    private void FindNearestStorage()
    {
        Storage[] storages = FindObjectsByType<Storage>(FindObjectsSortMode.None);

        float minDistance = Mathf.Infinity;
        Storage nearestStorage = null;

        foreach (Storage storage in storages)
        {
            float distance = Vector3.Distance(transform.position, storage.transform.position);

            if (distance < minDistance &&
                storage.GetStorageTypes().Contains(resourceType))
            {
                minDistance = distance;
                nearestStorage = storage;
            }
        }

        targetStorage = nearestStorage;
    }

    public void FindNearestSuitableNode()
    {
        ResourceNode[] nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);

        float minDistance = Mathf.Infinity;
        ResourceNode nearestNode = null;

        foreach (ResourceNode node in nodes)
        {
            if (node.GetNodeType() == resourceType)
            {
                float distance = Vector3.Distance(transform.position, node.transform.position);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestNode = node;
                }
            }
        }

        if (nearestNode != null)
        {
            ChangeNode(nearestNode);
        }
    }

    #endregion


    #region ====== Unity Events ======

    private void FixedUpdate()
    {
        if (gatheringState == GatheringState.Gathering)
        {
            GatherResource();
        }
    }

    public void OnCollisionEnter(Collision other)
    {
        if (gatheringState == GatheringState.ReturningToBase &&
            other.gameObject.GetComponent<Storage>())
        {
            Storage storage = other.gameObject.GetComponent<Storage>();

            if (storage.GetStorageTypes().Contains(resourceType))
            {
                storage.ReceiveResource(currentlyCarryingAmount, resourceType);
                currentlyCarryingAmount = 0;
                if (currentResourceNode != null)
                    MovingToNode();
                else
                    ReturnToIdle();
                var move = GetComponent<MoveAbilityComponent>();
                if (move != null)
                    move.Stop();
            }
        }
        else if (gatheringState == GatheringState.MovingToNode &&
                 other.gameObject.GetComponent<ResourceNode>() == currentResourceNode)
        {
            gatheringState = GatheringState.Gathering;

            var move = GetComponent<MoveAbilityComponent>();
            if (move != null)
                move.Stop();
        }
    }

    public void OnTriggerExit(Collider collision)
    {
        if (gatheringState == GatheringState.Gathering && collision.GetComponent<ResourceNode>() == currentResourceNode)
        {
            MovingToNode();
        } 
    }
    #endregion
}
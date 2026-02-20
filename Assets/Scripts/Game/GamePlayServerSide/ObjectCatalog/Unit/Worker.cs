using NUnit.Framework;
using UnityEngine;

public class ResourceGatherable : MonoBehaviour, Ability
{
    enum GatheringState
    {
        Idle,
        MovingToNode,
        Gathering,
        ReturningToBase
    }
    [SerializeField] private float carryingCapacity;
    [SerializeField] private float GatheringSpeed; // amount of resource gathered per second

    [SerializeField]private float currentlyCarryingAmount;
    [SerializeField]private ResourceNode currentResourceNode;
    [SerializeField]private ResourceType resourceType = ResourceType.None;
    [SerializeField]private GatheringState gatheringState=GatheringState.Idle;
    [SerializeField]private Storage targetStorage;
    public void SetCurrentResourceNode(ResourceNode resourceNode)
    {
        currentResourceNode = resourceNode;
    }

    public void GatherResource()
    {
        if (currentlyCarryingAmount >= carryingCapacity)
        {
            currentlyCarryingAmount = carryingCapacity;
            ReturnToBase();
            return;
        }
        if (currentResourceNode != null)
        {
            gatheringState = GatheringState.Gathering;
            float expectedGatheramount = 0;
            if (carryingCapacity - currentlyCarryingAmount > GatheringSpeed * Time.fixedDeltaTime)
            {
                expectedGatheramount = GatheringSpeed * Time.fixedDeltaTime;
            }
            else
            {
                expectedGatheramount = carryingCapacity - currentlyCarryingAmount;
            }
            
            float gatheredAmount = currentResourceNode.Extract(expectedGatheramount);
            if (gatheredAmount <= 0)
            {
                ReturnToBase();
                return;
            }
            currentlyCarryingAmount += gatheredAmount;
            Debug.Log("Gathered " + gatheredAmount + " from " + currentResourceNode.GetNodeType());
        }
        else
        {
            ReturnToBase();
        }
    }

    public ResourceType GetCurrentResource()
    {
        return resourceType;
    }

    public void ChangeNode(ResourceNode newNode)
    {
        if (currentResourceNode == null)
        {
            currentResourceNode = newNode;
            if (resourceType != newNode.GetNodeType())
                resourceType = newNode.GetNodeType();
        }
        else if (newNode.GetNodeType() != currentResourceNode.GetNodeType())
        {
            currentResourceNode = newNode;
            currentlyCarryingAmount = 0;
            resourceType = newNode.GetNodeType();
            targetStorage = null;// reset carrying amount when changing resource type
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
            if (GetComponent<MoveAbilityComponent>() != null)
            {
                GetComponent<MoveAbilityComponent>().OnMoveToWorld(targetStorage.transform.position);
               
            }
            else Debug.LogError("MoveAbilityComponent is missing from the worker");
        }
        else
        {
            if(GetComponent<MoveAbilityComponent>()!=null)
            GetComponent<MoveAbilityComponent>().OnMoveToWorld(targetStorage.transform.position);
            else Debug.LogError("MoveAbilityComponent is missing from the worker");
        }
       
    }

    private void FindNearestStorage()
    {
        Storage[] storages = FindObjectsByType<Storage>(FindObjectsSortMode.None);
        float minDistance = Mathf.Infinity;
        Storage nearestStorage = null;
        foreach (Storage storage in storages)
        {
            float distance = Vector3.Distance(transform.position, storage.transform.position);
            if (distance < minDistance&&storage.GetStorageTypes().Contains(resourceType))
            {
                minDistance = distance;
                nearestStorage = storage;
            }
        }
        targetStorage = nearestStorage;
    }

    public void MovingToNode()
    {
        if (currentResourceNode != null)
        {
            if (GetComponent<MoveAbilityComponent>() != null)
                GetComponent<MoveAbilityComponent>().OnMoveToWorld(currentResourceNode.transform.position);
            else Debug.LogError("MoveAbilityComponent is missing from the worker");
            gatheringState = GatheringState.MovingToNode;
        }
        else
        {
            Debug.Log("ResourceNullMovingToNode");
            ReturnToIdle();
        }
    }

    private void FixedUpdate()
    {
        if (gatheringState == GatheringState.Gathering)
        {
            GatherResource();
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (gatheringState == GatheringState.ReturningToBase && other.GetComponent<Storage>())
        {
            other.GetComponent<Storage>();
            if(other.GetComponent<Storage>().GetStorageTypes().Contains(resourceType))
            {
                other.GetComponent<Storage>().ReceiveResource(currentlyCarryingAmount,resourceType);
                currentlyCarryingAmount = 0;
                if (GetComponent<MoveAbilityComponent>() != null)
                {
                    GetComponent<MoveAbilityComponent>().Stop();
                }
                if (currentResourceNode != null)
                {
                    Debug.Log("Returning to node after delivering resource");
                    MovingToNode();
                }
                else
                {
                    ReturnToIdle();
                }
            }
            
        }
        else if (gatheringState == GatheringState.MovingToNode && other.GetComponent<ResourceNode>() == currentResourceNode)
        {
            gatheringState = GatheringState.Gathering;
            if (GetComponent<MoveAbilityComponent>() != null)
            {
                GetComponent<MoveAbilityComponent>().Stop();
            }
        }
    }

    public void OnTriggerExit(Collider collision)
    {
        if (gatheringState == GatheringState.Gathering && collision.GetComponent<ResourceNode>() == currentResourceNode)
        {
            MovingToNode();
        }
    }
    public void ReturnToIdle()
    {
       gatheringState = GatheringState.Idle;
    }
    public ResourceNode GetCurrentResourceNode()
    {
        return currentResourceNode;
    }
}

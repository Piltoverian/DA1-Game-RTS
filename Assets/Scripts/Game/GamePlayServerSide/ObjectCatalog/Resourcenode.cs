using UnityEngine;

public enum ResourceType
{
    None=0,
    Oil=1,
    Iron=2,
    Uranium=3,
    Food=4
}
public class ResourceNode : SelectableObject
{
    [SerializeField]protected ResourceType nodetype;
    [SerializeField]protected float amount;
    [SerializeField] Collider threshholdCollider;
    [SerializeField] Collider collider;
    public ResourceType GetNodeType()
    {
        return nodetype;
    }

    public void OnDepleted()
    {
        //playsound,...
        Destroy(gameObject);
    }

    public float Extract(float ExpectedTakenAmount)
    {
        float takenamount = Mathf.Min(amount, ExpectedTakenAmount);
        this.amount -= takenamount;
        if (this.amount <= 0)
        {
            OnDepleted();
        }
        return takenamount;
    }
}

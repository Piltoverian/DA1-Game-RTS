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
    [SerializeField] private bool isDepleted = false;
    public ResourceType GetNodeType()
    {
        return nodetype;
    }

    public void OnDepleted()
    {
        isDepleted = true;
        //playsound,...
        Destroy(gameObject);
    }

    public bool IsDepleted()
    {
        return isDepleted;
    }


    public float Extract(float ExpectedTakenAmount)
    {
        float takenamount = Mathf.Min(amount, ExpectedTakenAmount);
        this.amount -= takenamount;
        if (Mathf.Abs(this.amount) <= 0.01)
        {
            OnDepleted();
        }
        return takenamount;
    }
}

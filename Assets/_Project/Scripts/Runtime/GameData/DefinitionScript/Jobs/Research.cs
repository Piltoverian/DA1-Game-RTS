using UnityEngine;

public class Research : Job
{
    [SerializeField] private TechDefinition techDefinition;
    private void OnValidate()
    {
        if (techDefinition != null)
        {
            techDefinition.IDs= new Unity.Collections.FixedString64Bytes(Id);
        }
    }
    public TechDefinition TechDefinition => techDefinition;

}

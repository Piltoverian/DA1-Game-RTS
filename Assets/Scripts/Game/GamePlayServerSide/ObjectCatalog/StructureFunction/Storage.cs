using UnityEngine;
using System.Collections.Generic; 
public class Storage : MonoBehaviour
{
    [SerializeField] private List<ResourceType> typeofstorage;
    [SerializeField] private Collider threshholdCollider;
    [SerializeField] private Collider hardCollider;
    public void ReceiveResource(float amount,ResourceType type)
    {
        //play sound, animation, etc.
        Debug.Log("Received resource of type " + type+" Amount: "+amount);
    }

    void OnTriggerEnter(Collider other)
    {
        //check if the other is a worker if yes stop it and call ReceiveResource with the amount of resource the worker is carrying
        //and call they to return to the resource node to get more resource
        
    }

    public List<ResourceType> GetStorageTypes()
    {
        return typeofstorage;
    }
}

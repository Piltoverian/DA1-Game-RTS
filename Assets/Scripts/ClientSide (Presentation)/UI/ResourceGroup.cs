using Mono.Cecil;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;

public class ResourceGroup : MonoBehaviour
{
    public GameObject resourceInfoPrefab;
    public List<ResourcePair> resources;
    public void OnResourceChange(ResourceChangeEvent eventData)
    {
        ClearInfo();
        foreach (var resource in eventData.value)
        {
            var info = Instantiate(resourceInfoPrefab, transform).GetComponent<InfoPanel>();
            info.SetInfoValue(resource.Amount);
        }
    }

    public void ClearInfo()
    {
        foreach (Transform child in transform)
        {
            if (child.GetComponent<InfoPanel>() != null)
                Destroy(child.gameObject);
        }
    }

    private void FixedUpdate()
    {
        for(int i = 0; i < resources.Count; i++)
        {
            resources[i] = new ResourcePair(resources[i].Type,  resources[i].Amount + Time.deltaTime);
        }
        EventBus eventBus = Resources.Load<EventBus>("EventBus");

        if (eventBus == null)
        {
            Debug.LogError("EventBus not found");
            return;
        }
        ResourceChangeChannel channel = eventBus.GetChannel("ResourceChangeChannel") as ResourceChangeChannel;
        if (channel == null)
        {
            Debug.LogError("ResourceChangeChannel not found");
            return;
        }
        channel.RaiseEvent(new ResourceChangeEvent { value = resources });
    }
}

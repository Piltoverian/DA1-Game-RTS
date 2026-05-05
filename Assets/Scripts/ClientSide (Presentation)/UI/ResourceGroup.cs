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
            var info = Instantiate(resourceInfoPrefab, transform).GetComponent<ResourceInfo>();
            info.SetInfo(resource.Amount);
        }
    }

    public void ClearInfo()
    {
        foreach (Transform child in transform)
        {
            if (child.GetComponent<ResourceInfo>() != null)
                Destroy(child.gameObject);
        }
    }

    private void FixedUpdate()
    {
        for(int i = 0; i < resources.Count; i++)
        {
            resources[i] = new ResourcePair(resources[i].Type,  resources[i].Amount + Time.deltaTime);
        }
        ScriptableObject scriptableObject = EventBus.GetInstance().GetChannel("ResourceChangeChannel");
        if (scriptableObject == null)
        {
            Debug.LogError("ResourceChangeChannel not found in EventBus");
            return;
        }
        ResourceChangeEvent resourceChangeEvent = new ResourceChangeEvent();
        resourceChangeEvent.value = resources;
        (scriptableObject as ResourceChangeChannel).RaiseEvent(resourceChangeEvent);
    }
}

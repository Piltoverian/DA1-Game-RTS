using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI component nhận ResourceChangeEvent từ EventBus.
/// Đọc snapshot khi UI sẵn sàng và nhận event từ PlayerContextSyncSystem.
/// Kết nối qua ResourceChangeListener component trên cùng GameObject.
/// </summary>
public class ResourceGroup : UIComponent
{
    public GameObject resourceInfoPrefab;
    readonly List<ResourcePair> displayed = new();

    private void LateUpdate()
    {
        if (!TryGetPlayer(out var em)
            || PlayerContextHelper.GetPlayerContextEntity(em, playerId, out var entity, out _) != FunctionResult.Success
            || !em.HasBuffer<ResourcePair>(entity)) return;
        var resources = em.GetBuffer<ResourcePair>(entity, true);
        bool changed = resources.Length != displayed.Count;
        for (int i = 0; !changed && i < resources.Length; i++)
            changed = resources[i].Type != displayed[i].Type || resources[i].Amount != displayed[i].Amount;
        if (!changed) return;
        displayed.Clear();
        foreach (var resource in resources) displayed.Add(resource);
        RenderResources();
    }

    public void OnResourceChange(ResourceChangeEvent eventData)
    {
        if (TryGetPlayer(out _) && eventData.playerId == playerId)
        {
            displayed.Clear();
            displayed.AddRange(eventData.value);
            RenderResources();
        }
    }

    private void RenderResources()
    {
        ClearInfo();
        foreach (var resource in displayed)
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
}


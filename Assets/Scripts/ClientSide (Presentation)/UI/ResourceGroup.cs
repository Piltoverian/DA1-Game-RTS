using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI component nhận ResourceChangeEvent từ EventBus.
/// Không truy cập ECS trực tiếp — event được phát từ PlayerContextSyncSystem (ECS side).
/// Kết nối qua ResourceChangeListener component trên cùng GameObject.
/// </summary>
public class ResourceGroup : UIComponent
{
    public GameObject resourceInfoPrefab;

    public void OnResourceChange(ResourceChangeEvent eventData)
    {
        if (eventData.playerId == playerId)
        {
            ClearInfo();
            foreach (var resource in eventData.value)
            {
                var info = Instantiate(resourceInfoPrefab, transform).GetComponent<InfoPanel>();
                info.SetInfoValue(resource.Amount);
            }
        }
        else
        {
            Debug.LogWarning($"ResourceGroup: Received ResourceChangeEvent for playerId {eventData.playerId}, but this UI is for playerId {playerId}. Ignoring.");
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


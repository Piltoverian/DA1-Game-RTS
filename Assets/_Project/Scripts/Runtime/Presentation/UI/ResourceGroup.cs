using System.Collections.Generic;
using System.Globalization;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// UI component nhận ResourceChangeEvent từ EventBus.
/// Snapshot ban đầu từ UIManager; cập nhật tiếp theo qua EventBus.
/// Kết nối qua ResourceChangeListener component trên cùng GameObject.
/// </summary>
public class ResourceGroup : UIComponent
{
    public GameObject resourceInfoPrefab;
    [Header("Resource symbols")]
    [SerializeField] private Sprite goldIcon;
    [SerializeField] private Sprite woodIcon;
    [SerializeField] private Sprite foodIcon;
    [Header("Text (ready for localization binding)")]
    [SerializeField] private string goldLabel = "Gold";
    [SerializeField] private string woodLabel = "Wood";
    [SerializeField] private string foodLabel = "Food";
    [SerializeField] private string valueFormat = "{0}: {1}";
    [SerializeField] private string unavailableValue = "--";
    private readonly Dictionary<ResourceType, InfoPanel> cells = new();

    private void EnsureCells()
    {
        if (cells.Count != 0 || resourceInfoPrefab == null) return;
        // Reuse the prototype's existing cells; hide surplus placeholders without deleting assets.
        var existing = GetComponentsInChildren<InfoPanel>(true);
        var types = new[] { ResourceType.Gold, ResourceType.Wood, ResourceType.Food };
        for (int i = 0; i < types.Length; i++)
        {
            var panel = i < existing.Length ? existing[i] :
                Instantiate(resourceInfoPrefab, transform).GetComponent<InfoPanel>();
            if (panel == null) continue;
            panel.gameObject.SetActive(true);
            panel.transform.SetSiblingIndex(i);
            panel.SetInfoIcon(types[i] switch
            {
                ResourceType.Gold => goldIcon,
                ResourceType.Wood => woodIcon,
                ResourceType.Food => foodIcon,
                _ => null
            });
            cells.Add(types[i], panel);
        }
        for (int i = types.Length; i < existing.Length; i++) existing[i].gameObject.SetActive(false);
    }

    private string Label(ResourceType type) => type switch
    {
        ResourceType.Gold => goldLabel,
        ResourceType.Wood => woodLabel,
        ResourceType.Food => foodLabel,
        _ => type.ToString()
    };

    private void SetValue(ResourceType type, string value)
    {
        if (cells.TryGetValue(type, out var panel))
            panel.SetInfoText(string.Format(CultureInfo.CurrentCulture, valueFormat, Label(type), value));
    }

    private void SetValue(ResourceType type, float value) =>
        SetValue(type, Mathf.Round(value).ToString("0", CultureInfo.CurrentCulture));

    protected override void OnPlayerSnapshot(EntityManager manager, Entity entity, PlayerContext context)
    {
        EnsureCells();
        if (!manager.HasBuffer<ResourcePair>(entity)) return;
        var resources = manager.GetBuffer<ResourcePair>(entity, true);
        foreach (var cell in cells)
        {
            bool found = false;
            foreach (var resource in resources)
            {
                if (resource.Type != cell.Key) continue;
                SetValue(cell.Key, resource.Amount);
                found = true;
                break;
            }
            if (!found) SetValue(cell.Key, unavailableValue);
        }
    }

    protected override void OnContextUnavailable() => ClearInfo();

    public void OnResourceChange(ResourceChangeEvent eventData)
    {
        if (playerId >= 0 && eventData != null && eventData.playerId == playerId)
        {
            EnsureCells();
            foreach (var cell in cells)
            {
                bool found = false;
                if (eventData.value != null)
                {
                    foreach (var resource in eventData.value)
                    {
                        if (resource.Type != cell.Key) continue;
                        SetValue(cell.Key, resource.Amount);
                        found = true;
                        break;
                    }
                }
                if (!found) SetValue(cell.Key, unavailableValue);
            }
        }
    }

    public void ClearInfo()
    {
        EnsureCells();
        foreach (var cell in cells) SetValue(cell.Key, unavailableValue);
    }
}


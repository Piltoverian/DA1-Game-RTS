using Unity.Entities;
using UnityEngine;

public class UnitInfoPanel : MonoBehaviour
{
    [SerializeField] private InfoPanel InfoPanelPrefab;
    private InfoPanel healthPanel;
    private Sprite healthIcon;
    private Entity lastSelected;
    private ulong lastWorldSequence;
    private float lastMaxHealth = float.NaN;

    void Awake()
    {
        var mapping = Resources.Load<InfoIconMapping>("InfoIconMapping");
        if (mapping != null) healthIcon = mapping.GetIconForStat(StatInfo.MaxHealth);
    }

    void Update()
    {
        if (!ClientPresentationSelection.TryGet(out var world, out var em, out var selected, out _)
            || !em.HasComponent<EntityHealth>(selected) || InfoPanelPrefab == null)
        {
            ClearInfo();
            return;
        }
        float maxHealth = em.GetComponentData<EntityHealth>(selected).MaxHP;
        if (healthPanel == null)
        {
            healthPanel = Instantiate(InfoPanelPrefab, transform);
            if (healthIcon != null) healthPanel.SetInfoIcon(healthIcon);
        }
        if (lastWorldSequence != world.SequenceNumber || lastSelected != selected || lastMaxHealth != maxHealth)
        {
            healthPanel.SetInfoValue(maxHealth);
            lastWorldSequence = world.SequenceNumber;
            lastSelected = selected;
            lastMaxHealth = maxHealth;
        }
        if (!healthPanel.gameObject.activeSelf) healthPanel.gameObject.SetActive(true);
    }

    public void ClearInfo()
    {
        if (healthPanel != null && healthPanel.gameObject.activeSelf) healthPanel.gameObject.SetActive(false);
        lastSelected = Entity.Null;
        lastWorldSequence = 0;
        lastMaxHealth = float.NaN;
    }

    void OnDisable() => ClearInfo();
}

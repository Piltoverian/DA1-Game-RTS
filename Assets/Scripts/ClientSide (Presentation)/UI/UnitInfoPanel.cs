using UnityEngine;

public class UnitInfoPanel : MonoBehaviour
{
    [SerializeField] private InfoPanel InfoPanelPrefab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        ClearInfo();
        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(Selected), typeof(Unit));
        if (query.IsEmpty) return;
        var selectedUnits = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        var firstUnit = selectedUnits[0];
        InfoPanel healthpanel = Instantiate(InfoPanelPrefab, transform);
        InfoIconMapping infoIconMapping = Resources.Load<InfoIconMapping>("InfoIconMapping");
        healthpanel.SetInfoIcon(infoIconMapping.GetIconForStat(StatInfo.MaxHealth));
        healthpanel.SetInfoValue(entityManager.GetComponentData<Unit>(firstUnit).maxHealth);
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

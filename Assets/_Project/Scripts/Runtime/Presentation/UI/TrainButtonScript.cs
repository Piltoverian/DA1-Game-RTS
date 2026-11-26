using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class TrainButtonScript : MonoBehaviour
{
    private Button button;
    private int buttonIndexInPrefabList = 0;

    private void Awake() => button = GetComponent<Button>();

    private bool TryGetProduction(out EntityManager em, out Entity entity, out int playerId)
    {
        if (!ClientPresentationSelection.TryGet(out _, out em, out entity, out playerId)) return false;
        return CommandAuthority.CanControl(em, playerId, entity) && em.HasComponent<BuildingComponent>(entity)
            && em.HasBuffer<ProductionElement>(entity) && buttonIndexInPrefabList >= 0
            && buttonIndexInPrefabList < em.GetBuffer<ProductionElement>(entity).Length
            && !BuildingHelper.IsUnderConstruction(em, entity);
    }

    public void OnButtonClick()
    {
        if (!TryGetProduction(out var em, out var entity, out int playerId)) return;
        var offer = em.GetBuffer<ProductionElement>(entity)[buttonIndexInPrefabList];
        TrainUnitHelper.TrainUnit(em, entity, offer.JobReference, playerId);
    }

    private void Update()
    {
        if (button != null) button.interactable = TryGetProduction(out _, out _, out _);
    }
}

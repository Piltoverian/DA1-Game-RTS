using Unity.Entities;
using UnityEngine;

public class BuildingSelectionManager : MonoBehaviour
{
    public static BuildingSelectionManager Instance;

    public Entity SelectedBuilding { get; private set; } = Entity.Null;

    private EntityManager entityManager;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
            return;

        BuildingBlocker blocker =
            hit.collider.GetComponent<BuildingBlocker>();

        if (blocker == null)
            return;

        SelectedBuilding = blocker.BuildingEntity;

        Debug.Log("Selected building: " + SelectedBuilding);

        BuildingUI.Instance.Refresh();
    }

    public bool HasSelectedProductionBuilding()
    {
        if (SelectedBuilding == Entity.Null)
            return false;

        if (!entityManager.Exists(SelectedBuilding))
            return false;

        return entityManager.HasComponent<ProductionData>(SelectedBuilding);
    }

    public bool IsSelectedBuildingUnderConstruction()
    {
        if (SelectedBuilding == Entity.Null)
            return false;

        if (!entityManager.Exists(SelectedBuilding))
            return false;

        return entityManager.HasComponent<UnderConstructionTag>(SelectedBuilding);
    }

    public void TrainUnit()
    {
        if (!HasSelectedProductionBuilding())
            return;

        if (IsSelectedBuildingUnderConstruction())
        {
            Debug.Log("Building is still under construction.");
            return;
        }

        ProductionData prod =
            entityManager.GetComponentData<ProductionData>(SelectedBuilding);

        if (prod.QueueCount >= prod.MaxQueue)
        {
            Debug.Log("Production queue full.");
            return;
        }

        prod.QueueCount++;

        if (prod.TimeRemaining <= 0f)
        {
            prod.TimeRemaining = prod.ProductionTime;
        }

        entityManager.SetComponentData(SelectedBuilding, prod);

        Debug.Log("Queued unit. Queue = " + prod.QueueCount);

        BuildingUI.Instance.Refresh();
    }
}
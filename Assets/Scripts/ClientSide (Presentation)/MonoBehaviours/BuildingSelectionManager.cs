using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;

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

        Debug.Log("BuildingSelectionManager received left click");

        // Nếu click lên UI thì không select / deselect building
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("Click blocked by UI");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        int buildingMask = LayerMask.GetMask("Building");

        // Chỉ raycast vào BuildingBlocker, không raycast lung tung vào Ground
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, buildingMask))
        {
            Debug.Log("No building hit, clear selection");
            ClearSelection();
            return;
        }

        Debug.Log("Hit object: " + hit.collider.name);

        BuildingBlocker blocker =
            hit.collider.GetComponent<BuildingBlocker>();

        if (blocker == null)
        {
            Debug.Log("Hit Building layer but no BuildingBlocker component");
            ClearSelection();
            return;
        }

        if (blocker.BuildingEntity == Entity.Null)
        {
            Debug.Log("BuildingBlocker has Entity.Null");
            ClearSelection();
            return;
        }

        if (!entityManager.Exists(blocker.BuildingEntity))
        {
            Debug.Log("Building entity does not exist anymore");
            ClearSelection();
            return;
        }

        SelectedBuilding = blocker.BuildingEntity;

        Debug.Log("Selected building: " + SelectedBuilding);

        if (BuildingUI.Instance != null)
            BuildingUI.Instance.Refresh();
    }

    public void ClearSelection()
    {
        SelectedBuilding = Entity.Null;

        if (BuildingUI.Instance != null)
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

        EntityQuery query =
            entityManager.CreateEntityQuery(typeof(PlayerResourceData));

        if (query.IsEmpty)
        {
            Debug.LogWarning("PlayerResourceData not found.");
            return;
        }

        Entity resEntity = query.GetSingletonEntity();

        PlayerResourceData res =
            entityManager.GetComponentData<PlayerResourceData>(resEntity);

        if (res.Gold < prod.UnitGoldCost ||
            res.Food < prod.UnitFoodCost)
        {
            Debug.Log("Not enough resources to train.");
            return;
        }

        res.Gold -= prod.UnitGoldCost;
        res.Food -= prod.UnitFoodCost;

        entityManager.SetComponentData(resEntity, res);

        prod.QueueCount++;

        if (prod.TimeRemaining <= 0f)
        {
            prod.TimeRemaining = prod.ProductionTime;
        }

        entityManager.SetComponentData(SelectedBuilding, prod);

        Debug.Log("Queued unit. Queue = " + prod.QueueCount);

        if (BuildingUI.Instance != null)
            BuildingUI.Instance.Refresh();
    }
}
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class BuildingPlacer : MonoBehaviour
{
    [Header("Ghost Prefabs")]
    public GameObject barracksGhost;
    public GameObject towerGhost;
    public GameObject resourceDepotGhost;

    [Header("Grid")]
    public float gridSize = 1f;

    [Header("Fallback Footprint")]
    public Vector3 defaultHalfExtents = new Vector3(3f, 2f, 3f);

    private GameObject currentGhost;
    private bool isPlacing;

    private EntityManager entityManager;

    private BuildingType selectedBuildingType;
    private Entity selectedBuildingPrefab;
    private GameObject selectedGhostPrefab;

    private Vector3 currentSnappedPosition;
    private bool currentCanPlace;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        EntityQuery query =
            entityManager.CreateEntityQuery(typeof(BuildingPrefabData));

        if (query.IsEmpty)
        {
            Debug.LogError("Không tìm thấy BuildingPrefabData. Kiểm tra Bootstrap trong SubScene.");
            return;
        }

        BuildingPrefabData prefabData =
            query.GetSingleton<BuildingPrefabData>();

        // default building
        selectedBuildingType = BuildingType.Barracks;
        selectedBuildingPrefab = prefabData.BarracksPrefab;
        selectedGhostPrefab = barracksGhost;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartPlacing();
        }

        if (!isPlacing || currentGhost == null)
            return;

        UpdatePlacementPreview();

        if (Input.GetMouseButtonDown(0) && currentCanPlace)
        {
            PlaceBuilding(currentSnappedPosition);
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }
    public void SelectBuilding(BuildingType type)
    {
        selectedBuildingType = type;

        BuildingPrefabData prefabData =
            entityManager.CreateEntityQuery(typeof(BuildingPrefabData))
                .GetSingleton<BuildingPrefabData>();

        switch (type)
        {
            case BuildingType.Barracks:
                selectedBuildingPrefab = prefabData.BarracksPrefab;
                selectedGhostPrefab = barracksGhost;
                break;

            case BuildingType.Tower:
                selectedBuildingPrefab = prefabData.TowerPrefab;
                selectedGhostPrefab = towerGhost;
                break;

            case BuildingType.ResourceDepot:
                selectedBuildingPrefab = prefabData.ResourceDepotPrefab;
                selectedGhostPrefab = resourceDepotGhost;
                break;
        }

        StartPlacing();
    }
    void UpdatePlacementPreview()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, LayerMask.GetMask("Ground")))
            return;

        currentSnappedPosition = SnapToGrid(hit.point, gridSize);
        currentGhost.transform.position = currentSnappedPosition;

        Vector3 halfExtents = GetBuildingHalfExtents(selectedBuildingPrefab);

        currentCanPlace = CanPlace(currentSnappedPosition, halfExtents);

        SetGhostColor(currentCanPlace ? Color.green : Color.red);
    }

    public void StartPlacing()
    {
        if (currentGhost != null)
            Destroy(currentGhost);

        currentGhost = Instantiate(selectedGhostPrefab);
        SetLayerRecursively(currentGhost, LayerMask.NameToLayer("Ghost"));

        isPlacing = true;
    }

    Vector3 SnapToGrid(Vector3 pos, float grid)
    {
        return new Vector3(
            Mathf.Round(pos.x / grid) * grid,
            0f,
            Mathf.Round(pos.z / grid) * grid
        );
    }

    Vector3 GetBuildingHalfExtents(Entity prefab)
    {
        if (!entityManager.HasComponent<BuildingData>(prefab))
            return defaultHalfExtents;

        BuildingData data = entityManager.GetComponentData<BuildingData>(prefab);

        return new Vector3(
            data.FootprintSizeX * 0.5f,
            data.BlockerHeight * 0.5f,
            data.FootprintSizeZ * 0.5f
        );
    }

    bool CanPlace(Vector3 pos, Vector3 halfExtents)
    {
        Vector3 center = pos + Vector3.up * halfExtents.y;

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.identity,
            LayerMask.GetMask("Building", "Unit"),
            QueryTriggerInteraction.Collide
        );

        return hits.Length == 0;
    }

    void PlaceBuilding(Vector3 pos)
    {
        Destroy(currentGhost);
        currentGhost = null;
        isPlacing = false;

        Entity building = entityManager.Instantiate(selectedBuildingPrefab);

        entityManager.SetComponentData(
            building,
            LocalTransform.FromPosition(new float3(pos.x, pos.y, pos.z))
        );

        Vector3 halfExtents = GetBuildingHalfExtents(selectedBuildingPrefab);
        CreateBuildingBlocker(pos, halfExtents);
    }

    void CreateBuildingBlocker(Vector3 pos, Vector3 halfExtents)
    {
        GameObject blocker = new GameObject("BuildingBlocker");

        blocker.layer = LayerMask.NameToLayer("Building");
        blocker.transform.position = pos + Vector3.up * halfExtents.y;

        BoxCollider col = blocker.AddComponent<BoxCollider>();
        col.size = halfExtents * 2f;
        col.isTrigger = true;
    }

    void CancelPlacement()
    {
        if (currentGhost != null)
            Destroy(currentGhost);

        currentGhost = null;
        isPlacing = false;
    }

    void SetGhostColor(Color color)
    {
        Renderer[] renderers = currentGhost.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in renderers)
        {
            r.material.color = color;
        }
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    void OnDrawGizmos()
    {
        if (!isPlacing || currentGhost == null)
            return;

        Vector3 halfExtents = defaultHalfExtents;

        if (entityManager != default &&
            selectedBuildingPrefab != Entity.Null &&
            entityManager.HasComponent<BuildingData>(selectedBuildingPrefab))
        {
            halfExtents = GetBuildingHalfExtents(selectedBuildingPrefab);
        }

        Gizmos.color = currentCanPlace
            ? new Color(0f, 1f, 0f, 0.25f)
            : new Color(1f, 0f, 0f, 0.25f);

        Gizmos.DrawCube(
            currentSnappedPosition + Vector3.up * halfExtents.y,
            halfExtents * 2f
        );

        Gizmos.color = Color.white;

        Gizmos.DrawWireCube(
            currentSnappedPosition + Vector3.up * halfExtents.y,
            halfExtents * 2f
        );
    }
    public void SelectBarracks()
    {
        SelectBuilding(BuildingType.Barracks);
    }

    public void SelectTower()
    {
        SelectBuilding(BuildingType.Tower);
    }

    public void SelectResourceDepot()
    {
        SelectBuilding(BuildingType.ResourceDepot);
    }
}
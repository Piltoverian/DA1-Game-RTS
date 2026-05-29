using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingPlacer : MonoBehaviour
{
    public static BuildingPlacer Instance;

    [Header("Placement Database")]
    public BuildingPlacementDatabase placementDatabase;

    [Header("Ghost Material")]
    public Material validGhostMaterial;
    public Material invalidGhostMaterial;

    [Header("Grid")]
    public float gridSize = 1f;

    [Header("Fallback Footprint")]
    public Vector3 defaultHalfExtents = new Vector3(3f, 2f, 3f);

    [Header("Raycast")]
    public LayerMask groundMask;

    [Header("Placement Collision")]
    public LayerMask placementBlockMask;
    public float placementCheckPadding = 0.05f;
    public bool logPlacementBlocking = false;

    private GameObject currentGhost;
    private Renderer[] currentGhostRenderers;

    private bool isPlacing;
    private bool isEntityManagerReady;

    private EntityManager entityManager;

    private int selectedCommandIndex;
    private BuildingType selectedBuildingType;
    private Entity selectedBuildingPrefab;
    private GameObject selectedPreviewPrefab;

    private Vector3 currentSnappedPosition;
    private bool currentCanPlace;

    private bool hasAppliedGhostMaterial;
    private bool lastGhostCanPlace;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null)
        {
            Debug.LogError("DefaultGameObjectInjectionWorld is null.");
            return;
        }

        entityManager = world.EntityManager;
        isEntityManagerReady = true;

        EntityQuery query = entityManager.CreateEntityQuery(typeof(BuildingPrefabData));

        if (query.IsEmpty)
        {
            //Debug.LogError("Không tìm thấy BuildingPrefabData. Kiểm tra BuildingPrefabAuthoring trong SubScene.");
            return;
        }

        if (groundMask.value == 0)
        {
            groundMask = LayerMask.GetMask("Ground");
        }

        if (placementBlockMask.value == 0)
        {
            placementBlockMask = LayerMask.GetMask("Building", "Obstacle");
        }
    }

    private void Update()
    {
        if (!isPlacing || currentGhost == null)
            return;

        UpdatePlacementPreview();

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0) && currentCanPlace)
        {
            if (!CanAffordBuilding(selectedBuildingPrefab))
            {
                Debug.Log("Not enough resources to build.");
                return;
            }

            PayBuildingCost(selectedBuildingPrefab);
            PlaceBuilding(currentSnappedPosition);
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }

    public void StartPlacementFromCommand(CommandData commandData, Entity sourceEntity)
    {
        if (commandData.Type != CommandType.Build)
        {
            Debug.LogWarning("Command is not Build.");
            return;
        }

        if (sourceEntity == Entity.Null)
        {
            Debug.LogWarning("Build command has null source entity.");
            return;
        }

        if (placementDatabase == null)
        {
            Debug.LogError("BuildingPlacementDatabase is null on BuildingPlacer.");
            return;
        }

        BuildingPlacementDefinition definition =
            placementDatabase.GetByCommandIndex(commandData.indexInUnitCommandList);

        if (definition == null)
        {
            Debug.LogError("No BuildingPlacementDefinition for command index: " + commandData.indexInUnitCommandList);
            return;
        }

        SelectBuilding(definition);
    }

    public void SelectBuilding(BuildingPlacementDefinition definition)
    {
        if (!isEntityManagerReady)
        {
            Debug.LogError("BuildingPlacer EntityManager is not ready.");
            return;
        }

        if (definition == null)
        {
            Debug.LogError("BuildingPlacementDefinition is null.");
            return;
        }

        if (definition.PreviewPrefab == null)
        {
            Debug.LogError("PreviewPrefab is null on " + definition.name);
            return;
        }

        selectedCommandIndex = definition.CommandIndex;
        selectedBuildingType = definition.BuildingType;
        selectedBuildingPrefab = GetBuildingPrefabEntityByCommandIndex(selectedCommandIndex);
        selectedPreviewPrefab = definition.PreviewPrefab;

        if (selectedBuildingPrefab == Entity.Null)
        {
            Debug.LogError("Selected building ECS prefab is null. CommandIndex = " + selectedCommandIndex);
            return;
        }

        StartPlacing();
    }

    public void SelectBuildingByType(BuildingType type)
    {
        if (placementDatabase == null)
        {
            Debug.LogError("BuildingPlacementDatabase is null.");
            return;
        }

        BuildingPlacementDefinition definition = placementDatabase.GetByType(type);

        if (definition == null)
        {
            Debug.LogError("No BuildingPlacementDefinition for type: " + type);
            return;
        }

        SelectBuilding(definition);
    }

    public void SelectBarracks()
    {
        SelectBuildingByType(BuildingType.Barracks);
    }

    public void SelectTower()
    {
        SelectBuildingByType(BuildingType.Tower);
    }

    public void SelectResourceDepot()
    {
        SelectBuildingByType(BuildingType.ResourceDepot);
    }

    //private Entity GetBuildingPrefabEntity(BuildingType type)
    //{
    //    EntityQuery query = entityManager.CreateEntityQuery(typeof(BuildingPrefabData));

    //    if (query.IsEmpty)
    //        return Entity.Null;

    //    BuildingPrefabData prefabData = query.GetSingleton<BuildingPrefabData>();

    //    switch (type)
    //    {
    //        case BuildingType.Barracks:
    //            return prefabData.BarracksPrefab;

    //        case BuildingType.Tower:
    //            return prefabData.TowerPrefab;

    //        case BuildingType.ResourceDepot:
    //            return prefabData.ResourceDepotPrefab;

    //        default:
    //            return Entity.Null;
    //    }
    //}
    private Entity GetBuildingPrefabEntityByCommandIndex(int commandIndex)
    {
        EntityQuery query = entityManager.CreateEntityQuery(
            typeof(BuildingPrefabCatalogTag),
            typeof(BuildingPrefabCatalogElement)
        );

        if (query.IsEmpty)
        {
            Debug.LogError("BuildingPrefabCatalog not found. Add BuildingPrefabCatalogAuthoring to a SubScene.");
            return Entity.Null;
        }

        Entity catalogEntity = query.GetSingletonEntity();

        DynamicBuffer<BuildingPrefabCatalogElement> buffer =
            entityManager.GetBuffer<BuildingPrefabCatalogElement>(catalogEntity);

        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].CommandIndex == commandIndex)
                return buffer[i].Prefab;
        }

        Debug.LogError("No ECS building prefab found for CommandIndex = " + commandIndex);
        return Entity.Null;
    }
    private void StartPlacing()
    {
        if (selectedPreviewPrefab == null)
        {
            Debug.LogError("Selected preview prefab is null.");
            return;
        }

        if (currentGhost != null)
            Destroy(currentGhost);

        currentGhost = Instantiate(selectedPreviewPrefab);
        PrepareGhostObject(currentGhost);

        hasAppliedGhostMaterial = false;
        isPlacing = true;
    }

    private void PrepareGhostObject(GameObject ghost)
    {
        int ghostLayer = LayerMask.NameToLayer("Ghost");

        if (ghostLayer >= 0)
        {
            SetLayerRecursively(ghost, ghostLayer);
        }

        Collider[] colliders = ghost.GetComponentsInChildren<Collider>(true);

        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        currentGhostRenderers = ghost.GetComponentsInChildren<Renderer>(true);

        Debug.Log("Ghost renderer count: " + currentGhostRenderers.Length);

        foreach (Renderer renderer in currentGhostRenderers)
        {
            Debug.Log(
                "Ghost renderer: " + renderer.name +
                " | material count: " + renderer.sharedMaterials.Length
            );
        }

        SetGhostMaterial(true, true);
    }

    private void UpdatePlacementPreview()
    {
        Camera cam = Camera.main;

        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            return;

        currentSnappedPosition = SnapToGrid(hit.point, gridSize);

        currentGhost.transform.position = currentSnappedPosition;

        Vector3 halfExtents = GetBuildingHalfExtents(selectedBuildingPrefab);

        currentCanPlace = CanPlace(currentSnappedPosition, halfExtents);

        SetGhostMaterial(currentCanPlace);
    }

    private Vector3 SnapToGrid(Vector3 pos, float grid)
    {
        if (grid <= 0f)
            grid = 1f;

        return new Vector3(
            Mathf.Round(pos.x / grid) * grid,
            0f,
            Mathf.Round(pos.z / grid) * grid
        );
    }

    private Vector3 GetBuildingHalfExtents(Entity prefab)
    {
        if (!isEntityManagerReady)
            return defaultHalfExtents;

        if (prefab == Entity.Null)
            return defaultHalfExtents;

        if (!entityManager.HasComponent<BuildingData>(prefab))
            return defaultHalfExtents;

        BuildingData data = entityManager.GetComponentData<BuildingData>(prefab);

        return new Vector3(
            data.FootprintSizeX * 0.5f,
            data.BlockerHeight * 0.5f,
            data.FootprintSizeZ * 0.5f
        );
    }

    private bool CanPlace(Vector3 pos, Vector3 halfExtents)
    {
        if (HasPlacementCollision(pos, halfExtents))
            return false;

        EntityQuery gridQuery = entityManager.CreateEntityQuery(typeof(GridComponent));

        if (gridQuery.IsEmpty)
            return false;

        GridComponent grid = gridQuery.GetSingleton<GridComponent>();

        float minX = pos.x - halfExtents.x + 0.01f;
        float minZ = pos.z - halfExtents.z + 0.01f;
        float maxX = pos.x + halfExtents.x - 0.01f;
        float maxZ = pos.z + halfExtents.z - 0.01f;

        int2 minGrid = GridHelper.WorldToGrid(new float3(minX, 0, minZ), grid);
        int2 maxGrid = GridHelper.WorldToGrid(new float3(maxX, 0, maxZ), grid);

        EntityQuery bucketQuery = entityManager.CreateEntityQuery(typeof(MovementAgentBucket));
        bool hasUnitBucket = !bucketQuery.IsEmpty;
        NativeParallelMultiHashMap<int, Entity> unitBucket = default;

        if (hasUnitBucket)
        {
            unitBucket = bucketQuery.GetSingleton<MovementAgentBucket>().Bucket;
        }

        for (int x = minGrid.x; x <= maxGrid.x; x++)
        {
            for (int y = minGrid.y; y <= maxGrid.y; y++)
            {
                if (x < 0 || x >= grid.width || y < 0 || y >= grid.height)
                    return false;

                int gridIndex = GridHelper.GetNodeIndex(new int2(x, y), grid);

                if (hasUnitBucket && unitBucket.ContainsKey(gridIndex))
                    return false;
            }
        }

        return true;
    }

    private bool HasPlacementCollision(Vector3 pos, Vector3 halfExtents)
    {
        Vector3 boxCenter = pos + Vector3.up * halfExtents.y;

        Vector3 checkHalfExtents = new Vector3(
            Mathf.Max(0.01f, halfExtents.x - placementCheckPadding),
            Mathf.Max(0.01f, halfExtents.y - placementCheckPadding),
            Mathf.Max(0.01f, halfExtents.z - placementCheckPadding)
        );

        Collider[] hits = Physics.OverlapBox(
            boxCenter,
            checkHalfExtents,
            Quaternion.identity,
            placementBlockMask,
            QueryTriggerInteraction.Collide
        );

        if (hits.Length > 0)
        {
            if (logPlacementBlocking)
            {
                Debug.Log("Cannot place. Blocked by: " + hits[0].name);
            }

            return true;
        }

        return false;
    }

    private bool CanAffordBuilding(Entity prefab)
    {
        if (prefab == Entity.Null)
            return false;

        if (!entityManager.HasComponent<BuildingData>(prefab))
            return false;

        EntityQuery query = entityManager.CreateEntityQuery(typeof(PlayerResourceData));

        if (query.IsEmpty)
        {
            Debug.LogWarning("PlayerResourceData not found.");
            return false;
        }

        BuildingData building = entityManager.GetComponentData<BuildingData>(prefab);
        PlayerResourceData res = query.GetSingleton<PlayerResourceData>();

        return res.Gold >= building.GoldCost &&
               res.Wood >= building.WoodCost;
    }

    private void PayBuildingCost(Entity prefab)
    {
        BuildingData building = entityManager.GetComponentData<BuildingData>(prefab);

        EntityQuery query = entityManager.CreateEntityQuery(typeof(PlayerResourceData));

        if (query.IsEmpty)
            return;

        Entity resEntity = query.GetSingletonEntity();

        PlayerResourceData res = entityManager.GetComponentData<PlayerResourceData>(resEntity);

        res.Gold -= building.GoldCost;
        res.Wood -= building.WoodCost;

        entityManager.SetComponentData(resEntity, res);
    }

    private void PlaceBuilding(Vector3 pos)
    {
        if (currentGhost != null)
            Destroy(currentGhost);

        currentGhost = null;
        isPlacing = false;

        Entity building = entityManager.Instantiate(selectedBuildingPrefab);

        if (entityManager.HasComponent<LocalTransform>(building))
        {
            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(building);
            transform.Position = new float3(pos.x, pos.y, pos.z);
            entityManager.SetComponentData(building, transform);
        }
        else
        {
            entityManager.AddComponentData(
                building,
                LocalTransform.FromPosition(new float3(pos.x, pos.y, pos.z))
            );
        }

        Vector3 halfExtents = GetBuildingHalfExtents(selectedBuildingPrefab);

        CreateBuildingBlocker(pos, halfExtents, building);
    }

    private void CreateBuildingBlocker(Vector3 pos, Vector3 halfExtents, Entity buildingEntity)
    {
        GameObject blocker = new GameObject("BuildingBlocker");

        int buildingLayer = LayerMask.NameToLayer("Building");

        if (buildingLayer >= 0)
        {
            blocker.layer = buildingLayer;
        }
        else
        {
            Debug.LogWarning("Layer 'Building' does not exist.");
        }

        blocker.transform.position = pos + Vector3.up * halfExtents.y;

        BoxCollider col = blocker.AddComponent<BoxCollider>();
        col.size = halfExtents * 2f;
        col.center = Vector3.zero;
        col.isTrigger = false;

        BuildingBlocker buildingBlocker = blocker.AddComponent<BuildingBlocker>();
        buildingBlocker.BuildingEntity = buildingEntity;
    }

    private void CancelPlacement()
    {
        if (currentGhost != null)
            Destroy(currentGhost);

        currentGhost = null;
        currentGhostRenderers = null;

        isPlacing = false;
        selectedBuildingPrefab = Entity.Null;
        selectedPreviewPrefab = null;

        hasAppliedGhostMaterial = false;
    }

    private void SetGhostMaterial(bool canPlace, bool force = false)
    {
        if (currentGhostRenderers == null)
            return;

        if (!force && hasAppliedGhostMaterial && lastGhostCanPlace == canPlace)
            return;

        Material targetMaterial = canPlace ? validGhostMaterial : invalidGhostMaterial;

        if (targetMaterial == null)
        {
            Debug.LogWarning("Ghost material is null.");
            return;
        }

        foreach (Renderer renderer in currentGhostRenderers)
        {
            if (renderer == null)
                continue;

            int materialCount = renderer.sharedMaterials.Length;

            Material[] newMaterials = new Material[materialCount];

            for (int i = 0; i < materialCount; i++)
            {
                newMaterials[i] = targetMaterial;
            }

            renderer.materials = newMaterials;
        }

        hasAppliedGhostMaterial = true;
        lastGhostCanPlace = canPlace;
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        if (!isPlacing || currentGhost == null)
            return;

        Vector3 halfExtents = defaultHalfExtents;

        if (isEntityManagerReady &&
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
}
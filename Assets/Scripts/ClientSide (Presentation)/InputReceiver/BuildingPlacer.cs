using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingPlacer : MonoBehaviour
{
    [Header("Singleton")]
    private static BuildingPlacer _instance;

    public static BuildingPlacer Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<BuildingPlacer>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("BuildingPlacer");
                    _instance = go.AddComponent<BuildingPlacer>();
                }
            }
            return _instance;
        }
    }

    [Header("Database")]
    [SerializeField] private BuildingDatabase buildingDatabase;
    public BuildingDatabase BuildingDatabase => buildingDatabase;

    [Header("Ghost Materials")]
    [SerializeField] private Material validGhostMaterial;
    [SerializeField] private Material invalidGhostMaterial;

    [Header("Raycast")]
    [SerializeField] private LayerMask groundMask;

    private EntityManager entityManager;
    private bool isPlacing;
    private BuildingDefinition currentDefinition;
    private Entity selectedBuildingPrefab;
    private Entity sourceWorkerEntity;
    private int localPlayerId = -1;

    private GameObject currentGhost;
    private Renderer[] currentGhostRenderers;
    private Vector3 currentSnappedPosition;
    private StartEndRect currentLocalRect;
    private bool currentCanPlace;

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else if (_instance != this)
            Destroy(gameObject);
    }

    private void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
            entityManager = world.EntityManager;

        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");
    }

    private void Update()
    {
        if (!isPlacing || currentGhost == null)
            return;

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
            return;
        }

        UpdatePlacementPreview();

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0) && currentCanPlace)
        {
            if (!CanAffordBuilding(selectedBuildingPrefab))
                return;

            PlaceBuilding(currentSnappedPosition, localPlayerId);
        }
    }

    public void StartPlacementFromCommand(CommandData commandData, Entity sourceEntity, int playerID)
    {
        if (commandData.Type != CommandType.Build)
            return;

        if (sourceEntity == Entity.Null)
            return;

        if (buildingDatabase == null)
        {
            Debug.LogError("BuildingDatabase is null on BuildingPlacer.");
            return;
        }

        int index = commandData.indexInUnitCommandList;
        BuildingDefinition definition = buildingDatabase.GetByIndex(index);
        if (definition == null)
            definition = buildingDatabase.GetByCommandIndex(index);

        if (definition == null)
        {
            Debug.LogError("No BuildingDefinition found for index: " + index);
            return;
        }

        StartPlacement(definition, sourceEntity, playerID, index);
    }

    public void StartPlacement(BuildingDefinition definition, Entity sourceWorker, int playerId, int databaseIndex = -1)
    {
        if (definition == null)
            return;

        CancelPlacement();

        currentDefinition = definition;
        sourceWorkerEntity = sourceWorker;
        localPlayerId = playerId;

        int lookupIndex = databaseIndex >= 0 ? databaseIndex : (buildingDatabase != null ? buildingDatabase.GetIndexOf(definition) : -1);
        selectedBuildingPrefab = GetBuildingPrefabEntityByCommandIndex(lookupIndex);

        if (entityManager != default && selectedBuildingPrefab != Entity.Null && entityManager.HasComponent<BlockageData>(selectedBuildingPrefab))
        {
            currentLocalRect = entityManager.GetComponentData<BlockageData>(selectedBuildingPrefab).LocalRect;
        }
        else
        {
            currentLocalRect = new StartEndRect(new float2(-1.5f, -1.5f));
            currentLocalRect.ExpandTo(new float2(1.5f, 1.5f));
        }

        GameObject previewSource = definition.BuildingPrefab != null ? definition.BuildingPrefab : definition.PreviewPrefab;
        if (previewSource != null)
        {
            currentGhost = CreateGhostFromPrefab(previewSource);
        }
        else
        {
            currentGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider col = currentGhost.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        currentGhostRenderers = currentGhost.GetComponentsInChildren<Renderer>(true);
        SetGhostMaterial(true);
        isPlacing = true;
    }

    private GameObject CreateGhostFromPrefab(GameObject prefab)
    {
        GameObject ghostRoot = new GameObject("Ghost_" + prefab.name);

        MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            GameObject part = new GameObject(mf.name);
            part.transform.SetParent(ghostRoot.transform, false);

            part.transform.localPosition = prefab.transform.InverseTransformPoint(mf.transform.position);
            part.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * mf.transform.rotation;
            part.transform.localScale = mf.transform.lossyScale;

            MeshFilter newMf = part.AddComponent<MeshFilter>();
            newMf.sharedMesh = mf.sharedMesh;

            MeshRenderer newMr = part.AddComponent<MeshRenderer>();
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) urpShader = Shader.Find("Universal Render Pipeline/Unlit");
            newMr.sharedMaterial = validGhostMaterial != null ? validGhostMaterial : (urpShader != null ? new Material(urpShader) : null);
        }

        int ghostLayer = LayerMask.NameToLayer("Ghost");
        if (ghostLayer >= 0)
            SetLayerRecursively(ghostRoot, ghostLayer);

        return ghostRoot;
    }

    private void UpdatePlacementPreview()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
        {
            currentCanPlace = false;
            SetGhostMaterial(false);
            return;
        }

        currentSnappedPosition = SnapToGrid(hit.point);
        currentGhost.transform.position = currentSnappedPosition;

        currentCanPlace = CanPlace(currentSnappedPosition) && CanAffordBuilding(selectedBuildingPrefab);
        SetGhostMaterial(currentCanPlace);
    }

    private Vector3 SnapToGrid(Vector3 hitPoint)
    {
        if (entityManager != default)
        {
            var query = entityManager.CreateEntityQuery(typeof(GridComponent));
            if (!query.IsEmpty)
            {
                var grid = query.GetSingleton<GridComponent>();
                float width = currentLocalRect.MaxPoint.x - currentLocalRect.MinPoint.x;
                float depth = currentLocalRect.MaxPoint.y - currentLocalRect.MinPoint.y;
                float3 anchorWorld = new float3(hitPoint.x - width * 0.5f, 0, hitPoint.z - depth * 0.5f);

                int2 gridPos = GridHelper.WorldToGrid(anchorWorld, grid);
                float3 cellCorner = grid.origin + new float3(gridPos.x * grid.cellsize, 0, gridPos.y * grid.cellsize);

                float snappedX = cellCorner.x - currentLocalRect.MinPoint.x;
                float snappedZ = cellCorner.z - currentLocalRect.MinPoint.y;
                return new Vector3(snappedX, 0f, snappedZ);
            }
        }

        return new Vector3(Mathf.Round(hitPoint.x), 0f, Mathf.Round(hitPoint.z));
    }

    private bool CanPlace(Vector3 rootPosition)
    {
        if (entityManager == default) return true;

        var gridQuery = entityManager.CreateEntityQuery(typeof(GridComponent));
        if (gridQuery.IsEmpty) return true;

        Entity gridEntity = gridQuery.GetSingletonEntity();
        var grid = entityManager.GetComponentData<GridComponent>(gridEntity);
        var costBuffer = entityManager.GetBuffer<GridNodeCost>(gridEntity);

        float2 worldMin = new float2(rootPosition.x + currentLocalRect.MinPoint.x, rootPosition.z + currentLocalRect.MinPoint.y);
        float2 worldMax = new float2(rootPosition.x + currentLocalRect.MaxPoint.x, rootPosition.z + currentLocalRect.MaxPoint.y);

        int2 minGrid = GridHelper.WorldToGrid(new float3(worldMin.x + 0.05f, 0, worldMin.y + 0.05f), grid);
        int2 maxGrid = GridHelper.WorldToGrid(new float3(worldMax.x - 0.05f, 0, worldMax.y - 0.05f), grid);

        var bucketQuery = entityManager.CreateEntityQuery(typeof(MovementAgentBucket));
        bool hasUnitBucket = !bucketQuery.IsEmpty;
        NativeParallelMultiHashMap<int, Entity> unitBucket = default;
        if (hasUnitBucket)
            unitBucket = bucketQuery.GetSingleton<MovementAgentBucket>().Bucket;

        for (int x = minGrid.x; x <= maxGrid.x; x++)
        {
            for (int y = minGrid.y; y <= maxGrid.y; y++)
            {
                if (x < 0 || x >= grid.width || y < 0 || y >= grid.height)
                    return false;

                int idx = GridHelper.GetNodeIndex(new int2(x, y), grid);
                if (costBuffer[idx].cost != 1)
                    return false;

                if (hasUnitBucket && unitBucket.ContainsKey(idx))
                    return false;
            }
        }

        return true;
    }

    private bool CanAffordBuilding(Entity buildingPrefab)
    {
        if (buildingPrefab == Entity.Null || localPlayerId < 0)
            return false;

        if (entityManager == default || !entityManager.HasBuffer<BuildingCost>(buildingPrefab))
            return true;

        var costBuffer = entityManager.GetBuffer<BuildingCost>(buildingPrefab);
        foreach (var cost in costBuffer)
        {
            if (PlayerContextHelper.GetPlayerResourceByType(entityManager, localPlayerId, cost.Type, out float currentAmount) != FunctionResult.Success)
                return false;

            if (currentAmount < cost.Amount)
                return false;
        }

        return true;
    }

    private void PlaceBuilding(Vector3 rootPosition, int playerId)
    {
        if (selectedBuildingPrefab == Entity.Null || entityManager == default)
            return;

        Entity requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new PlaceBuildingRequest
        {
            PlayerId = playerId,
            PrefabEntity = selectedBuildingPrefab,
            Position = new float3(rootPosition.x, rootPosition.y, rootPosition.z),
        });

        DynamicBuffer<PlaceBuildingWorkerElement> workerBuffer = entityManager.AddBuffer<PlaceBuildingWorkerElement>(requestEntity);

        List<Entity> selectedEntities = SelectHelper.GetAllSelectedEntitiesByplayerID(playerId);
        bool addedAny = false;

        foreach (Entity worker in selectedEntities)
        {
            if (entityManager.HasComponent<BuilderComponent>(worker) && entityManager.HasComponent<MoveOverride>(worker))
            {
                workerBuffer.Add(new PlaceBuildingWorkerElement { WorkerEntity = worker });
                addedAny = true;
            }
        }

        if (!addedAny && sourceWorkerEntity != Entity.Null && entityManager.Exists(sourceWorkerEntity))
        {
            if (entityManager.HasComponent<BuilderComponent>(sourceWorkerEntity) && entityManager.HasComponent<MoveOverride>(sourceWorkerEntity))
            {
                workerBuffer.Add(new PlaceBuildingWorkerElement { WorkerEntity = sourceWorkerEntity });
            }
        }

        CancelPlacement();
    }

    private Entity GetBuildingPrefabEntityByCommandIndex(int commandIndex)
    {
        if (entityManager == default)
            return Entity.Null;

        EntityQuery query = entityManager.CreateEntityQuery(
            typeof(BuildingPrefabCatalogTag),
            typeof(BuildingPrefabCatalogElement)
        );

        if (query.IsEmpty)
            return Entity.Null;

        Entity catalogEntity = query.GetSingletonEntity();
        DynamicBuffer<BuildingPrefabCatalogElement> buffer = entityManager.GetBuffer<BuildingPrefabCatalogElement>(catalogEntity);

        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].CommandIndex == commandIndex)
                return buffer[i].Prefab;
        }

        if (commandIndex >= 0 && commandIndex < buffer.Length)
        {
            return buffer[commandIndex].Prefab;
        }

        return Entity.Null;
    }

    public void CancelPlacement()
    {
        if (currentGhost != null)
            Destroy(currentGhost);

        currentGhost = null;
        currentGhostRenderers = null;
        isPlacing = false;
        currentDefinition = null;
        selectedBuildingPrefab = Entity.Null;
    }

    private void SetGhostMaterial(bool canPlace)
    {
        if (currentGhostRenderers == null) return;

        Material targetMat = canPlace ? validGhostMaterial : invalidGhostMaterial;
        if (targetMat == null) return;

        foreach (var r in currentGhostRenderers)
        {
            if (r != null)
                r.sharedMaterial = targetMat;
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}

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

    [Header("Ghost Materials")]
    [SerializeField] private Material validGhostMaterial;
    [SerializeField] private Material invalidGhostMaterial;
    [SerializeField] private Material unknownGhostMaterial;

    [Header("Raycast")]
    [SerializeField] private LayerMask groundMask;

    private EntityManager entityManager;
    private bool isPlacing;
    private FixedString64Bytes currentBuildingId;
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

    public void StartPlacementFromOfferIndex(int index, Entity sourceEntity, int playerID)
    {
        if (sourceEntity == Entity.Null)
            return;

        if (entityManager != default && entityManager.HasBuffer<BuildOffer>(sourceEntity))
        {
            var offers = entityManager.GetBuffer<BuildOffer>(sourceEntity);
            if (index >= 0 && index < offers.Length)
            {
                StartPlacement(offers[index].DefinitionID, sourceEntity, playerID);
                return;
            }
        }

        Debug.LogError("No BuildOffer found for index: " + index);
    }

    public void StartPlacement(FixedString64Bytes buildingId, Entity sourceWorker, int playerId)
    {
        CancelPlacement();

        currentBuildingId = buildingId;
        sourceWorkerEntity = sourceWorker;
        localPlayerId = playerId;

        selectedBuildingPrefab = GetBuildingPrefabEntityFromRegistry(buildingId);

        if (selectedBuildingPrefab == Entity.Null)
        {
            Debug.LogError($"Building with ID '{buildingId}' not found in GameDataRegistry.");
            return;
        }

        if (entityManager != default && entityManager.HasComponent<BlockageData>(selectedBuildingPrefab))
        {
            currentLocalRect = entityManager.GetComponentData<BlockageData>(selectedBuildingPrefab).LocalRect;
        }
        else
        {
            currentLocalRect = new StartEndRect(new float2(-1.5f, -1.5f));
            currentLocalRect.ExpandTo(new float2(1.5f, 1.5f));
        }

        GameObject previewSource = EntityPresentation.BuildingPreview(buildingId);
        if (previewSource != null)
        {
            currentGhost = CreateGhostFromPrefab(previewSource);
        }
        else
        {
            currentGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            currentGhost.SetActive(false);
            Collider col = currentGhost.GetComponent<Collider>();
            if (col != null) Destroy(col);
            float width = currentLocalRect.MaxPoint.x - currentLocalRect.MinPoint.x;
            float depth = currentLocalRect.MaxPoint.y - currentLocalRect.MinPoint.y;
            currentGhost.transform.localScale = new Vector3(math.max(1f, width), 2f, math.max(1f, depth));
        }

        currentGhostRenderers = currentGhost.GetComponentsInChildren<Renderer>(true);
        SetGhostMaterial(BuildingPreviewState.Unknown);
        isPlacing = true;
        UpdatePlacementPreview();
    }

    private GameObject CreateGhostFromPrefab(GameObject prefab)
    {
        GameObject ghostRoot = new GameObject("Ghost_" + prefab.name);
        ghostRoot.SetActive(false);
        var model = new GameObject("Snapped model").transform;
        model.SetParent(ghostRoot.transform, false);
        model.localScale = prefab.transform.lossyScale;
        if (entityManager != default && entityManager.Exists(selectedBuildingPrefab)
            && entityManager.HasComponent<PostTransformMatrix>(selectedBuildingPrefab))
        {
            var matrix = entityManager.GetComponentData<PostTransformMatrix>(selectedBuildingPrefab).Value;
            model.localPosition = matrix.c3.xyz;
            model.localScale = new Vector3(math.length(matrix.c0.xyz), math.length(matrix.c1.xyz), math.length(matrix.c2.xyz));
        }

        MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            GameObject part = new GameObject(mf.name);
            part.transform.SetParent(model, false);

            part.transform.localPosition = prefab.transform.InverseTransformPoint(mf.transform.position);
            part.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * mf.transform.rotation;
            part.transform.localScale = new Vector3(mf.transform.lossyScale.x / prefab.transform.lossyScale.x,
                mf.transform.lossyScale.y / prefab.transform.lossyScale.y, mf.transform.lossyScale.z / prefab.transform.lossyScale.z);

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
        Vector3 hitPoint;
        if(TerrainVisualSurface.Active!=null && TerrainVisualSurface.Active.Raycast(ray,out var logical,out _)) hitPoint=logical;
        else if(Physics.Raycast(ray,out RaycastHit hit,500f,groundMask)) hitPoint=hit.point;
        else if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) hitPoint = ray.GetPoint(distance);
        else {currentCanPlace=false;SetGhostMaterial(BuildingPreviewState.Invalid);currentGhost.SetActive(false);return;}
        currentSnappedPosition=SnapToGrid(hitPoint);
        Vector3 visiblePosition=currentSnappedPosition;
        if(TerrainVisualSurface.Active!=null)visiblePosition.y+=TerrainVisualSurface.Active.Sample(visiblePosition.x,visiblePosition.z);
        currentGhost.transform.position=visiblePosition;

        var preview = FogOrderRules.Preview(entityManager, localPlayerId, currentSnappedPosition, currentLocalRect);
        bool affordable = CanAffordBuilding(selectedBuildingPrefab);
        if (preview == BuildingPreviewState.Valid && !affordable) preview = BuildingPreviewState.Invalid;
        currentCanPlace = preview != BuildingPreviewState.Invalid && affordable;
        SetGhostMaterial(preview);
        currentGhost.SetActive(true);
    }

    private Vector3 SnapToGrid(Vector3 hitPoint)
    {
        if (entityManager != default)
        {
            using var query = entityManager.CreateEntityQuery(typeof(GridComponent));
            if (!query.IsEmpty)
            {
                var grid = query.GetSingleton<GridComponent>();
                if (selectedBuildingPrefab != Entity.Null && entityManager.HasComponent<GridFootprint>(selectedBuildingPrefab))
                    return (Vector3)GridSnapperMath.Snap(new float3(hitPoint.x, 0, hitPoint.z), grid,
                        entityManager.GetComponentData<GridFootprint>(selectedBuildingPrefab).Cells);
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

    private bool CanAffordBuilding(Entity buildingPrefab)
    {
        if (buildingPrefab == Entity.Null || localPlayerId < 0 || entityManager == default
            || !entityManager.Exists(buildingPrefab) || !entityManager.HasBuffer<EntityResourceCost>(buildingPrefab)) return false;
        using var costs = new NativeList<ResourcePair>(Allocator.Temp);
        foreach (var cost in entityManager.GetBuffer<EntityResourceCost>(buildingPrefab))
            costs.Add(new ResourcePair(cost.Type, cost.Amount));
        return ResourcePayment.CanAfford(entityManager, localPlayerId, costs);
    }
    private void PlaceBuilding(Vector3 rootPosition, int playerId)
    {
        if (selectedBuildingPrefab == Entity.Null || entityManager == default)
            return;

        List<Entity> sources = SelectHelper.GetAllSelectedEntitiesByplayerID(playerId);
        CommandDataHelper.AddPlanBuildToQueue(entityManager, playerId, sources, selectedBuildingPrefab, rootPosition);
        CancelPlacement();
    }

    private Entity GetBuildingPrefabEntityFromRegistry(FixedString64Bytes buildingId)
    {
        if (entityManager == default)
            return Entity.Null;

        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GameDataRegistryComponent>(),
            ComponentType.ReadOnly<RegistryBlobElement>(),
            ComponentType.ReadOnly<RegistryPrefabElement>()
        );

        if (query.IsEmpty)
            return Entity.Null;

        Entity regEntity = query.GetSingletonEntity();
        var blobBuffer = entityManager.GetBuffer<RegistryBlobElement>(regEntity);
        var prefabBuffer = entityManager.GetBuffer<RegistryPrefabElement>(regEntity);

        var blobRef = blobBuffer.GetBlobByID<BuildingBlob>(buildingId, out var result);
        if (result != FunctionResult.Success)
            return Entity.Null;

        int pIndex = blobRef.Value.Base.PrefabIndex;
        if (pIndex >= 0 && pIndex < prefabBuffer.Length)
        {
            return prefabBuffer[pIndex].Prefab;
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
        currentBuildingId = default;
        selectedBuildingPrefab = Entity.Null;
    }

    private void SetGhostMaterial(BuildingPreviewState preview)
    {
        if (currentGhostRenderers == null) return;

        Material targetMat = preview == BuildingPreviewState.Valid ? validGhostMaterial : invalidGhostMaterial;
        if (preview == BuildingPreviewState.Unknown) targetMat = unknownGhostMaterial;
        if (targetMat == null) return;

        foreach (var r in currentGhostRenderers)
        {
            if (r != null)
                r.sharedMaterial = targetMat;
        }
    }

    private void OnDestroy()
    {
        CancelPlacement();
        if (_instance == this) _instance = null;
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}

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
    private World presentationWorld;
    private ulong presentationWorldSequence;
    private bool isPlacing;
    private FixedString64Bytes currentBuildingId;
    private Entity selectedBuildingPrefab;
    private Entity sourceWorkerEntity;
    private int localPlayerId = -1;

    private GameObject currentGhost;
    private Renderer[] currentGhostRenderers;
    private Material fallbackGhostMaterial;
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
        RefreshWorld();

        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");
    }

    private void Update()
    {
        if (!RefreshWorld()) { CancelPlacement(); return; }
        if (!isPlacing || currentGhost == null)
            return;
        if (!TryGetReadyPlayer(localPlayerId) || !entityManager.Exists(sourceWorkerEntity)
            || !CommandAuthority.CanControl(entityManager, localPlayerId, sourceWorkerEntity))
        { CancelPlacement(); return; }

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
        if (!RefreshWorld() || !TryGetReadyPlayer(playerID) || sourceEntity == Entity.Null
            || !entityManager.Exists(sourceEntity) || !CommandAuthority.CanControl(entityManager, playerID, sourceEntity))
            return;

        if (entityManager.HasBuffer<BuildOffer>(sourceEntity))
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
        if (!RefreshWorld() || !TryGetReadyPlayer(playerId) || sourceWorker == Entity.Null
            || !entityManager.Exists(sourceWorker) || !CommandAuthority.CanControl(entityManager, playerId, sourceWorker)
            || !entityManager.HasBuffer<BuildOffer>(sourceWorker)) return;
        bool offered = false;
        foreach (var offer in entityManager.GetBuffer<BuildOffer>(sourceWorker))
            if (offer.DefinitionID == buildingId) { offered = true; break; }
        if (!offered) return;

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

        GameObject previewSource = EntityPresentation.BuildingPreview(entityManager, buildingId);
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
            if (validGhostMaterial == null && fallbackGhostMaterial == null && urpShader != null)
                fallbackGhostMaterial = new Material(urpShader);
            newMr.sharedMaterial = validGhostMaterial != null ? validGhostMaterial : fallbackGhostMaterial;
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

        var preview = BuildingPlacementPreview.Evaluate(entityManager, localPlayerId, currentSnappedPosition, currentLocalRect);
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
        if (!RefreshWorld() || !TryGetReadyPlayer(playerId) || selectedBuildingPrefab == Entity.Null
            || !entityManager.Exists(selectedBuildingPrefab))
            return;

        List<Entity> sources = SelectHelper.GetAllSelectedEntitiesByplayerID(playerId);
        CommandDataHelper.AddPlanBuildToQueue(entityManager, playerId, sources, selectedBuildingPrefab, rootPosition);
        CancelPlacement();
    }

    private Entity GetBuildingPrefabEntityFromRegistry(FixedString64Bytes buildingId)
    {
        if (!ClientPresentationCatalog.TryGet(entityManager, out var regEntity, out _))
            return Entity.Null;

        var blobBuffer = entityManager.GetBuffer<RegistryBlobElement>(regEntity);
        var prefabBuffer = entityManager.GetBuffer<RegistryPrefabElement>(regEntity);

        var blobRef = blobBuffer.GetBlobByID<BuildingBlob>(buildingId, out var result);
        if (result != FunctionResult.Success)
            return Entity.Null;

        int pIndex = blobRef.Value.Base.PrefabIndex;
        if (pIndex >= 0 && pIndex < prefabBuffer.Length)
        {
            var prefab = prefabBuffer[pIndex].Prefab;
            return entityManager.Exists(prefab) ? prefab : Entity.Null;
        }

        return Entity.Null;
    }

    public void CancelPlacement()
    {
        if (currentGhost != null)
            Destroy(currentGhost);

        currentGhost = null;
        if (fallbackGhostMaterial != null) Destroy(fallbackGhostMaterial);
        fallbackGhostMaterial = null;
        currentGhostRenderers = null;
        isPlacing = false;
        currentBuildingId = default;
        selectedBuildingPrefab = Entity.Null;
        sourceWorkerEntity = Entity.Null;
        localPlayerId = -1;
        currentLocalRect = default;
        currentCanPlace = false;
    }

    private bool RefreshWorld()
    {
        if (!ClientPresentationCatalog.TryGetCurrent(out var world, out var em))
        {
            CancelPlacement();
            presentationWorld = null;
            presentationWorldSequence = 0;
            entityManager = default;
            return false;
        }
        if (!ReferenceEquals(presentationWorld, world) || presentationWorldSequence != world.SequenceNumber)
        {
            CancelPlacement();
            presentationWorld = world;
            presentationWorldSequence = world.SequenceNumber;
            entityManager = em;
        }
        return ClientPresentationCatalog.IsReady(entityManager);
    }

    private bool TryGetReadyPlayer(int expectedId)
        => expectedId >= 0 && SelectHelper.TryGetLocalPlayerId(out int currentId) && currentId == expectedId;

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

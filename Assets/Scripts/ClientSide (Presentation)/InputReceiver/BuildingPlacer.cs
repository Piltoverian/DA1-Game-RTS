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

    [Header("Pathfinding Cost")]
    public int buildingObstacleCost = int.MaxValue;
    public float buildingCostPadding = 0.01f;

    [Header("Footprint Collider")]
    [Tooltip("Nếu true, hệ thống sẽ ưu tiên BoxCollider có tên chứa 'Placement' để lấy size đặt nhà.")]
    public bool preferPlacementNamedCollider = true;

    [Tooltip("Nếu không có PlacementCollider, hệ thống sẽ dùng BoxCollider có diện tích XZ lớn nhất.")]
    public bool useLargestBoxColliderWhenNoPlacementCollider = true;

    [Header("Debug")]
    public bool logConstructionDebug = true;

    private GameObject currentGhost;
    private Renderer[] currentGhostRenderers;

    private bool isPlacing;
    private bool isEntityManagerReady;

    private EntityManager entityManager;

    private Entity selectedBuildingPrefab;
    private GameObject selectedPreviewPrefab;

    private Vector3 currentSnappedPosition;
    private bool currentCanPlace;

    private bool hasAppliedGhostMaterial;
    private bool lastGhostCanPlace;

    private int selectedCommandIndex = -1;

    private struct BuildingFootprint
    {
        public Vector3 CenterOffset;
        public Vector3 HalfExtents;

        public Vector3 GetWorldCenter(Vector3 rootPosition)
        {
            return rootPosition + CenterOffset;
        }
    }

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
            // Debug.LogError("Không tìm thấy BuildingPrefabData. Kiểm tra BuildingPrefabAuthoring trong SubScene.");
            return;
        }

        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");

        if (placementBlockMask.value == 0)
            placementBlockMask = LayerMask.GetMask("Building", "Obstacle");
    }

    private void Update()
    {
        if (!isPlacing || currentGhost == null)
            return;

        UpdatePlacementPreview();

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("Click blocked by UI");
            return;
        }

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
        selectedBuildingPrefab = GetBuildingPrefabEntityByCommandIndex(selectedCommandIndex);
        selectedPreviewPrefab = definition.PreviewPrefab;

        if (selectedBuildingPrefab == Entity.Null)
        {
            Debug.LogError("Selected building ECS prefab is null. CommandIndex = " + selectedCommandIndex);
            return;
        }

        StartPlacing();
    }

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
        DisableGhostHelperObjects(ghost);

        int ghostLayer = LayerMask.NameToLayer("Ghost");

        if (ghostLayer >= 0)
            SetLayerRecursively(ghost, ghostLayer);
        else
            Debug.LogWarning("Layer 'Ghost' does not exist.");

        Collider[] colliders = ghost.GetComponentsInChildren<Collider>(true);

        foreach (Collider col in colliders)
            col.enabled = false;

        currentGhostRenderers = ghost.GetComponentsInChildren<Renderer>(true);

        if (logConstructionDebug)
            Debug.Log("Ghost renderer count: " + currentGhostRenderers.Length);

        SetGhostMaterial(true, true);
    }

    private void DisableGhostHelperObjects(GameObject ghost)
    {
        Transform[] children = ghost.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            string objectName = child.name.ToLower();

            if (objectName.Contains("selected") ||
                objectName.Contains("selection") ||
                objectName.Contains("outline") ||
                objectName.Contains("highlight"))
            {
                child.gameObject.SetActive(false);
            }
        }
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

        BuildingFootprint footprint = GetSelectedBuildingFootprint();
        Vector3 footprintCenter = footprint.GetWorldCenter(currentSnappedPosition);

        currentCanPlace = CanPlace(footprintCenter, footprint.HalfExtents);

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

    private BuildingFootprint GetSelectedBuildingFootprint()
    {
        if (TryGetBoxColliderFootprint(selectedPreviewPrefab, out BuildingFootprint footprint))
            return footprint;

        return GetBuildingDataFootprint(selectedBuildingPrefab);
    }

    private bool TryGetBoxColliderFootprint(GameObject prefab, out BuildingFootprint footprint)
    {
        footprint = new BuildingFootprint
        {
            CenterOffset = Vector3.zero,
            HalfExtents = defaultHalfExtents
        };

        if (prefab == null)
            return false;

        BoxCollider selectedCollider = GetPlacementBoxCollider(prefab);

        if (selectedCollider == null)
            return false;

        CalculateBoxColliderLocalBounds(
            prefab.transform,
            selectedCollider,
            out Vector3 localMin,
            out Vector3 localMax
        );

        Vector3 localSize = localMax - localMin;
        Vector3 localCenter = (localMin + localMax) * 0.5f;

        footprint.CenterOffset = localCenter;
        footprint.HalfExtents = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(localSize.x) * 0.5f),
            Mathf.Max(0.01f, Mathf.Abs(localSize.y) * 0.5f),
            Mathf.Max(0.01f, Mathf.Abs(localSize.z) * 0.5f)
        );

        return true;
    }

    private BoxCollider GetPlacementBoxCollider(GameObject prefab)
    {
        BoxCollider[] boxes = prefab.GetComponentsInChildren<BoxCollider>(true);

        if (boxes == null || boxes.Length == 0)
            return null;

        if (preferPlacementNamedCollider)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] == null)
                    continue;

                string objectName = boxes[i].name.ToLower();

                if (objectName.Contains("placement") || objectName.Contains("footprint"))
                    return boxes[i];
            }
        }

        if (!useLargestBoxColliderWhenNoPlacementCollider)
            return boxes[0];

        BoxCollider best = boxes[0];
        float bestArea = GetColliderXZArea(best);

        for (int i = 1; i < boxes.Length; i++)
        {
            float area = GetColliderXZArea(boxes[i]);

            if (area > bestArea)
            {
                bestArea = area;
                best = boxes[i];
            }
        }

        return best;
    }

    private float GetColliderXZArea(BoxCollider box)
    {
        if (box == null)
            return 0f;

        Vector3 scale = box.transform.lossyScale;

        float sizeX = Mathf.Abs(box.size.x * scale.x);
        float sizeZ = Mathf.Abs(box.size.z * scale.z);

        return sizeX * sizeZ;
    }

    private void CalculateBoxColliderLocalBounds(
        Transform root,
        BoxCollider box,
        out Vector3 localMin,
        out Vector3 localMax)
    {
        Vector3 half = box.size * 0.5f;

        localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCornerInCollider = box.center + new Vector3(
                        half.x * x,
                        half.y * y,
                        half.z * z
                    );

                    Vector3 worldCorner = box.transform.TransformPoint(localCornerInCollider);
                    Vector3 rootLocalCorner = root.InverseTransformPoint(worldCorner);

                    localMin = Vector3.Min(localMin, rootLocalCorner);
                    localMax = Vector3.Max(localMax, rootLocalCorner);
                }
            }
        }
    }

    private BuildingFootprint GetBuildingDataFootprint(Entity prefab)
    {
        if (!isEntityManagerReady)
        {
            return new BuildingFootprint
            {
                CenterOffset = Vector3.zero,
                HalfExtents = defaultHalfExtents
            };
        }

        if (prefab == Entity.Null)
        {
            return new BuildingFootprint
            {
                CenterOffset = Vector3.zero,
                HalfExtents = defaultHalfExtents
            };
        }

        if (!entityManager.HasComponent<BuildingData>(prefab))
        {
            return new BuildingFootprint
            {
                CenterOffset = Vector3.zero,
                HalfExtents = defaultHalfExtents
            };
        }

        BuildingData data = entityManager.GetComponentData<BuildingData>(prefab);

        return new BuildingFootprint
        {
            CenterOffset = new Vector3(0f, data.BlockerHeight * 0.5f, 0f),
            HalfExtents = new Vector3(
                data.FootprintSizeX * 0.5f,
                data.BlockerHeight * 0.5f,
                data.FootprintSizeZ * 0.5f
            )
        };
    }

    private bool CanPlace(Vector3 footprintCenter, Vector3 halfExtents)
    {
        if (HasPlacementCollision(footprintCenter, halfExtents))
            return false;

        EntityQuery gridQuery = entityManager.CreateEntityQuery(typeof(GridComponent));

        if (gridQuery.IsEmpty)
            return false;

        GridComponent grid = gridQuery.GetSingleton<GridComponent>();

        float padding = 0.01f;

        if (grid.cellsize > 0f)
            padding = Mathf.Min(padding, grid.cellsize * 0.45f);

        float minX = footprintCenter.x - halfExtents.x + padding;
        float minZ = footprintCenter.z - halfExtents.z + padding;
        float maxX = footprintCenter.x + halfExtents.x - padding;
        float maxZ = footprintCenter.z + halfExtents.z - padding;

        if (minX > maxX)
        {
            minX = footprintCenter.x - halfExtents.x;
            maxX = footprintCenter.x + halfExtents.x;
        }

        if (minZ > maxZ)
        {
            minZ = footprintCenter.z - halfExtents.z;
            maxZ = footprintCenter.z + halfExtents.z;
        }

        int2 minGrid = GridHelper.WorldToGrid(new float3(minX, 0, minZ), grid);
        int2 maxGrid = GridHelper.WorldToGrid(new float3(maxX, 0, maxZ), grid);

        EntityQuery bucketQuery = entityManager.CreateEntityQuery(typeof(MovementAgentBucket));
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

                int gridIndex = GridHelper.GetNodeIndex(new int2(x, y), grid);

                if (hasUnitBucket && unitBucket.ContainsKey(gridIndex))
                    return false;
            }
        }

        return true;
    }

    private bool HasPlacementCollision(Vector3 footprintCenter, Vector3 halfExtents)
    {
        Vector3 checkHalfExtents = new Vector3(
            Mathf.Max(0.01f, halfExtents.x + placementCheckPadding),
            Mathf.Max(0.01f, halfExtents.y + placementCheckPadding),
            Mathf.Max(0.01f, halfExtents.z + placementCheckPadding)
        );

        Collider[] hits = Physics.OverlapBox(
            footprintCenter,
            checkHalfExtents,
            Quaternion.identity,
            placementBlockMask,
            QueryTriggerInteraction.Collide
        );

        if (hits.Length > 0)
        {
            if (logPlacementBlocking)
                Debug.Log("Cannot place. Blocked by: " + hits[0].name);

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
        if (prefab == Entity.Null)
            return;

        if (!entityManager.HasComponent<BuildingData>(prefab))
            return;

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

    private void PlaceBuilding(Vector3 rootPosition)
    {
        BuildingFootprint footprint = GetSelectedBuildingFootprint();
        Vector3 footprintCenter = footprint.GetWorldCenter(rootPosition);

        if (currentGhost != null)
            Destroy(currentGhost);

        currentGhost = null;
        currentGhostRenderers = null;
        isPlacing = false;

        Entity building = entityManager.Instantiate(selectedBuildingPrefab);

        if (entityManager.HasComponent<LocalTransform>(building))
        {
            LocalTransform transform =
                entityManager.GetComponentData<LocalTransform>(building);

            transform.Position = new float3(rootPosition.x, rootPosition.y, rootPosition.z);

            entityManager.SetComponentData(building, transform);
        }
        else
        {
            entityManager.AddComponentData(
                building,
                LocalTransform.FromPosition(new float3(rootPosition.x, rootPosition.y, rootPosition.z))
            );
        }

        ApplyFootprintToPlacedBuilding(building, footprint);
        ResetConstructionState(building);

        if (logConstructionDebug)
            DebugConstructionState(building, "After PlaceBuilding");

        CreateBuildingBlocker(footprintCenter, footprint.HalfExtents, building);
        SendBuildingCostChangeRequest(footprintCenter, footprint.HalfExtents, buildingObstacleCost);
    }

    private void ApplyFootprintToPlacedBuilding(Entity building, BuildingFootprint footprint)
    {
        BuildingCostArea costArea = new BuildingCostArea
        {
            CenterOffset = new float3(
                footprint.CenterOffset.x,
                footprint.CenterOffset.y,
                footprint.CenterOffset.z
            ),
            HalfExtents = new float3(
                footprint.HalfExtents.x,
                footprint.HalfExtents.y,
                footprint.HalfExtents.z
            )
        };

        if (entityManager.HasComponent<BuildingCostArea>(building))
            entityManager.SetComponentData(building, costArea);
        else
            entityManager.AddComponentData(building, costArea);

        if (entityManager.HasComponent<BuildingData>(building))
        {
            BuildingData data = entityManager.GetComponentData<BuildingData>(building);

            data.FootprintSizeX = footprint.HalfExtents.x * 2f;
            data.FootprintSizeZ = footprint.HalfExtents.z * 2f;
            data.BlockerHeight = footprint.HalfExtents.y * 2f;

            entityManager.SetComponentData(building, data);
        }
    }

    private void ResetConstructionState(Entity building)
    {
        if (!entityManager.HasComponent<BuildingData>(building))
        {
            Debug.LogWarning("Placed building has no BuildingData.");
            return;
        }

        BuildingData data = entityManager.GetComponentData<BuildingData>(building);

        ConstructionData con;

        if (entityManager.HasComponent<ConstructionData>(building))
        {
            con = entityManager.GetComponentData<ConstructionData>(building);

            con.Elapsed = 0f;

            if (con.TotalTime <= 0f)
                con.TotalTime = Mathf.Max(0.1f, data.ConstructionTime);

            entityManager.SetComponentData(building, con);
        }
        else
        {
            con = new ConstructionData
            {
                TotalTime = Mathf.Max(0.1f, data.ConstructionTime),
                Elapsed = 0f,
                StartRevealHeight = 0f,
                EndRevealHeight = data.BlockerHeight + 2f
            };

            entityManager.AddComponentData(building, con);
        }

        if (!entityManager.HasComponent<UnderConstructionTag>(building))
            entityManager.AddComponent<UnderConstructionTag>(building);

        RevealHeightProperty reveal = new RevealHeightProperty
        {
            Value = con.StartRevealHeight
        };

        if (entityManager.HasComponent<RevealHeightProperty>(building))
            entityManager.SetComponentData(building, reveal);
        else
            entityManager.AddComponentData(building, reveal);
    }

    private void DebugConstructionState(Entity building, string label)
    {
        Debug.Log(
            $"[{label}] Entity = {building}\n" +
            $"Has BuildingData = {entityManager.HasComponent<BuildingData>(building)}\n" +
            $"Has BuildingCostArea = {entityManager.HasComponent<BuildingCostArea>(building)}\n" +
            $"Has ConstructionData = {entityManager.HasComponent<ConstructionData>(building)}\n" +
            $"Has RevealHeightProperty = {entityManager.HasComponent<RevealHeightProperty>(building)}\n" +
            $"Has UnderConstructionTag = {entityManager.HasComponent<UnderConstructionTag>(building)}"
        );

        if (entityManager.HasComponent<ConstructionData>(building))
        {
            ConstructionData con = entityManager.GetComponentData<ConstructionData>(building);

            Debug.Log(
                $"ConstructionData: " +
                $"TotalTime={con.TotalTime}, " +
                $"Elapsed={con.Elapsed}, " +
                $"StartReveal={con.StartRevealHeight}, " +
                $"EndReveal={con.EndRevealHeight}"
            );
        }

        if (entityManager.HasComponent<RevealHeightProperty>(building))
        {
            RevealHeightProperty reveal =
                entityManager.GetComponentData<RevealHeightProperty>(building);

            Debug.Log("RevealHeightProperty Value = " + reveal.Value);
        }

        if (entityManager.HasComponent<BuildingCostArea>(building))
        {
            BuildingCostArea area = entityManager.GetComponentData<BuildingCostArea>(building);

            Debug.Log(
                $"BuildingCostArea: " +
                $"CenterOffset={area.CenterOffset}, " +
                $"HalfExtents={area.HalfExtents}"
            );
        }
    }

    private void CreateBuildingBlocker(Vector3 footprintCenter, Vector3 halfExtents, Entity buildingEntity)
    {
        GameObject blocker = new GameObject("BuildingBlocker");

        int buildingLayer = LayerMask.NameToLayer("Building");

        if (buildingLayer >= 0)
            blocker.layer = buildingLayer;
        else
            Debug.LogWarning("Layer 'Building' does not exist.");

        blocker.transform.position = footprintCenter;

        BoxCollider col = blocker.AddComponent<BoxCollider>();
        col.size = halfExtents * 2f;
        col.center = Vector3.zero;
        col.isTrigger = false;

        BuildingBlocker buildingBlocker = blocker.AddComponent<BuildingBlocker>();
        buildingBlocker.BuildingEntity = buildingEntity;
    }

    private void SendBuildingCostChangeRequest(Vector3 footprintCenter, Vector3 halfExtents, int newCost)
    {
        if (!isEntityManagerReady)
            return;

        EntityQuery gridQuery = entityManager.CreateEntityQuery(
            typeof(GridComponent),
            typeof(CostChangeRequest)
        );

        if (gridQuery.IsEmpty)
        {
            Debug.LogWarning("Cannot send building cost request. GridComponent or CostChangeRequest buffer not found.");
            return;
        }

        Entity gridEntity = gridQuery.GetSingletonEntity();

        if (!entityManager.HasBuffer<CostChangeRequest>(gridEntity))
        {
            Debug.LogWarning("Grid entity has no CostChangeRequest buffer.");
            return;
        }

        GridComponent grid = entityManager.GetComponentData<GridComponent>(gridEntity);

        float padding = Mathf.Max(0f, buildingCostPadding);

        if (grid.cellsize > 0f)
            padding = Mathf.Min(padding, grid.cellsize * 0.45f);

        float minX = footprintCenter.x - halfExtents.x + padding;
        float minZ = footprintCenter.z - halfExtents.z + padding;
        float maxX = footprintCenter.x + halfExtents.x - padding;
        float maxZ = footprintCenter.z + halfExtents.z - padding;

        if (minX > maxX)
        {
            minX = footprintCenter.x - halfExtents.x;
            maxX = footprintCenter.x + halfExtents.x;
        }

        if (minZ > maxZ)
        {
            minZ = footprintCenter.z - halfExtents.z;
            maxZ = footprintCenter.z + halfExtents.z;
        }

        StartEndRect area = new StartEndRect(new float2(minX, minZ));
        area.ExpandTo(new float2(maxX, maxZ));

        DynamicBuffer<CostChangeRequest> requestBuffer =
            entityManager.GetBuffer<CostChangeRequest>(gridEntity);

        requestBuffer.Add(new CostChangeRequest
        {
            newCost = newCost,
            area = area
        });

        if (logConstructionDebug)
        {
            Debug.Log(
                $"Building cost request sent. " +
                $"Cost={newCost}, " +
                $"Area=({minX:F2},{minZ:F2}) -> ({maxX:F2},{maxZ:F2})"
            );
        }
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
        selectedCommandIndex = -1;

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

            Material[] newMaterials = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < newMaterials.Length; i++)
                newMaterials[i] = targetMaterial;

            renderer.materials = newMaterials;
        }

        hasAppliedGhostMaterial = true;
        lastGhostCanPlace = canPlace;
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        if (!isPlacing || currentGhost == null)
            return;

        BuildingFootprint footprint = GetSelectedBuildingFootprint();
        Vector3 footprintCenter = footprint.GetWorldCenter(currentSnappedPosition);

        Gizmos.color = currentCanPlace
            ? new Color(0f, 1f, 0f, 0.25f)
            : new Color(1f, 0f, 0f, 0.25f);

        Gizmos.DrawCube(
            footprintCenter,
            footprint.HalfExtents * 2f
        );

        Gizmos.color = Color.white;

        Gizmos.DrawWireCube(
            footprintCenter,
            footprint.HalfExtents * 2f
        );
    }
}

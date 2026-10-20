using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>North-up tactical overview from the live ECS grid. No extra camera/render pass.</summary>
[AddComponentMenu("UI/Tactical Minimap")]
public class TacticalMinimap : MaskableGraphic, IPointerDownHandler, IDragHandler
{
    [SerializeField, Range(16, 64)] private int terrainSamples = 48;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;
    [SerializeField] private Color groundColor = new Color(0.12f, 0.22f, 0.17f, 1f);
    [SerializeField] private Color highGroundColor = new Color(0.36f, 0.43f, 0.28f, 1f);
    [SerializeField] private Color rockColor = new Color(0.3f, 0.32f, 0.31f, 1f);
    [SerializeField] private Color allyColor = new Color(0.35f, 0.92f, 0.8f, 1f);
    [SerializeField] private Color enemyColor = new Color(1f, 0.4f, 0.35f, 1f);
    [SerializeField] private Color resourceColor = new Color(0.96f, 0.76f, 0.36f, 1f);
    private const int MaximumMarkers = 2048;
    private struct Marker { public Vector2 UV; public Color Color; public float Size; public bool Selected; }
    private readonly List<Marker> markers = new();
    private readonly Dictionary<int, int> teams = new();
    private readonly Vector2[] cameraCorners = new Vector2[4];
    private bool hasCamera;
    private Color[] terrain;
    private World world;
    private EntityQuery gridQuery, objectsQuery, nodesQuery, playersQuery;
    private GridComponent grid;
    private Entity gridEntity;
    private uint terrainGeneration;
    private int sampledResolution;
    private float nextRefresh;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = true;
        nextRefresh = 0f;
    }

    private void Update()
    {
        var current = World.DefaultGameObjectInjectionWorld;
        if (current != world)
        {
            ReleaseWorld();
            if (current != null && current.IsCreated)
            {
                world = current;
                var em = world.EntityManager;
                gridQuery = em.CreateEntityQuery(typeof(GridComponent), typeof(GridTerrain));
                objectsQuery = em.CreateEntityQuery(typeof(LocalTransform), typeof(EntityOwner));
                nodesQuery = em.CreateEntityQuery(typeof(LocalTransform), typeof(ResourceNodeData), typeof(ResourceNodeTag));
                playersQuery = em.CreateEntityQuery(typeof(PlayerContext));
            }
        }
        if (world == null || !world.IsCreated) { ClearMap(); return; }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        if (gridQuery.CalculateEntityCount() != 1) { ClearMap(); return; }
        var manager = world.EntityManager;
        var entity = gridQuery.GetSingletonEntity();
        var currentGrid = manager.GetComponentData<GridComponent>(entity);
        if (!currentGrid.initialized || currentGrid.width <= 0 || currentGrid.height <= 0 ||
            !IsFinite(currentGrid.cellsize) || currentGrid.cellsize <= 0f ||
            !IsFinite(currentGrid.origin.x) || !IsFinite(currentGrid.origin.z)) { ClearMap(); return; }
        bool rebuildTerrain = terrain == null || entity != gridEntity || currentGrid.generation != terrainGeneration ||
            grid.width != currentGrid.width || grid.height != currentGrid.height || sampledResolution != terrainSamples;
        grid = currentGrid;
        if (rebuildTerrain)
        {
            var cells = manager.GetBuffer<GridTerrain>(entity, true);
            if ((long)grid.width * grid.height > cells.Length) { ClearMap(); return; }
            sampledResolution = Mathf.Clamp(terrainSamples, 16, 64);
            terrain = new Color[sampledResolution * sampledResolution];
            for (int y = 0; y < sampledResolution; y++)
            for (int x = 0; x < sampledResolution; x++)
            {
                int gx = Mathf.Min(grid.width - 1, x * grid.width / sampledResolution);
                int gy = Mathf.Min(grid.height - 1, y * grid.height / sampledResolution);
                var cell = cells[gy * grid.width + gx];
                terrain[y * sampledResolution + x] = cell.isMountain || cell.isCliff ? rockColor :
                    Color.Lerp(groundColor, highGroundColor, Mathf.Clamp01(cell.heightLevel / 4f));
            }
            gridEntity = entity;
            terrainGeneration = grid.generation;
        }
        markers.Clear();
        bool hasPlayer = SelectHelper.TryGetLocalPlayerId(out int player);
        // During bootstrap show terrain only, never mislabel every object as an enemy.
        if (hasPlayer)
        {
            teams.Clear();
            using (var players = playersQuery.ToComponentDataArray<PlayerContext>(Allocator.Temp))
                foreach (var context in players) teams[context.PlayerId] = context.TeamID;
            int localTeam = teams.TryGetValue(player, out int team) ? team : -1;
            using var objects = objectsQuery.ToEntityArray(Allocator.Temp);
            foreach (var obj in objects)
            {
                if (markers.Count >= MaximumMarkers) break;
                bool building = manager.HasComponent<BuildingComponent>(obj);
                if (!building && !manager.HasComponent<UnitComponent>(obj)) continue;
                if (manager.HasComponent<EntityHealth>(obj) && manager.GetComponentData<EntityHealth>(obj).CurrentHP <= 0) continue;
                int owner = manager.GetComponentData<EntityOwner>(obj).PlayerID;
                bool selected = manager.HasComponent<Selected>(obj) && manager.IsComponentEnabled<Selected>(obj);
                Vector3 p = manager.GetComponentData<LocalTransform>(obj).Position;
                bool allied = owner == player || (localTeam >= 0 && teams.TryGetValue(owner, out int otherTeam) && localTeam == otherTeam);
                AddMarker(p, allied ? allyColor : owner < 0 ? Color.gray : enemyColor, building ? 5f : 3f, selected);
            }
            using var nodes = nodesQuery.ToEntityArray(Allocator.Temp);
            foreach (var node in nodes)
            {
                if (markers.Count >= MaximumMarkers) break;
                if (manager.HasComponent<ResourceNodePendingConfig>(node) || manager.GetComponentData<ResourceNodeData>(node).Amount <= 0) continue;
                Vector3 p = manager.GetComponentData<LocalTransform>(node).Position;
                if (manager.HasComponent<BlockageData>(node)) p = manager.GetComponentData<BlockageData>(node).GetWorldCenter(p);
                AddMarker(p, resourceColor, 2.5f, false);
            }
        }
        RefreshCamera();
        SetVerticesDirty();
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private Vector2 WorldUV(Vector3 position) => new Vector2(
        (position.x - grid.origin.x) / (grid.width * grid.cellsize),
        (position.z - grid.origin.z) / (grid.height * grid.cellsize));
    private void AddMarker(Vector3 position, Color tint, float size, bool selected)
    {
        Vector2 uv = WorldUV(position);
        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1 || !IsFinite(uv.x) || !IsFinite(uv.y)) return;
        markers.Add(new Marker { UV = uv, Color = tint, Size = size, Selected = selected });
    }
    private void RefreshCamera()
    {
        hasCamera = false;
        var view = Camera.main;
        if (view == null) return;
        var plane = new Plane(Vector3.up, new Vector3(0, grid.origin.y, 0));
        for (int i = 0; i < 4; i++)
        {
            var ray = view.ViewportPointToRay(new Vector3(i == 1 || i == 2 ? 1 : 0, i >= 2 ? 1 : 0, 0));
            if (!plane.Raycast(ray, out float distance)) return;
            cameraCorners[i] = WorldUV(ray.GetPoint(distance));
        }
        hasCamera = true;
    }
    private Rect MapRect()
    {
        Rect r = rectTransform.rect;
        r = new Rect(r.x + 6, r.y + 6, Mathf.Max(0, r.width - 12), Mathf.Max(0, r.height - 12));
        if (grid.width <= 0 || grid.height <= 0) return r;
        float aspect = (float)grid.width / grid.height;
        if (r.width > r.height * aspect) { float w = r.height * aspect; r.x += (r.width - w) * .5f; r.width = w; }
        else { float h = r.width / aspect; r.y += (r.height - h) * .5f; r.height = h; }
        return r;
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect full = rectTransform.rect;
        Quad(vh, full, new Color(.04f, .085f, .075f, 1f));
        if (terrain == null) return;
        Rect r = MapRect();
        float w = r.width / sampledResolution, h = r.height / sampledResolution;
        for (int y = 0; y < sampledResolution; y++)
        for (int x = 0; x < sampledResolution; x++)
            Quad(vh, new Rect(r.x + x * w, r.y + y * h, w, h), terrain[y * sampledResolution + x]);
        foreach (var marker in markers)
        {
            Vector2 p = Point(r, marker.UV);
            float size = marker.Size;
            if (marker.Selected) Quad(vh, Clipped(new Rect(p.x - size, p.y - size, size * 2, size * 2), r), Color.white);
            Quad(vh, Clipped(new Rect(p.x - size * .5f, p.y - size * .5f, size, size), r), marker.Color);
        }
        if (hasCamera) for (int i = 0; i < 4; i++) Line(vh, Point(r, cameraCorners[i]), Point(r, cameraCorners[(i + 1) % 4]), Color.white);
    }
    private static Vector2 Point(Rect r, Vector2 uv) => new Vector2(r.x + Mathf.Clamp01(uv.x) * r.width, r.y + Mathf.Clamp01(uv.y) * r.height);
    private static Rect Clipped(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
    private static void Quad(VertexHelper vh, Rect r, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin, r.yMin), tint, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMax), tint, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMax), tint, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMin), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, Color tint)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * .75f;
        int start = vh.currentVertCount;
        vh.AddVert(a - normal, tint, Vector2.zero); vh.AddVert(a + normal, tint, Vector2.zero);
        vh.AddVert(b + normal, tint, Vector2.zero); vh.AddVert(b - normal, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
    public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Navigate(data); }
    public void OnDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Navigate(data); }
    private void Navigate(PointerEventData data)
    {
        if (terrain == null || GameplayInputGate.BlocksWorldInput) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out var point)) return;
        Rect r = MapRect();
        if (r.width <= 0 || r.height <= 0 || !r.Contains(point)) return;
        var view = Camera.main;
        if (view == null) return;
        var plane = new Plane(Vector3.up, new Vector3(0, grid.origin.y, 0));
        var ray = view.ViewportPointToRay(new Vector3(.5f, .5f, 0));
        if (!plane.Raycast(ray, out float distance)) return;
        Vector3 focus = ray.GetPoint(distance);
        Vector3 target = new Vector3(grid.origin.x + (point.x - r.x) / r.width * grid.width * grid.cellsize,
            focus.y, grid.origin.z + (point.y - r.y) / r.height * grid.height * grid.cellsize);
        view.transform.position += target - focus;
        RefreshCamera(); SetVerticesDirty();
    }
    private void ClearMap()
    {
        if (terrain == null && markers.Count == 0) return;
        terrain = null; markers.Clear(); hasCamera = false; SetVerticesDirty();
    }
    private void ReleaseWorld()
    {
        if (world != null && world.IsCreated) { gridQuery.Dispose(); objectsQuery.Dispose(); nodesQuery.Dispose(); playersQuery.Dispose(); }
        world = null; gridEntity = Entity.Null; ClearMap(); nextRefresh = 0f;
    }
    protected override void OnDisable() { ReleaseWorld(); base.OnDisable(); }
}

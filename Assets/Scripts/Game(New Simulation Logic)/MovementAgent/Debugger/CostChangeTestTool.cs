using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Test tool cho Dynamic FlowField Phase 1.
/// Gắn lên bất kỳ GameObject nào trong scene.
/// 
/// Hotkeys:
///   T + Click chuột trái = Đặt obstacle (int.MaxValue) tại vị trí chuột (3×3 cells)
///   Y + Click chuột trái = Xóa obstacle (cost = 1) tại vị trí chuột (3×3 cells)
///
/// Cần: Camera.main nhìn xuống mặt phẳng XZ, Ground layer trên grid object.
/// </summary>
public class CostChangeTestTool : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Layer mask cho raycast xuống ground")]
    public LayerMask groundLayer;

    [Tooltip("Kích thước vùng ảnh hưởng (cells)")]
    public float brushSize = 3f;

    [Tooltip("Cost khi đặt obstacle")]
    public int obstacleCost = int.MaxValue;

    [Tooltip("Cost khi xóa obstacle")]
    public int clearCost = 1;

    [Header("Debug Display")]
    public bool showDebugInfo = true;

    private EntityManager _em;
    private Entity _gridEntity;
    private bool _initialized;
    private uint _lastGeneration;
    private uint _lastIslandGeneration;

    void Update()
    {
        if (!_initialized)
        {
            TryInitialize();
            return;
        }

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        bool clicked = mouse.leftButton.wasPressedThisFrame;

        if (clicked && keyboard.tKey.isPressed)
        {
            TrySendCostChange(obstacleCost);
        }

        if (clicked && keyboard.yKey.isPressed)
        {
            TrySendCostChange(clearCost);
        }

        if (showDebugInfo && _em.Exists(_gridEntity))
        {
            var grid = _em.GetComponentData<GridComponent>(_gridEntity);
            _lastGeneration = grid.generation;
            _lastIslandGeneration = grid.islandGeneration;
        }
    }

    void TryInitialize()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        _em = world.EntityManager;

        var query = _em.CreateEntityQuery(typeof(GridComponent));
        if (query.IsEmpty) return;

        _gridEntity = query.GetSingletonEntity();
        _initialized = true;
        Debug.Log("[CostChangeTestTool] Initialized. T+Click=Obstacle, Y+Click=Clear");
    }

    void TrySendCostChange(int newCost)
    {
        if (!_em.Exists(_gridEntity)) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            Debug.LogWarning("[CostChangeTestTool] Raycast miss! Kiểm tra groundLayer mask.");
            return;
        }

        float halfSize = brushSize * 0.5f;
        float2 worldMin = new float2(hit.point.x - halfSize, hit.point.z - halfSize);
        float2 worldMax = new float2(hit.point.x + halfSize, hit.point.z + halfSize);

        StartEndRect area = new StartEndRect(worldMin);
        area.ExpandTo(worldMax);

        var buffer = _em.GetBuffer<CostChangeRequest>(_gridEntity);
        buffer.Add(new CostChangeRequest
        {
            newCost = newCost,
            area = area
        });

        string action = newCost >= 255 ? "OBSTACLE" : "CLEAR";
        Debug.Log($"[CostChangeTestTool] {action} at world({hit.point.x:F1}, {hit.point.z:F1})");
    }

    void OnGUI()
    {
        if (!showDebugInfo || !_initialized) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.yellow;

        float y = 10;
        GUI.Label(new Rect(10, y, 400, 25), $"Grid Generation: {_lastGeneration}", style);
        y += 20;
        GUI.Label(new Rect(10, y, 400, 25), $"Island Generation: {_lastIslandGeneration}", style);
        y += 20;
        GUI.Label(new Rect(10, y, 400, 25), "T+Click = Obstacle | Y+Click = Clear", style);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;

public class CameraScript : MonoBehaviour
{
    [SerializeField] InputAction move;
    [SerializeField, Min(1)] private float panSpeed = 65f;
    [SerializeField, Min(1)] private float fastPanMultiplier = 1.8f;
    private World cameraWorld;
    private EntityQuery grids;
    private void OnEnable() => move?.Enable();
    private void OnDisable()
    {
        move?.Disable();
        if (cameraWorld != null && cameraWorld.IsCreated) grids.Dispose();
        cameraWorld = null;
    }

    // Update is called once per frame
    void Update()
    {
        if (!Application.isFocused || HudSystemMenu.IsOpen || PlayableSessionController.BlocksInput || GameplayInputGate.IsEditingText) return;
        var selection = GameManager.Instance.GetModule<SelectManager>();
        if (selection != null && selection.GetCurrentSelectionRect().isNotNull)
        {
            return;
        }
        if (move == null) return;
        var input=Vector2.ClampMagnitude(move.ReadValue<Vector2>(),1);var camera=Camera.main;
        Vector3 right=camera?Vector3.ProjectOnPlane(camera.transform.right,Vector3.up).normalized:Vector3.right;
        Vector3 up=camera?Vector3.ProjectOnPlane(camera.transform.up,Vector3.up).normalized:Vector3.forward;
        bool fast = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        transform.position += (right*input.x+up*input.y)*Time.unscaledDeltaTime*panSpeed*(fast?fastPanMultiplier:1f);
        ClampToBattlefield(camera);
    }

    private void ClampToBattlefield(Camera view)
    {
        var current = World.DefaultGameObjectInjectionWorld;
        if (cameraWorld != current)
        {
            if (cameraWorld != null && cameraWorld.IsCreated) grids.Dispose();
            cameraWorld = current;
            if (cameraWorld != null && cameraWorld.IsCreated) grids = cameraWorld.EntityManager.CreateEntityQuery(typeof(GridComponent));
        }
        if (view == null || cameraWorld == null || !cameraWorld.IsCreated || grids.CalculateEntityCount() != 1) return;
        var grid = grids.GetSingleton<GridComponent>(); if (!grid.initialized) return;
        var ray = view.ViewportPointToRay(new Vector3(.5f,.5f,0));
        if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
        var focus = ray.GetPoint(distance);
        float halfWidth = view.orthographicSize * view.aspect;
        float halfDepth = view.orthographicSize / Mathf.Max(.1f, Mathf.Abs(view.transform.forward.y));
        var right = view.transform.right; var forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
        float padX = Mathf.Min(grid.width * grid.cellsize * .49f, Mathf.Abs(right.x) * halfWidth + Mathf.Abs(forward.x) * halfDepth);
        float padZ = Mathf.Min(grid.height * grid.cellsize * .49f, Mathf.Abs(right.z) * halfWidth + Mathf.Abs(forward.z) * halfDepth);
        float x = Mathf.Clamp(focus.x, grid.origin.x + padX, grid.origin.x + grid.width * grid.cellsize - padX);
        float z = Mathf.Clamp(focus.z, grid.origin.z + padZ, grid.origin.z + grid.height * grid.cellsize - padZ);
        transform.position += new Vector3(x-focus.x,0,z-focus.z);
    }

    Vector3 ConvertCamToWorld(Vector2 vector)
    {
        Ray ray= Camera.main.ScreenPointToRay(vector);
        if(TerrainVisualSurface.Active!=null && TerrainVisualSurface.Active.Raycast(ray,out var logical,out _)) return logical;
        if (Physics.Raycast(ray, out RaycastHit hitInfo,Mathf.Infinity, LayerMask.GetMask("Ground")))
        {
            return hitInfo.point;
        }
        return Vector3.zero;    
    }
}

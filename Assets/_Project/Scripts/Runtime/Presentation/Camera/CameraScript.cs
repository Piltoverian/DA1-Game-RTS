using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class CameraScript : MonoBehaviour
{
    [SerializeField] InputAction move;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        move.Enable();
    }

    // Update is called once per frame
    void Update()
    {
        if (GameManager.Instance.GetModule<SelectManager>().GetCurrentSelectionRect().isNotNull)
        {
            return;
        }
        var input=move.ReadValue<Vector2>();var camera=Camera.main;
        Vector3 right=camera?camera.transform.right:Vector3.right;
        Vector3 up=camera?Vector3.ProjectOnPlane(camera.transform.up,Vector3.up).normalized:Vector3.forward;
        transform.position += (right*input.x+up*input.y)*Time.deltaTime*50f;
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

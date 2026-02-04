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
        transform.position += new Vector3(move.ReadValue<Vector2>().x, 0, move.ReadValue<Vector2>().y) * Time.deltaTime * 5f;
    }

    Vector3 ConvertCamToWorld(Vector2 vector)
    {
        Debug.Log(LayerMask.GetMask("Ground"));
        Ray ray= Camera.main.ScreenPointToRay(vector);
        if (Physics.Raycast(ray, out RaycastHit hitInfo,Mathf.Infinity, LayerMask.GetMask("Ground")))
        {
            return hitInfo.point;
        }
        Debug.Log("Cannot hit");
        return Vector3.zero;    
    }
}

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class DragSelectableObject : SelectableObject
{
    //Player this object is assigned to
    //Check if Player is null so it is neutral so can be selected by any player
    private void FixedUpdate()
    {
        if(Mouse.current.rightButton.isPressed)
        { 
        }
    }

    private void Start()
    {
    }

    Vector3 ConvertCamToWorld(Vector2 vector)
    {
        Ray ray = Camera.main.ScreenPointToRay(vector);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, LayerMask.GetMask("Ground")))
        {
            return hitInfo.point;
        } 
        return Vector3.zero;
    }
    public void OnMoveTo(Vector2 position)
    {
        Vector3 pos = ConvertCamToWorld(position);
        if (pos != Vector3.zero)
        {
            gameObject.GetComponent<NavMeshAgent>().SetDestination(pos);
        }
    }
}

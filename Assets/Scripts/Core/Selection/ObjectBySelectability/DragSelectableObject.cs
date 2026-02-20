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
}

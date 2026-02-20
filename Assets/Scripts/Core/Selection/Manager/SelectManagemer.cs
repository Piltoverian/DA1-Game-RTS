using gameManagerModule;
using NUnit.Framework;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.InputSystem;
public class SelectManager : MonoBehaviour, IFixedUpdateModule
{

    [SerializeField] private List<SelectableObject> objlist;
    [SerializeField] private List<SelectableObject> selectedobjects=new List<SelectableObject>();
    [SerializeField] private StartEndRect selectingRect;
    public void AwakeModule()
    {

    }
    public void OnGameStart()
    {
        objlist = new List<SelectableObject>(FindObjectsByType<SelectableObject>(FindObjectsSortMode.None));
    }

    // Update is called once per frame
    public void FixedUpdateModule()
    {
        Vector2 MousePos = Mouse.current.position.ReadValue();
        if (GameManager.Instance.GetModule<FixedUpdateInputTracker>().IsJustPress(Mouse.current.leftButton))
        {
            SingleSelecting();
            return;
        }
        else if (GameManager.Instance.GetModule<FixedUpdateInputTracker>().IsHolding(Mouse.current.leftButton))
        {
            if (selectingRect == null)
            {
                selectingRect = new StartEndRect(MousePos);
            }
            else
            {
                selectingRect.ExpandTo(MousePos);
                DragSelect();
                Debug.Log(selectedobjects);
                Debug.Log("Holding");
            }
            return;
        }
        else
        {
            selectingRect = null;
        }
        if (Mouse.current.rightButton.isPressed) {
            foreach (SelectableObject obj in selectedobjects)
            {
                    TargetCommand command= new TargetCommand(obj,MousePos);
                    command.Execute();   
            }
        }
    }
    public void SingleSelecting()
    {
        //if InputManager is implemented check if multi select by ctrl press enable
        selectedobjects.Clear();
        if (Camera.main==null)
        {
            Debug.Log("?????");
            return;
        }
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity,LayerMask.GetMask("Selectable")))
        {
            SelectableObject obj = hitInfo.collider.gameObject.GetComponent<SelectableObject>();
            selectedobjects.Add(obj);
        }
    }

    public void DragSelect()
    {
        foreach (SelectableObject obj in objlist)
        { 
            if(selectedobjects.Contains(obj))
            {
                continue;
            }
            Vector2 Screenpoint = Camera.main.WorldToScreenPoint(obj.gameObject.transform.position);
            if (selectingRect.isContains(Screenpoint)&&obj is DragSelectableObject)
            {
                selectedobjects.Add((SelectableObject)obj);
            }
        }
    }

    public StartEndRect GetCurrentSelectionRect()
    {
        return selectingRect;
    }

}

public class StartEndRect
{
    public Vector2 StartPoint;
    public Vector2 EndPoint;
    Texture2D whiteTexture;
    UnityEngine.Color textureColor = UnityEngine.Color.white;
    

    public void ExpandTo(Vector2 point)
    {
        EndPoint = point;
    }
    private StartEndRect()
    {
    }
    public StartEndRect(Vector2 start)
    {
        StartPoint = start;
        EndPoint = start;
        whiteTexture = new Texture2D(1,1);
        whiteTexture.SetPixel(0,0, UnityEngine.Color.white);
        whiteTexture.Apply();
    }

    public bool isContains(Vector2 point) { 
        float minX=Mathf.Min(StartPoint.x, EndPoint.x);
        float maxX=Mathf.Max(StartPoint.x,EndPoint.x);
        float minY=Mathf.Min(StartPoint.y, EndPoint.y);
        float maxY=Mathf.Max(StartPoint.y,EndPoint.y);
        if(!Inrange(minX,point.x,maxX))
        {
            return false;
        }
        if (!Inrange(minY, point.y, maxY))
        {
            return false;
        }
        return true;
    }

    public Rect ToRect()
    {
        float minX = Mathf.Min(StartPoint.x, EndPoint.x);
        float maxX = Mathf.Max(StartPoint.x, EndPoint.x);
        float minY = Mathf.Min(StartPoint.y, EndPoint.y);
        float maxY = Mathf.Max(StartPoint.y, EndPoint.y);

        return new Rect(
            minX,
            Screen.height - maxY,          
            maxX - minX,
            maxY - minY
        );
    }

    private bool Inrange(float min, float current, float max) {
        if (min > current) { 
            return false;
        }
        if (max < current)
        {
            return false;
        }
        else
        {
            return true;
        }
    }
}

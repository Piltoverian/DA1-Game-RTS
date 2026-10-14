using UnityEngine;

// Local visual preference only: never part of the authoritative match recipe.
[DisallowMultipleComponent]
public class LocalBaseCameraSnapper : MonoBehaviour
{
    public bool snapOnReady=true;
    public void Snap(Camera camera,Vector3 logicalPosition)
    {
        float distance=Mathf.Max(15,camera.transform.position.y)/Mathf.Max(.01f,-camera.transform.forward.y);
        camera.transform.position=logicalPosition-camera.transform.forward*distance;
    }
}
